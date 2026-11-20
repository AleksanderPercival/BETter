using BETter.Domain.Enums;
using BETter.Domain.Exceptions;

namespace BETter.Domain.Entities;

public class SportEvent
{
    private readonly List<Market> _markets = [];

    public Guid Id { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public string Sport { get; private set; } = string.Empty;
    public string League { get; private set; } = string.Empty;
    public string HomeTeam { get; private set; } = string.Empty;
    public string AwayTeam { get; private set; } = string.Empty;
    public DateTimeOffset StartTime { get; private set; }
    public EventStatus Status { get; private set; }
    public int? HomeScore { get; private set; }
    public int? AwayScore { get; private set; }

    public IReadOnlyCollection<Market> Markets => _markets.AsReadOnly();

    private SportEvent() { }

    public SportEvent(
        Guid id,
        string externalId,
        string sport,
        string league,
        string homeTeam,
        string awayTeam,
        DateTimeOffset startTime,
        EventStatus status = EventStatus.Prematch)
    {
        if (id == Guid.Empty)
            throw new DomainException("SportEvent Id cannot be empty.");
        if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
            throw new DomainException("HomeTeam and AwayTeam are required.");

        Id = id;
        ExternalId = externalId;
        Sport = sport;
        League = league;
        HomeTeam = homeTeam;
        AwayTeam = awayTeam;
        StartTime = startTime;
        Status = status;
    }

    public string DisplayName => $"{HomeTeam} vs {AwayTeam}";

    public void AddMarket(Market market)
    {
        ArgumentNullException.ThrowIfNull(market);
        if (market.SportEventId != Id)
            throw new DomainException("Market does not belong to this SportEvent.");

        _markets.Add(market);
    }

    public void UpdateScore(int homeScore, int awayScore)
    {
        if (homeScore < 0 || awayScore < 0)
            throw new DomainException("Scores cannot be negative.");

        HomeScore = homeScore;
        AwayScore = awayScore;
    }

    public void ChangeStatus(EventStatus newStatus)
    {
        Status = newStatus;

        if (newStatus is EventStatus.Final or EventStatus.Postponed or EventStatus.Canceled)
        {
            SuspendAllMarkets();
        }
    }

    public void SuspendAllMarkets()
    {
        foreach (var market in _markets)
        {
            market.Suspend();
        }
    }

    public void ResumeAllMarkets()
    {
        if (Status is EventStatus.Final or EventStatus.Postponed or EventStatus.Canceled)
            throw new DomainException("Cannot resume markets for a finished or canceled event.");

        foreach (var market in _markets)
        {
            market.Resume();
        }
    }
}
