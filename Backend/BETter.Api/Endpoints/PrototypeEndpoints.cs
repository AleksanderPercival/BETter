using BETter.Api.Contracts;
using BETter.Domain.Entities;
using BETter.Domain.Enums;
using BETter.Domain.Exceptions;
using BETter.Domain.Ports;
using BETter.Domain.Services;
using BETter.Infrastructure.External.Espn;
using BETter.Infrastructure.Persistence;
using BETter.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BETter.Api.Endpoints;

public static class PrototypeEndpoints
{
    public static IEndpointRouteBuilder MapPrototypeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", GetEventsAsync);
        app.MapGet("/api/wallet", GetWalletAsync);
        app.MapPost("/api/wallet/deposit", DepositAsync);
        app.MapPost("/api/slips/calculate", CalculateSlipAsync);
        app.MapPost("/api/slips/place", PlaceSlipAsync);
        app.MapGet("/api/slips", GetUserSlipsAsync);

        return app;
    }

    private static async Task<IResult> GetEventsAsync(
        bool? refresh,
        IOfferSynchronizer offerSynchronizer,
        ISportEventRepository sportEventRepository,
        BetterDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await offerSynchronizer.EnsureOfferReadyAsync(forceRefresh: refresh == true, cancellationToken);

        var events = await sportEventRepository.GetActiveEventsAsync(cancellationToken);
        var response = events.Select(MapSportEvent).ToList();

        return Results.Ok(response);
    }

    private static async Task<IResult> GetWalletAsync(
        Guid? userId,
        WalletRepository walletRepository,
        BetterDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var snapshot = await walletRepository.GetSnapshotAsync(userId ?? WalletRepository.DemoUserId, cancellationToken);

        return Results.Ok(new WalletBalanceResponse(
            snapshot.UserId,
            snapshot.WalletId,
            snapshot.AvailableBalance,
            snapshot.FrozenBalance,
            snapshot.BonusBalance));
    }

    private static async Task<IResult> DepositAsync(
        DepositRequest request,
        WalletRepository walletRepository,
        BetterDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            var effectiveUserId = request.UserId ?? WalletRepository.DemoUserId;
            var snapshot = await walletRepository.DepositAsync(effectiveUserId, request.Amount, cancellationToken);

            return Results.Ok(new WalletBalanceResponse(
                snapshot.UserId,
                snapshot.WalletId,
                snapshot.AvailableBalance,
                snapshot.FrozenBalance,
                snapshot.BonusBalance));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "Concurrent wallet modification detected. Please retry." });
        }
    }

    private static async Task<IResult> CalculateSlipAsync(
        CalculateSlipRequest request,
        ISportEventRepository sportEventRepository,
        ICouponCalculator couponCalculator,
        IAntiCorrelationValidator antiCorrelationValidator,
        BetterDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);

            if (request.Items is null || request.Items.Count == 0)
            {
                return Results.BadRequest(new { error = "At least one selection is required to calculate a bet slip." });
            }

            var betSlip = new BetSlip(Guid.NewGuid(), WalletRepository.DemoUserId, DateTimeOffset.UtcNow);

            foreach (var itemInput in request.Items)
            {
                decimal odds;
                string eventName = itemInput.EventName ?? "Match";
                string marketName = itemInput.MarketName ?? "1X2";
                string selectionName = itemInput.SelectionName ?? "Selection";

                var sportEvent = await sportEventRepository.GetByIdAsync(itemInput.SportEventId, cancellationToken);
                if (sportEvent is not null)
                {
                    eventName = sportEvent.DisplayName;
                    var market = sportEvent.Markets.FirstOrDefault(m => m.Id == itemInput.MarketId);
                    var selection = market?.Selections.FirstOrDefault(s => s.Id == itemInput.SelectionId);

                    if (market is not null)
                    {
                        marketName = market.Name;
                    }

                    if (selection is not null)
                    {
                        selectionName = selection.Name;
                        odds = itemInput.Odds ?? selection.Odds;
                    }
                    else if (itemInput.Odds.HasValue)
                    {
                        odds = itemInput.Odds.Value;
                    }
                    else
                    {
                        return Results.BadRequest(new { error = $"Selection {itemInput.SelectionId} was not found." });
                    }
                }
                else if (itemInput.Odds.HasValue)
                {
                    odds = itemInput.Odds.Value;
                }
                else
                {
                    return Results.BadRequest(new { error = $"SportEvent {itemInput.SportEventId} was not found." });
                }

                betSlip.AddItem(new BetSlipItem(
                    Guid.NewGuid(),
                    betSlip.Id,
                    itemInput.SportEventId,
                    itemInput.MarketId,
                    itemInput.SelectionId,
                    eventName,
                    marketName,
                    selectionName,
                    odds));
            }

            var correlationResult = antiCorrelationValidator.Validate(betSlip);
            if (!correlationResult.IsValid)
            {
                return Results.BadRequest(new
                {
                    error = correlationResult.ErrorMessage,
                    conflictingSportEventIds = correlationResult.ConflictingSportEventIds
                });
            }

            var calculation = couponCalculator.Calculate(
                betSlip,
                request.EffectiveGrossStake,
                request.IsTaxFreePromo);

            return Results.Ok(new CalculateSlipResponse(
                betSlip.Type.ToString(),
                calculation.GrossStake,
                calculation.NetStake,
                calculation.TaxMultiplier,
                calculation.TotalOdds,
                calculation.PotentialPayout,
                calculation.IsWithinLimits,
                calculation.ValidationError));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> PlaceSlipAsync(
        PlaceSlipRequest request,
        BetterDbContext dbContext,
        ISportEventRepository sportEventRepository,
        IBetSlipRepository betSlipRepository,
        IWalletPort walletPort,
        WalletRepository walletRepository,
        ICouponCalculator couponCalculator,
        IAntiCorrelationValidator antiCorrelationValidator,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest(new { error = "At least one selection is required to place a bet slip." });
        }

        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            var effectiveUserId = request.UserId ?? WalletRepository.DemoUserId;
            var grossStake = request.EffectiveGrossStake;

            // PU-09: Atomowa transakcja bazodanowa (ACID) obejmująca walidację, blokadę Hold w Portfelu i zapis BetSlip
            var response = await dbContext.ExecuteInTransactionAsync(async ct =>
            {
                var betSlip = new BetSlip(Guid.NewGuid(), effectiveUserId, DateTimeOffset.UtcNow);

                foreach (var itemInput in request.Items)
                {
                    var sportEvent = await sportEventRepository.GetByIdAsync(itemInput.SportEventId, ct)
                        ?? throw new DomainException($"SportEvent '{itemInput.SportEventId}' does not exist.");

                    if (sportEvent.Status is not (EventStatus.Prematch or EventStatus.Live))
                    {
                        throw new DomainException(
                            $"Event '{sportEvent.DisplayName}' is no longer open for betting (status: {sportEvent.Status}).");
                    }

                    var market = sportEvent.Markets.FirstOrDefault(m => m.Id == itemInput.MarketId)
                        ?? throw new DomainException($"Market '{itemInput.MarketId}' does not exist in '{sportEvent.DisplayName}'.");

                    // PU-09 / 4a: Weryfikacja blokady rynku (Suspended Market)
                    if (market.IsSuspended)
                    {
                        throw new DomainException(
                            $"Market '{market.Name}' for '{sportEvent.DisplayName}' is currently suspended (Suspended Market).");
                    }

                    var selection = market.Selections.FirstOrDefault(s => s.Id == itemInput.SelectionId)
                        ?? throw new DomainException($"Selection '{itemInput.SelectionId}' does not exist.");

                    if (selection.Status != SelectionStatus.Pending)
                    {
                        throw new DomainException($"Selection '{selection.Name}' is already settled.");
                    }

                    // PU-09 / 4a: Weryfikacja spójności kursu względem widoku gracza
                    if (itemInput.Odds.HasValue)
                    {
                        var clientOdds = Math.Round(itemInput.Odds.Value, 2, MidpointRounding.AwayFromZero);
                        var currentOdds = Math.Round(selection.Odds, 2, MidpointRounding.AwayFromZero);
                        if (clientOdds != currentOdds)
                        {
                            throw new DomainException(
                                $"Odds for '{sportEvent.DisplayName} ({selection.Name})' changed from {clientOdds:F2} to {currentOdds:F2}.");
                        }
                    }

                    betSlip.AddItem(new BetSlipItem(
                        Guid.NewGuid(),
                        betSlip.Id,
                        sportEvent.Id,
                        market.Id,
                        selection.Id,
                        sportEvent.DisplayName,
                        market.Name,
                        selection.Name,
                        selection.Odds));
                }

                // PU-09 / 3: Ponowna walidacja anty-korelacyjna po stronie serwera (PU-08)
                var correlationResult = antiCorrelationValidator.Validate(betSlip);
                if (!correlationResult.IsValid)
                {
                    throw new DomainException(correlationResult.ErrorMessage ?? "Correlated selections are not allowed on an AKO slip.");
                }

                // PU-09 / 3: Wyliczenie podatku 12%, kursu całkowitego i limitów (PU-07)
                var calculation = couponCalculator.Calculate(betSlip, grossStake, request.IsTaxFreePromo);
                betSlip.ApplyCalculation(calculation);

                // PU-09 / 5: Pre-autoryzacja stawki (Hold) i zapis w Rejestrze (Ledger) - PU-05
                var holdSucceeded = await walletPort.HoldStakeAsync(
                    effectiveUserId,
                    betSlip.Id,
                    betSlip.GrossStake,
                    ct);

                if (!holdSucceeded)
                {
                    throw new DomainException("Insufficient available funds on deposit balance to place this bet (Hold rejected).");
                }

                // PU-09 / 6: Zapis encji kuponu ze statusem OPEN
                await betSlipRepository.AddAsync(betSlip, ct);

                var walletSnapshot = await walletRepository.GetSnapshotAsync(effectiveUserId, ct);

                return new PlaceSlipResponse(
                    betSlip.Id,
                    betSlip.Status.ToString(),
                    betSlip.Type.ToString(),
                    betSlip.GrossStake,
                    betSlip.NetStake,
                    betSlip.TaxMultiplier,
                    betSlip.TotalOdds,
                    betSlip.PotentialPayout,
                    betSlip.CreatedAt,
                    walletSnapshot.AvailableBalance,
                    walletSnapshot.FrozenBalance,
                    walletSnapshot.BonusBalance);
            }, cancellationToken);

            return Results.Created($"/api/slips/{response.SlipId}", response);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "Concurrency conflict detected while updating wallet balance (Double-Spending protection)." });
        }
    }

    private static async Task<IResult> GetUserSlipsAsync(
        Guid? userId,
        IBetSlipRepository betSlipRepository,
        BetterDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var slips = await betSlipRepository.GetByUserIdAsync(userId ?? WalletRepository.DemoUserId, cancellationToken);
        return Results.Ok(slips);
    }

    private static SportEventResponse MapSportEvent(SportEvent sportEvent)
    {
        var isLive = sportEvent.Status == EventStatus.Live;
        var formattedStart = FormatDisplayStartTime(sportEvent);

        var markets = sportEvent.Markets
            .Select(m => new MarketResponse(
                m.Id,
                m.Name,
                m.IsSuspended,
                m.Selections
                    .OrderBy(s => GetSelectionSortOrder(s.Name))
                    .Select(s => new SelectionResponse(
                        s.Id,
                        s.Name,
                        Math.Round(s.Odds, 2, MidpointRounding.AwayFromZero),
                        m.IsSuspended || s.Status != SelectionStatus.Pending))
                    .ToList()))
            .ToList();

        return new SportEventResponse(
            sportEvent.Id,
            sportEvent.ExternalId,
            sportEvent.Sport,
            sportEvent.League,
            formattedStart,
            sportEvent.StartTime,
            sportEvent.HomeTeam,
            sportEvent.AwayTeam,
            sportEvent.Status.ToString(),
            isLive,
            sportEvent.HomeScore,
            sportEvent.AwayScore,
            markets);
    }

    private static string FormatDisplayStartTime(SportEvent sportEvent)
    {
        if (sportEvent.Status == EventStatus.Live)
        {
            return sportEvent.HomeScore.HasValue && sportEvent.AwayScore.HasValue
                ? $"LIVE • {sportEvent.HomeScore}:{sportEvent.AwayScore}"
                : "LIVE";
        }

        var localStart = sportEvent.StartTime.ToLocalTime();
        var today = DateTimeOffset.Now.Date;

        if (localStart.Date == today)
        {
            return $"Dziś, {localStart:HH:mm}";
        }

        if (localStart.Date == today.AddDays(1))
        {
            return $"Jutro, {localStart:HH:mm}";
        }

        return $"{localStart:dd.MM, HH:mm}";
    }

    private static int GetSelectionSortOrder(string name) =>
        name.ToUpperInvariant() switch
        {
            "1" => 0,
            "X" => 1,
            "2" => 2,
            _ => 3
        };
}
