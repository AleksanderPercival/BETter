using System.Text.Json.Serialization;

namespace BETter.Infrastructure.External.Espn;

public sealed class EspnScoreboardResponse
{
    [JsonPropertyName("leagues")]
    public List<EspnLeagueDto>? Leagues { get; set; }

    [JsonPropertyName("events")]
    public List<EspnEventDto>? Events { get; set; }
}

public sealed class EspnLeagueDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("abbreviation")]
    public string? Abbreviation { get; set; }
}

public sealed class EspnEventDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public EspnStatusDto? Status { get; set; }

    [JsonPropertyName("competitions")]
    public List<EspnCompetitionDto>? Competitions { get; set; }
}

public sealed class EspnCompetitionDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public EspnStatusDto? Status { get; set; }

    [JsonPropertyName("competitors")]
    public List<EspnCompetitorDto>? Competitors { get; set; }

    [JsonPropertyName("odds")]
    public List<EspnOddsDto>? Odds { get; set; }
}

public sealed class EspnCompetitorDto
{
    [JsonPropertyName("homeAway")]
    public string? HomeAway { get; set; }

    [JsonPropertyName("score")]
    public string? Score { get; set; }

    [JsonPropertyName("team")]
    public EspnTeamDto? Team { get; set; }
}

public sealed class EspnTeamDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("shortDisplayName")]
    public string? ShortDisplayName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class EspnStatusDto
{
    [JsonPropertyName("displayClock")]
    public string? DisplayClock { get; set; }

    [JsonPropertyName("type")]
    public EspnStatusTypeDto? Type { get; set; }
}

public sealed class EspnStatusTypeDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("completed")]
    public bool? Completed { get; set; }

    [JsonPropertyName("shortDetail")]
    public string? ShortDetail { get; set; }
}

public sealed class EspnOddsDto
{
    [JsonPropertyName("homeTeamOdds")]
    public EspnTeamOddsDto? HomeTeamOdds { get; set; }

    [JsonPropertyName("awayTeamOdds")]
    public EspnTeamOddsDto? AwayTeamOdds { get; set; }

    [JsonPropertyName("drawOdds")]
    public EspnDrawOddsDto? DrawOdds { get; set; }
}

public sealed class EspnTeamOddsDto
{
    [JsonPropertyName("moneyLine")]
    public decimal? MoneyLine { get; set; }
}

public sealed class EspnDrawOddsDto
{
    [JsonPropertyName("moneyLine")]
    public decimal? MoneyLine { get; set; }

    [JsonPropertyName("value")]
    public decimal? Value { get; set; }
}
