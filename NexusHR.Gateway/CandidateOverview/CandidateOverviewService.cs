using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NexusHR.Gateway.CandidateOverview;

internal sealed class CandidateOverviewService(
    IHttpClientFactory httpClientFactory)
{
    private const int DownstreamPageSize = 100;

    private static readonly HashSet<string> CandidateStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Draft",
            "DocumentsPending",
            "ReadyForHiring",
            "Archived"
        };

    private static readonly HashSet<string> HiringStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Draft",
            "OfferPrepared",
            "OfferSent",
            "OfferAccepted",
            "OfferRejected",
            "Cancelled",
            "Completed"
        };

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<CandidateOverviewResponse> GetAsync(
        int page,
        int pageSize,
        string? search,
        string? statusFilter,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => pageSize
        };

        var filter = ParseStatusFilter(statusFilter);

        var candidates = await GetAllCandidatesAsync(
            search,
            filter?.Source == CandidateOverviewStatusSource.Candidate
                ? filter.Status
                : null,
            authorizationHeader,
            cancellationToken);

        var hiringStatuses = await GetHiringStatusesAsync(
            candidates.Select(candidate => candidate.Id).ToArray(),
            authorizationHeader,
            cancellationToken);

        var hiringStatusByCandidateId = hiringStatuses
            .ToDictionary(
                hiringStatus => hiringStatus.CandidateId,
                hiringStatus => hiringStatus);

        var overviewItems = candidates
            .Select(candidate =>
            {
                hiringStatusByCandidateId.TryGetValue(
                    candidate.Id,
                    out var hiringStatus);

                var displayStatusSource = hiringStatus is null
                    ? "Candidate"
                    : "Hiring";

                var displayStatus = hiringStatus?.Status
                    ?? candidate.Status;

                return new CandidateOverviewItemResponse(
                    candidate.Id,
                    candidate.FirstName,
                    candidate.LastName,
                    candidate.Email,
                    candidate.PhoneNumber,
                    candidate.Status,
                    hiringStatus?.HiringProcessId,
                    hiringStatus?.Status,
                    displayStatusSource,
                    displayStatus,
                    candidate.CreatedAtUtc);
            })
            .Where(item => MatchesFilter(item, filter))
            .ToArray();

        var totalCount = overviewItems.Length;

        var items = overviewItems
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArray();

        return new CandidateOverviewResponse(
            items,
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }

    private async Task<IReadOnlyCollection<CandidateItemResponse>>
        GetAllCandidatesAsync(
            string? search,
            string? candidateStatus,
            string? authorizationHeader,
            CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(
            CandidateOverviewHttpClients.CandidateApi);

        var candidates = new List<CandidateItemResponse>();
        var currentPage = 1;
        var totalCount = int.MaxValue;

        while (candidates.Count < totalCount)
        {
            var queryParameters = new List<string>
            {
                $"page={currentPage}",
                $"pageSize={DownstreamPageSize}"
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                queryParameters.Add(
                    $"search={Uri.EscapeDataString(search.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(candidateStatus))
            {
                queryParameters.Add(
                    $"status={Uri.EscapeDataString(candidateStatus)}");
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/candidates?{string.Join('&', queryParameters)}");

            AddAuthorizationHeader(
                request,
                authorizationHeader);

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            await EnsureSuccessAsync(
                response,
                "Candidate API",
                cancellationToken);

            var pageResponse = await response.Content
                .ReadFromJsonAsync<CandidatePageResponse>(
                    JsonOptions,
                    cancellationToken)
                ?? throw new CandidateOverviewDownstreamException(
                    HttpStatusCode.BadGateway,
                    "Candidate API geçersiz bir yanıt döndürdü.");

            totalCount = pageResponse.TotalCount;
            candidates.AddRange(pageResponse.Items);

            if (pageResponse.Items.Count == 0)
            {
                break;
            }

            currentPage++;
        }

        return candidates;
    }

    private async Task<IReadOnlyCollection<
        CandidateHiringProcessStatusResponse>>
        GetHiringStatusesAsync(
            IReadOnlyCollection<Guid> candidateIds,
            string? authorizationHeader,
            CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0)
        {
            return Array.Empty<
                CandidateHiringProcessStatusResponse>();
        }

        var client = httpClientFactory.CreateClient(
            CandidateOverviewHttpClients.HiringApi);

        var results = new List<
            CandidateHiringProcessStatusResponse>();

        foreach (var batch in candidateIds.Chunk(100))
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/hiring-processes/active/by-candidates")
            {
                Content = JsonContent.Create(
                    new CandidateIdsRequest(batch),
                    options: JsonOptions)
            };

            AddAuthorizationHeader(
                request,
                authorizationHeader);

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            await EnsureSuccessAsync(
                response,
                "Hiring API",
                cancellationToken);

            var batchResponse = await response.Content
                .ReadFromJsonAsync<
                    CandidateHiringProcessStatusResponse[]>(
                    JsonOptions,
                    cancellationToken)
                ?? Array.Empty<
                    CandidateHiringProcessStatusResponse>();

            results.AddRange(batchResponse);
        }

        return results;
    }

    private static CandidateOverviewStatusFilter? ParseStatusFilter(
        string? statusFilter)
    {
        if (string.IsNullOrWhiteSpace(statusFilter))
        {
            return null;
        }

        var parts = statusFilter.Split(
            ':',
            count: 2,
            StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            throw new CandidateOverviewValidationException(
                "Durum filtresi geçersiz.");
        }

        if (parts[0].Equals(
                "candidate",
                StringComparison.OrdinalIgnoreCase) &&
            CandidateStatuses.Contains(parts[1]))
        {
            return new CandidateOverviewStatusFilter(
                CandidateOverviewStatusSource.Candidate,
                GetCanonicalStatus(
                    CandidateStatuses,
                    parts[1]));
        }

        if (parts[0].Equals(
                "hiring",
                StringComparison.OrdinalIgnoreCase) &&
            HiringStatuses.Contains(parts[1]))
        {
            return new CandidateOverviewStatusFilter(
                CandidateOverviewStatusSource.Hiring,
                GetCanonicalStatus(
                    HiringStatuses,
                    parts[1]));
        }

        throw new CandidateOverviewValidationException(
            "Durum filtresi geçersiz.");
    }

    private static string GetCanonicalStatus(
        IEnumerable<string> statuses,
        string status)
    {
        return statuses.Single(item =>
            item.Equals(
                status,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesFilter(
        CandidateOverviewItemResponse item,
        CandidateOverviewStatusFilter? filter)
    {
        if (filter is null)
        {
            return true;
        }

        var expectedSource = filter.Source ==
            CandidateOverviewStatusSource.Candidate
                ? "Candidate"
                : "Hiring";

        return item.DisplayStatusSource.Equals(
                   expectedSource,
                   StringComparison.OrdinalIgnoreCase) &&
               item.DisplayStatus.Equals(
                   filter.Status,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void AddAuthorizationHeader(
        HttpRequestMessage request,
        string? authorizationHeader)
    {
        if (!string.IsNullOrWhiteSpace(authorizationHeader))
        {
            request.Headers.TryAddWithoutValidation(
                "Authorization",
                authorizationHeader);
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string serviceName,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content
            .ReadAsStringAsync(cancellationToken);

        throw new CandidateOverviewDownstreamException(
            response.StatusCode,
            $"{serviceName} isteği başarısız oldu: {responseBody}");
    }
}

internal static class CandidateOverviewHttpClients
{
    public const string CandidateApi = "CandidateOverview.CandidateApi";
    public const string HiringApi = "CandidateOverview.HiringApi";
}

internal enum CandidateOverviewStatusSource
{
    Candidate,
    Hiring
}

internal sealed record CandidateOverviewStatusFilter(
    CandidateOverviewStatusSource Source,
    string Status);

internal sealed class CandidateOverviewValidationException(
    string message)
    : Exception(message);

internal sealed class CandidateOverviewDownstreamException(
    HttpStatusCode statusCode,
    string message)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
