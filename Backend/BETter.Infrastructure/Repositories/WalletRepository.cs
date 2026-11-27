using BETter.Domain.Exceptions;
using BETter.Infrastructure.Persistence;
using BETter.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BETter.Infrastructure.Repositories;

public sealed record WalletSnapshot(
    Guid UserId,
    Guid WalletId,
    decimal AvailableBalance,
    decimal FrozenBalance,
    decimal BonusBalance);

/// <summary>
/// Adapter repozytorium portfela dla PostgreSQL (EF Core).
/// Zapewnia atomowość subkont, zapis w Ledgerze (PU-05) i kontrolę współbieżności (WNF-02).
/// </summary>
public class WalletRepository
{
    public static readonly Guid DemoUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public const decimal DefaultInitialAvailableBalance = 1000.00m;
    public const decimal DefaultInitialBonusBalance = 50.00m;

    private readonly BetterDbContext _dbContext;

    public WalletRepository(BetterDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WalletEntity> GetOrCreateByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var effectiveUserId = userId == Guid.Empty ? DemoUserId : userId;

        var wallet = await _dbContext.Wallets
            .Include(w => w.BettingLimits)
            .FirstOrDefaultAsync(w => w.UserId == effectiveUserId, cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        var now = DateTimeOffset.UtcNow;
        var userExists = await _dbContext.Users.AnyAsync(u => u.Id == effectiveUserId, cancellationToken);
        if (!userExists)
        {
            var isDemo = effectiveUserId == DemoUserId;
            var user = new UserEntity
            {
                Id = effectiveUserId,
                Username = isDemo ? "demo.player" : $"player-{effectiveUserId.ToString()[..8]}",
                Email = isDemo ? "demo@better.local" : $"{effectiveUserId:N}@better.local",
                PasswordHash = "PROTOTYPE_HASH_18_PLUS",
                IsAdultConfirmed = true,
                CreatedAt = now
            };
            await _dbContext.Users.AddAsync(user, cancellationToken);
        }

        var walletId = Guid.NewGuid();
        wallet = new WalletEntity
        {
            Id = walletId,
            UserId = effectiveUserId,
            AvailableBalance = effectiveUserId == DemoUserId ? DefaultInitialAvailableBalance : 0.00m,
            FrozenBalance = 0.00m,
            BonusBalance = effectiveUserId == DemoUserId ? DefaultInitialBonusBalance : 0.00m,
            UpdatedAt = now,
            Version = 1,
            BettingLimits = new BettingLimitsEntity
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                DailyDepositLimit = 50_000.00m,
                SingleDepositMaxLimit = 10_000.00m,
                MaxSingleStakeLimit = 10_000.00m,
                UpdatedAt = now
            }
        };

        await _dbContext.Wallets.AddAsync(wallet, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return wallet;
    }

    public async Task<WalletSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var wallet = await GetOrCreateByUserIdAsync(userId, cancellationToken);
        return ToSnapshot(wallet);
    }

