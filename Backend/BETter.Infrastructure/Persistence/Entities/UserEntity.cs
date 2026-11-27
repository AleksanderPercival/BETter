namespace BETter.Infrastructure.Persistence.Entities;

public class UserEntity
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdultConfirmed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public WalletEntity? Wallet { get; set; }
}
