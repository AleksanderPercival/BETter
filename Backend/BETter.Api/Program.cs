using BETter.Domain.Services;

var builder = WebApplication.CreateBuilder(args);

// Inside (Domain & Wallet) services
builder.Services.AddSingleton<ICouponCalculator>(_ => new CouponCalculator());
builder.Services.AddSingleton<IAntiCorrelationValidator, AntiCorrelationValidator>();

// TODO (BETter.Wallet): register WalletService implementing IWalletPort
// TODO (BETter.Infrastructure): register BetterDbContext (PostgreSQL) & repository adapters

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

app.UseCors("FrontendDev");

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "BETter.Api",
    timestampUtc = DateTimeOffset.UtcNow
}));

// TODO (Etap 3 - Walking Skeleton):
// GET  /api/events
// POST /api/wallet/deposit
// POST /api/slips/calculate
// POST /api/slips/place

app.Run();
