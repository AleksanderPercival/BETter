using BETter.Api.Endpoints;
using BETter.Domain.Services;
using BETter.Infrastructure;
using BETter.Infrastructure.External.Espn;
using BETter.Infrastructure.Persistence;
using BETter.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Inside (Domain) services
builder.Services.AddSingleton<ICouponCalculator>(_ => new CouponCalculator());
builder.Services.AddSingleton<IAntiCorrelationValidator, AntiCorrelationValidator>();

// Outside (Infrastructure): PostgreSQL (BetterDbContext), Repositories, IWalletPort adapter & ESPN HttpClient
builder.Services.AddBetterInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Inicjalizacja schematu bazy danych, konta demo i pierwszej synchronizacji oferty ESPN przy starcie
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<BetterDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var walletRepository = scope.ServiceProvider.GetRequiredService<WalletRepository>();
        await walletRepository.GetOrCreateByUserIdAsync(WalletRepository.DemoUserId);

        var offerSynchronizer = scope.ServiceProvider.GetRequiredService<IOfferSynchronizer>();
        await offerSynchronizer.EnsureOfferReadyAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(
            ex,
            "Database startup initialization skipped (verify PostgreSQL container via 'docker compose -f Backend/docker-compose.yml up -d').");
    }
}

app.UseCors("FrontendDev");

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "BETter.Api",
    timestampUtc = DateTimeOffset.UtcNow
}));

app.MapPrototypeEndpoints();

app.Run();
