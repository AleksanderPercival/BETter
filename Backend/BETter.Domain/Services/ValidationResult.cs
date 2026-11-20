namespace BETter.Domain.Services;

public sealed record ValidationResult(
    bool IsValid,
    string? ErrorMessage = null,
    IReadOnlyCollection<Guid>? ConflictingSportEventIds = null)
{
    public static ValidationResult Success() => new(true);

    public static ValidationResult Failure(
        string errorMessage,
        IReadOnlyCollection<Guid>? conflictingSportEventIds = null) =>
        new(false, errorMessage, conflictingSportEventIds);
}
