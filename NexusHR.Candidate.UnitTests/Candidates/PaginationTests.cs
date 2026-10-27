using System.Reflection;
using MediatR;

using NexusHR.Candidate.Application;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Application.Candidates.GetCandidates;

namespace NexusHR.Candidate.UnitTests.Candidates;

public class PaginationTests
{
    [Theory]
    [InlineData(int.MaxValue, 100, false)]
    [InlineData(int.MaxValue, int.MaxValue, false)]
    [InlineData(2, 20, true)]
    [InlineData(-1, 0, true)]
    public async Task ExtremePagesNeverWrapOrQueryAnEarlierPage(int page, int size, bool queriesPage)
    {
        var repository = DispatchProxy.Create<ICandidateRepository, RepositoryProxy>();
        var handler = (IRequestHandler<GetCandidatesQuery, GetCandidatesResponse>)Activator.CreateInstance(
            typeof(DependencyInjection).Assembly.GetType("NexusHR.Candidate.Application.Candidates.GetCandidates.GetCandidatesQueryHandler")!, repository)!;
        var response = await handler.Handle(new GetCandidatesQuery(page, size, null, null), default);
        Assert.Equal(queriesPage, ((RepositoryProxy)(object)repository).PageQueried);
        Assert.Empty(response.Items);
        Assert.Equal(Math.Max(1, page), response.Page);
        Assert.Equal(3, response.TotalCount);
    }

    public class RepositoryProxy : DispatchProxy
    {
        public bool PageQueried { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "CountAsync") return Task.FromResult(3);
            if (method.Name == "GetPageAsync")
            {
                PageQueried = true;
                Assert.InRange((int)args![0]!, 0, int.MaxValue);
                return Task.FromResult<IReadOnlyCollection<Domain.Candidates.Candidate>>([]);
            }
            throw new NotSupportedException(method.Name);
        }
    }
}

