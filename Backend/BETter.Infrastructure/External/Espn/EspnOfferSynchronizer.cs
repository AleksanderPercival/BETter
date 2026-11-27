using BETter.Domain.Entities;
using BETter.Domain.Enums;
using BETter.Domain.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BETter.Infrastructure.External.Espn;

public interface IOfferSynchronizer
{
    Task EnsureOfferReadyAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

public class EspnOfferSynchronizer : IOfferSynchronizer
{
    private static readonly SemaphoreSlim SyncLock = new(1, 1);
    private static DateTimeOffset _lastSyncUtc = DateTimeOffset.MinValue;

    private readonly IEspnApiClient _espnApiClient;
    private readonly ISportEventRepository _sportEventRepository;
    private readonly EspnOptions _options;
    private readonly ILogger<EspnOfferSynchronizer> _logger;

    public EspnOfferSynchronizer(
        IEspnApiClient espnApiClient,
        ISportEventRepository sportEventRepository,
        IOptions<EspnOptions> options,
        ILogger<EspnOfferSynchronizer> logger)
    {
        _espnApiClient = espnApiClient;
        _sportEventRepository = sportEventRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureOfferReadyAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (!forceRefresh && (now - _lastSyncUtc).TotalSeconds < _options.SyncIntervalSeconds)
        {
            return;
        }

        await SyncLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (!forceRefresh && (now - _lastSyncUtc).TotalSeconds < _options.SyncIntervalSeconds)
            {
                return;
            }

            bool anyNetworkFailure = false;

            foreach (var league in _options.Leagues)
            {
                try
                {
                    var snapshots = await _espnApiClient.FetchLeagueScoreboardAsync(
                        league.Slug,
                        league.DisplayName,
                        cancellationToken);

                    foreach (var snapshot in snapshots.Take(12))
                    {
                        await UpsertEventSnapshotAsync(snapshot, cancellationToken);
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation(
                        "ESPN league {LeagueSlug} returned {StatusCode} (skipping league).",
                        league.Slug,
                        ex.StatusCode);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    anyNetworkFailure = true;
                    _logger.LogWarning(
                        "ESPN API unreachable for league {LeagueSlug} ({Reason}). Applying safety suspension for Live events (PU-13 / 1a).",
                        league.Slug,
                        ex.Message);
                }
            }

            if (anyNetworkFailure)
            {
                await SuspendLiveEventsForSafetyAsync(cancellationToken);
            }

            var activeEvents = await _sportEventRepository.GetActiveEventsAsync(cancellationToken);
            if (activeEvents.Count == 0)
            {
                _logger.LogInformation("No active events in database after ESPN sync. Seeding fallback offer.");
                await SeedFallbackEventsAsync(cancellationToken);
            }

            _lastSyncUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private async Task UpsertEventSnapshotAsync(
        ExternalMatchSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var existing = await _sportEventRepository.GetByExternalIdAsync(snapshot.ExternalId, cancellationToken);

        if (existing is null)
        {
            var eventId = Guid.NewGuid();
            var sportEvent = new SportEvent(
                eventId,
                snapshot.ExternalId,
                snapshot.Sport,
                snapshot.League,
                snapshot.HomeTeam,
                snapshot.AwayTeam,
                snapshot.StartTime,
                snapshot.Status);

            if (snapshot.HomeScore.HasValue && snapshot.AwayScore.HasValue)
            {
                sportEvent.UpdateScore(snapshot.HomeScore.Value, snapshot.AwayScore.Value);
            }

            var marketId = Guid.NewGuid();
            var market = new Market(marketId, eventId, "1X2", snapshot.IsSuspended);
            market.AddSelection(new Selection(Guid.NewGuid(), marketId, eventId, "1", snapshot.HomeOdds));
            market.AddSelection(new Selection(Guid.NewGuid(), marketId, eventId, "X", snapshot.DrawOdds));
            market.AddSelection(new Selection(Guid.NewGuid(), marketId, eventId, "2", snapshot.AwayOdds));

            sportEvent.AddMarket(market);
            await _sportEventRepository.SaveAsync(sportEvent, cancellationToken);
            return;
        }

        if (snapshot.HomeScore.HasValue && snapshot.AwayScore.HasValue)
        {
            existing.UpdateScore(snapshot.HomeScore.Value, snapshot.AwayScore.Value);
        }

        existing.ChangeStatus(snapshot.Status);

        var primaryMarket = existing.Markets.FirstOrDefault(m => m.Name.StartsWith("1X2", StringComparison.OrdinalIgnoreCase));
        if (primaryMarket is not null && existing.Status is EventStatus.Prematch or EventStatus.Live)
        {
            if (snapshot.IsSuspended)
            {
                primaryMarket.Suspend();
            }
            else
            {
                primaryMarket.Resume();
            }

            UpdateSelectionIfPending(primaryMarket, "1", snapshot.HomeOdds);
            UpdateSelectionIfPending(primaryMarket, "X", snapshot.DrawOdds);
            UpdateSelectionIfPending(primaryMarket, "2", snapshot.AwayOdds);
        }

        await _sportEventRepository.SaveAsync(existing, cancellationToken);
    }

    private static void UpdateSelectionIfPending(Market market, string selectionName, decimal newOdds)
    {
        var selection = market.Selections.FirstOrDefault(s =>
            string.Equals(s.Name, selectionName, StringComparison.OrdinalIgnoreCase));

        if (selection is not null && selection.Status == SelectionStatus.Pending)
        {
            selection.UpdateOdds(newOdds);
        }
    }

    private async Task SuspendLiveEventsForSafetyAsync(CancellationToken cancellationToken)
    {
        var liveEvents = await _sportEventRepository.GetByStatusAsync(EventStatus.Live, cancellationToken);
        foreach (var liveEvent in liveEvents)
        {
            liveEvent.SuspendAllMarkets();
            await _sportEventRepository.SaveAsync(liveEvent, cancellationToken);
        }
    }

    private async Task SeedFallbackEventsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var seeds = new[]
        {
            new ExternalMatchSnapshot(
                "seed:ita.1:inter-milan",
                "Soccer",
                "Serie A",
                "Inter Mediolan",
                "AC Milan",
                now.AddHours(3),
                EventStatus.Prematch,
                null,
                null,
                2.05m,
                3.45m,
                3.60m,
                IsSuspended: false),
            new ExternalMatchSnapshot(
                "seed:ita.1:juventus-napoli",
                "Soccer",
                "Serie A",
                "Juventus",
                "SSC Napoli",
                now.AddHours(5),
                EventStatus.Prematch,
                null,
                null,
                2.25m,
                3.25m,
                3.30m,
                IsSuspended: false),
            new ExternalMatchSnapshot(
                "seed:pol.1:gornik-legia",
                "Soccer",
                "PKO BP Ekstraklasa",
                "Górnik Zabrze",
                "Legia Warszawa",
                now.AddHours(2),
                EventStatus.Prematch,
                null,
                null,
                2.65m,
                3.30m,
                2.55m,
                IsSuspended: false),
            new ExternalMatchSnapshot(
                "seed:eng.1:mancity-villa",
                "Soccer",
                "Premier League",
                "Manchester City",
                "Aston Villa",
                now.AddMinutes(-35),
                EventStatus.Live,
                1,
                1,
                1.65m,
                3.90m,
                5.10m,
                IsSuspended: false),
            new ExternalMatchSnapshot(
                "seed:ucl:real-bayern",
                "Soccer",
                "UEFA Champions League",
                "Real Madryt",
                "Bayern Monachium",
                now.AddDays(1),
                EventStatus.Prematch,
                null,
                null,
                2.10m,
                3.65m,
                3.25m,
                IsSuspended: false)
        };

        foreach (var seed in seeds)
        {
            await UpsertEventSnapshotAsync(seed, cancellationToken);
        }
    }
}
