using System.Net.Http.Json;
using System.Text.Json;
using BETter.Domain.Enums;

namespace BETter.Infrastructure.External.Espn;

public sealed record ExternalMatchSnapshot(
    string ExternalId,
    string Sport,
    string League,
    string HomeTeam,
    string AwayTeam,
    DateTimeOffset StartTime,
    EventStatus Status,
    int? HomeScore,
    int? AwayScore,
    decimal HomeOdds,
    decimal DrawOdds,
    decimal AwayOdds,
    bool IsSuspended);

public interface IEspnApiClient
{
    Task<IReadOnlyList<ExternalMatchSnapshot>> FetchLeagueScoreboardAsync(
        string leagueSlug,
        string fallbackLeagueName,
        CancellationToken cancellationToken = default);
}

public class EspnApiClient : IEspnApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public EspnApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ExternalMatchSnapshot>> FetchLeagueScoreboardAsync(
        string leagueSlug,
        string fallbackLeagueName,
        CancellationToken cancellationToken = default)
    {
        // Podstawowy endpoint z dokumentacji projektu (np. apis/site/v2/sports/soccer/ita.1/scoreboard)
        var baseEndpoint = $"apis/site/v2/sports/soccer/{leagueSlug}/scoreboard";
        var response = await _httpClient.GetFromJsonAsync<EspnScoreboardResponse>(
            baseEndpoint,
            JsonOptions,
            cancellationToken);

        var snapshots = MapScoreboard(response, fallbackLeagueName);

        // Jeżeli w bieżącym dniu nie ma aktywnych meczów (Prematch/Live), spróbuj pobrać terminarz na najbliższe 7 dni
        if (!snapshots.Any(s => s.Status is EventStatus.Prematch or EventStatus.Live))
        {
            var today = DateTime.UtcNow.Date;
            var end = today.AddDays(7);
            var rangeEndpoint = $"{baseEndpoint}?dates={today:yyyyMMdd}-{end:yyyyMMdd}";

            try
            {
                var rangeResponse = await _httpClient.GetFromJsonAsync<EspnScoreboardResponse>(
                    rangeEndpoint,
                    JsonOptions,
                    cancellationToken);

                var rangeSnapshots = MapScoreboard(rangeResponse, fallbackLeagueName);
                if (rangeSnapshots.Count > 0)
                {
                    snapshots = rangeSnapshots;
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound)
            {
                // Niektóre rozgrywki turniejowe (np. UEFA) odrzucają zakres dat 400/404 poza oknem fazy - zachowujemy wynik podstawowy
            }
        }

        return snapshots;
    }

    private static List<ExternalMatchSnapshot> MapScoreboard(
        EspnScoreboardResponse? scoreboard,
        string fallbackLeagueName)
    {
        var results = new List<ExternalMatchSnapshot>();
        if (scoreboard?.Events is null || scoreboard.Events.Count == 0)
        {
            return results;
        }

        var leagueName = !string.IsNullOrWhiteSpace(fallbackLeagueName)
            ? fallbackLeagueName
            : scoreboard.Leagues?.FirstOrDefault()?.Name ?? "Piłka nożna";

        foreach (var ev in scoreboard.Events)
        {
            var competition = ev.Competitions?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(ev.Id) || competition?.Competitors is null)
            {
                continue;
            }

            var home = competition.Competitors.FirstOrDefault(c =>
                string.Equals(c.HomeAway, "home", StringComparison.OrdinalIgnoreCase));
            var away = competition.Competitors.FirstOrDefault(c =>
                string.Equals(c.HomeAway, "away", StringComparison.OrdinalIgnoreCase));

            var homeName = home?.Team?.ShortDisplayName ?? home?.Team?.DisplayName ?? home?.Team?.Name;
            var awayName = away?.Team?.ShortDisplayName ?? away?.Team?.DisplayName ?? away?.Team?.Name;

            if (string.IsNullOrWhiteSpace(homeName) || string.IsNullOrWhiteSpace(awayName))
            {
                continue;
            }

            var startTime = DateTimeOffset.TryParse(ev.Date, out var parsedDate)
                ? parsedDate
                : DateTimeOffset.UtcNow.AddHours(2);

            var statusDto = competition.Status ?? ev.Status;
            var status = MapEventStatus(statusDto);

            int? homeScore = int.TryParse(home?.Score, out var hs) ? hs : null;
            int? awayScore = int.TryParse(away?.Score, out var aws) ? aws : null;

            var (homeOdds, drawOdds, awayOdds) = ExtractOrCalculate1X2Odds(
                ev.Id,
                competition.Odds?.FirstOrDefault(),
                status,
                homeScore,
                awayScore);

            var isSuspended = status is EventStatus.Final or EventStatus.Postponed or EventStatus.Canceled;

            results.Add(new ExternalMatchSnapshot(
                ExternalId: $"espn:{ev.Id}",
                Sport: "Soccer",
                League: leagueName,
                HomeTeam: homeName,
                AwayTeam: awayName,
                StartTime: startTime,
                Status: status,
                HomeScore: status is EventStatus.Live or EventStatus.Final ? (homeScore ?? 0) : null,
                AwayScore: status is EventStatus.Live or EventStatus.Final ? (awayScore ?? 0) : null,
                HomeOdds: homeOdds,
                DrawOdds: drawOdds,
                AwayOdds: awayOdds,
                IsSuspended: isSuspended));
        }

        return results;
    }

    private static EventStatus MapEventStatus(EspnStatusDto? statusDto)
    {
        var typeName = statusDto?.Type?.Name?.ToUpperInvariant() ?? string.Empty;
        var state = statusDto?.Type?.State?.ToLowerInvariant() ?? string.Empty;

        if (typeName.Contains("POSTPONED"))
            return EventStatus.Postponed;

        if (typeName.Contains("CANCELED") || typeName.Contains("ABANDONED"))
            return EventStatus.Canceled;

        return state switch
        {
            "in" => EventStatus.Live,
            "post" => EventStatus.Final,
            _ => EventStatus.Prematch
        };
    }

    private static (decimal Home, decimal Draw, decimal Away) ExtractOrCalculate1X2Odds(
        string externalId,
        EspnOddsDto? espnOdds,
        EventStatus status,
        int? homeScore,
        int? awayScore)
    {
        var homeMl = espnOdds?.HomeTeamOdds?.MoneyLine;
        var awayMl = espnOdds?.AwayTeamOdds?.MoneyLine;
        var drawMl = espnOdds?.DrawOdds?.MoneyLine;

        if (homeMl.HasValue && awayMl.HasValue && homeMl.Value != 0m && awayMl.Value != 0m)
        {
            var homeDec = MoneyLineToDecimal(homeMl.Value);
            var awayDec = MoneyLineToDecimal(awayMl.Value);
            var drawDec = drawMl.HasValue && drawMl.Value != 0m
                ? MoneyLineToDecimal(drawMl.Value)
                : (espnOdds?.DrawOdds?.Value is decimal dv and >= 1.05m
                    ? Math.Round(dv, 2, MidpointRounding.AwayFromZero)
                    : 3.30m);

            return (homeDec, drawDec, awayDec);
        }

        // Gdy ESPN nie zwraca kursów dla rynku europejskiego, wyliczamy deterministyczne kursy 1X2 (typ decimal)
        unchecked
        {
            int hash = 17;
            foreach (var ch in externalId)
            {
                hash = (hash * 31) + ch;
            }

            var seed = Math.Abs(hash % 100);
            decimal homeProb = 0.34m + ((seed % 25) * 0.008m); // 0.34 - 0.54
            decimal drawProb = 0.26m + ((seed % 7) * 0.006m);  // 0.26 - 0.30
            decimal awayProb = 1.06m - homeProb - drawProb;    // marża bukmacherska ~6%

            if (status == EventStatus.Live && homeScore.HasValue && awayScore.HasValue)
            {
                var diff = homeScore.Value - awayScore.Value;
                if (diff > 0)
                {
                    homeProb = Math.Min(0.82m, homeProb + (diff * 0.18m));
                    drawProb = Math.Max(0.10m, drawProb - (diff * 0.08m));
                    awayProb = Math.Max(0.06m, 1.06m - homeProb - drawProb);
                }
                else if (diff < 0)
                {
                    var absDiff = Math.Abs(diff);
                    awayProb = Math.Min(0.82m, awayProb + (absDiff * 0.18m));
                    drawProb = Math.Max(0.10m, drawProb - (absDiff * 0.08m));
                    homeProb = Math.Max(0.06m, 1.06m - awayProb - drawProb);
                }
            }

            var homeOdds = ClampOdds(1.00m / homeProb);
            var drawOdds = ClampOdds(1.00m / drawProb);
            var awayOdds = ClampOdds(1.00m / awayProb);

            return (homeOdds, drawOdds, awayOdds);
        }
    }

    private static decimal MoneyLineToDecimal(decimal moneyLine)
    {
        decimal raw = moneyLine > 0m
            ? 1.00m + (moneyLine / 100.00m)
            : 1.00m + (100.00m / Math.Abs(moneyLine));

        return ClampOdds(raw);
    }

    private static decimal ClampOdds(decimal value)
    {
        var clamped = Math.Clamp(value, 1.05m, 25.00m);
        return Math.Round(clamped, 2, MidpointRounding.AwayFromZero);
    }
}
