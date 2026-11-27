using BETter.Domain.Ports;
using BETter.Infrastructure.Adapters;
using BETter.Infrastructure.External.Espn;
using BETter.Infrastructure.Persistence;
using BETter.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BETter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBetterInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useInMemory = string.Equals(
            configuration["Database:UseInMemory"],
            "true",
            StringComparison.OrdinalIgnoreCase);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=better;Username=better;Password=better";

        services.AddDbContext<BetterDbContext>(options =>
        {
            if (useInMemory)
            {
                options.UseInMemoryDatabase("BETterPrototypeDb");
            }
            else
            {
                options.UseNpgsql(connectionString);
            }
        });

        services.AddScoped<ISportEventRepository, SportEventRepository>();
        services.AddScoped<IBetSlipRepository, BetSlipRepository>();
        services.AddScoped<WalletRepository>();
        services.AddScoped<IWalletPort, WalletPortAdapter>();

        services.Configure<EspnOptions>(configuration.GetSection(EspnOptions.SectionName));

        var espnOptions = new EspnOptions();
        configuration.GetSection(EspnOptions.SectionName).Bind(espnOptions);

        services.AddHttpClient<IEspnApiClient, EspnApiClient>(client =>
        {
            client.BaseAddress = new Uri(espnOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(2, espnOptions.TimeoutSeconds));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BETter-Sportsbook/1.0");
        });

        services.AddScoped<IOfferSynchronizer, EspnOfferSynchronizer>();

        return services;
    }
}
