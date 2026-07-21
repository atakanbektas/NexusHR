using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusHR.Candidate.Application.Abstractions.Persistence;
using NexusHR.Candidate.Infrastructure.Persistence;
using NexusHR.Candidate.Infrastructure.Repositories;
using NexusHR.Candidate.Application.Abstractions.Documents;
using NexusHR.Candidate.Infrastructure.Documents;


namespace NexusHR.Candidate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(
            "CandidateDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CandidateDatabase connection string bulunamadı.");
        }

        services.AddDbContext<CandidateDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ICandidateRepository,CandidateRepository>();
        services.AddScoped<ICvInformationExtractor,PdfCvInformationExtractor>();

        return services;
    }
}