    public async Task<WalletSnapshot> DepositAsync(Guid userId, decimal amount, CancellationToken cancellationToken = default)
    {
        var roundedAmount = ValidatePositiveAmount(amount, "Deposit amount");

        return await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            var wallet = await GetOrCreateByUserIdAsync(userId, ct);

            if (wallet.BettingLimits?.SingleDepositMaxLimit is decimal maxSingleDeposit && roundedAmount > maxSingleDeposit)
            {
                throw new DomainException(
                    $"Deposit amount ({roundedAmount:F2} PLN) exceeds responsible gaming single deposit limit ({maxSingleDeposit:F2} PLN).");
            }

            if (wallet.BettingLimits?.DailyDepositLimit is decimal dailyLimit)
            {
                var startOfDayUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
                var depositedTodayEntries = await _dbContext.LedgerEntries
                    .Where(le => le.WalletId == wallet.Id
                              && le.Type == WalletTransactionType.Deposit
                              && le.CreatedAt >= startOfDayUtc)
                    .Select(le => le.Amount)
                    .ToListAsync(ct);

                var depositedToday = depositedTodayEntries.Sum();
                if (depositedToday + roundedAmount > dailyLimit)
                {
                    throw new DomainException(
                        $"Deposit violates daily responsible gaming limit ({dailyLimit:F2} PLN).");
                }
            }

            wallet.AvailableBalance = Math.Round(wallet.AvailableBalance + roundedAmount, 2, MidpointRounding.AwayFromZero);
            wallet.UpdatedAt = DateTimeOffset.UtcNow;
            wallet.Version++;

            await AppendLedgerEntryAsync(
                wallet,
                betSlipId: null,
                WalletTransactionType.Deposit,
                roundedAmount,
                $"Deposit +{roundedAmount:F2} PLN",
                ct);

            await _dbContext.SaveChangesAsync(ct);
            return ToSnapshot(wallet);
        }, cancellationToken);
    }

    public async Task<WalletSnapshot> WithdrawAsync(Guid userId, decimal amount, CancellationToken cancellationToken = default)
    {
        var roundedAmount = ValidatePositiveAmount(amount, "Withdrawal amount");

        return await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            var wallet = await GetOrCreateByUserIdAsync(userId, ct);

            // PU-03 / 3a: Izolacja subkont - środki zamrożone i bonusowe nie podlegają wypłacie.
            if (roundedAmount > wallet.AvailableBalance)
            {
                throw new DomainException(
                    $"Insufficient available deposit balance ({wallet.AvailableBalance:F2} PLN). Frozen and bonus funds cannot be withdrawn.");
            }

            wallet.AvailableBalance = Math.Round(wallet.AvailableBalance - roundedAmount, 2, MidpointRounding.AwayFromZero);
            wallet.UpdatedAt = DateTimeOffset.UtcNow;
            wallet.Version++;

            await AppendLedgerEntryAsync(
                wallet,
                betSlipId: null,
                WalletTransactionType.Withdrawal,
                roundedAmount,
                $"Withdrawal -{roundedAmount:F2} PLN",
                ct);

            await _dbContext.SaveChangesAsync(ct);
            return ToSnapshot(wallet);
        }, cancellationToken);
    }

    public async Task<bool> HoldStakeAsync(
        Guid userId,
        Guid betSlipId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var roundedAmount = ValidatePositiveAmount(amount, "Hold stake amount");
        var wallet = await GetOrCreateByUserIdAsync(userId, cancellationToken);

        if (wallet.BettingLimits?.MaxSingleStakeLimit is decimal maxStake && roundedAmount > maxStake)
        {
            throw new DomainException(
                $"Stake ({roundedAmount:F2} PLN) exceeds responsible gaming max single stake ({maxStake:F2} PLN).");
        }

        if (wallet.AvailableBalance < roundedAmount)
        {
            return false;
        }

        wallet.AvailableBalance = Math.Round(wallet.AvailableBalance - roundedAmount, 2, MidpointRounding.AwayFromZero);
        wallet.FrozenBalance = Math.Round(wallet.FrozenBalance + roundedAmount, 2, MidpointRounding.AwayFromZero);
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
        wallet.Version++;

        await AppendLedgerEntryAsync(
            wallet,
            betSlipId,
            WalletTransactionType.Hold,
            roundedAmount,
            $"Pre-authorization (Hold) for BetSlip {betSlipId}",
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SettleWinAsync(
        Guid userId,
        Guid betSlipId,
        decimal stakeAmount,
        decimal payoutAmount,
        CancellationToken cancellationToken = default)
    {
        var roundedStake = ValidatePositiveAmount(stakeAmount, "Stake amount");
        var roundedPayout = ValidatePositiveAmount(payoutAmount, "Payout amount");
        var wallet = await GetOrCreateByUserIdAsync(userId, cancellationToken);

        EnsureSufficientFrozenBalance(wallet, roundedStake);

        wallet.FrozenBalance = Math.Round(wallet.FrozenBalance - roundedStake, 2, MidpointRounding.AwayFromZero);
        wallet.AvailableBalance = Math.Round(wallet.AvailableBalance + roundedPayout, 2, MidpointRounding.AwayFromZero);
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
        wallet.Version++;

        await AppendLedgerEntryAsync(
            wallet,
            betSlipId,
            WalletTransactionType.Payout,
            roundedPayout,
            $"Winning BetSlip {betSlipId} payout +{roundedPayout:F2} PLN",
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SettleLossAsync(
        Guid userId,
        Guid betSlipId,
        decimal stakeAmount,
        CancellationToken cancellationToken = default)
    {
        var roundedStake = ValidatePositiveAmount(stakeAmount, "Stake amount");
        var wallet = await GetOrCreateByUserIdAsync(userId, cancellationToken);

        EnsureSufficientFrozenBalance(wallet, roundedStake);

        wallet.FrozenBalance = Math.Round(wallet.FrozenBalance - roundedStake, 2, MidpointRounding.AwayFromZero);
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
        wallet.Version++;

        await AppendLedgerEntryAsync(
            wallet,
            betSlipId,
            WalletTransactionType.SettleLoss,
            roundedStake,
            $"Lost BetSlip {betSlipId} stake settled (-{roundedStake:F2} PLN from Frozen)",
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RefundVoidAsync(
        Guid userId,
        Guid betSlipId,
        decimal stakeAmount,
        CancellationToken cancellationToken = default)
    {
        var roundedStake = ValidatePositiveAmount(stakeAmount, "Stake amount");
        var wallet = await GetOrCreateByUserIdAsync(userId, cancellationToken);

        EnsureSufficientFrozenBalance(wallet, roundedStake);

        wallet.FrozenBalance = Math.Round(wallet.FrozenBalance - roundedStake, 2, MidpointRounding.AwayFromZero);
        wallet.AvailableBalance = Math.Round(wallet.AvailableBalance + roundedStake, 2, MidpointRounding.AwayFromZero);
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
        wallet.Version++;

        await AppendLedgerEntryAsync(
            wallet,
            betSlipId,
            WalletTransactionType.RefundVoid,
            roundedStake,
            $"Void BetSlip {betSlipId} stake refunded (+{roundedStake:F2} PLN)",
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CashOutAsync(
        Guid userId,
        Guid betSlipId,
        decimal stakeAmount,
        decimal cashOutAmount,
        CancellationToken cancellationToken = default)
    {
        var roundedStake = ValidatePositiveAmount(stakeAmount, "Stake amount");
        var roundedCashOut = ValidatePositiveAmount(cashOutAmount, "Cash-out amount");
        var wallet = await GetOrCreateByUserIdAsync(userId, cancellationToken);

        EnsureSufficientFrozenBalance(wallet, roundedStake);

        wallet.FrozenBalance = Math.Round(wallet.FrozenBalance - roundedStake, 2, MidpointRounding.AwayFromZero);
        wallet.AvailableBalance = Math.Round(wallet.AvailableBalance + roundedCashOut, 2, MidpointRounding.AwayFromZero);
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
        wallet.Version++;

        await AppendLedgerEntryAsync(
            wallet,
            betSlipId,
            WalletTransactionType.Cashout,
            roundedCashOut,
            $"Early Cash-out for BetSlip {betSlipId} (+{roundedCashOut:F2} PLN)",
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AppendLedgerEntryAsync(
        WalletEntity wallet,
        Guid? betSlipId,
        WalletTransactionType type,
        decimal amount,
        string description,
        CancellationToken cancellationToken)
    {
        var entry = new LedgerEntryEntity
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            BetSlipId = betSlipId,
            Type = type,
            Amount = amount,
            AvailableBalanceAfter = wallet.AvailableBalance,
            FrozenBalanceAfter = wallet.FrozenBalance,
            BonusBalanceAfter = wallet.BonusBalance,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _dbContext.LedgerEntries.AddAsync(entry, cancellationToken);
    }

    private static decimal ValidatePositiveAmount(decimal amount, string fieldName)
    {
        if (amount <= 0m)
        {
            throw new DomainException($"{fieldName} must be greater than zero.");
        }

        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static void EnsureSufficientFrozenBalance(WalletEntity wallet, decimal stakeAmount)
    {
        if (wallet.FrozenBalance < stakeAmount)
        {
            throw new DomainException(
                $"Frozen balance ({wallet.FrozenBalance:F2} PLN) is lower than required stake ({stakeAmount:F2} PLN).");
        }
    }

    private static WalletSnapshot ToSnapshot(WalletEntity wallet) =>
        new(
            wallet.UserId,
            wallet.Id,
            wallet.AvailableBalance,
            wallet.FrozenBalance,
            wallet.BonusBalance);
}
