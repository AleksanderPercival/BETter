namespace BETter.Infrastructure.External.Espn;

public sealed class EspnOptions
{
    public const string SectionName = "Espn";

    public string BaseUrl { get; set; } = "https://site.api.espn.com/";
    public int TimeoutSeconds { get; set; } = 8;
    public int SyncIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Lista kodów lig piłkarskich w ESPN Site API v2 (np. ita.1 = Serie A).
    /// </summary>
    public List<EspnLeagueConfig> Leagues { get; set; } =
    [
        new() { Slug = "ita.1", DisplayName = "Serie A" },
        new() { Slug = "eng.1", DisplayName = "Premier League" },
        new() { Slug = "esp.1", DisplayName = "LaLiga" },
        new() { Slug = "ger.1", DisplayName = "Bundesliga" },
        new() { Slug = "uefa.champions", DisplayName = "UEFA Champions League" }
    ];
}

public sealed class EspnLeagueConfig
{
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
