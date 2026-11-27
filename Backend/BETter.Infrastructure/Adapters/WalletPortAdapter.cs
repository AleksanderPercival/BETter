using BETter.Domain.Ports;
using BETter.Infrastructure.Repositories;

namespace BETter.Infrastructure.Adapters;

/// <summary>
/// Adapter implementujący port wyjściowy IWalletPort z warstwy BETter.Domain.
/// Po wdrożeniu BETter.Wallet (WalletService) może zostać zastąpiony bez zmian w domenie i API.
/// </summary>
public class WalletPortAdapter : IWalletPort
{
    private readonly WalletRepository _walletRepository;

    public WalletPortAdapter(WalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public Task<bool> HoldStakeAsync(
        Guid userId,
        Guid betSlipId,
        decimal amount,
        CancellationToken cancellationToken = default) =>
        _walletRepository.HoldStakeAsync(userId, betSlipId, amount, cancellationToken);

    public Task SettleWinAsync(
        Guid userId,
        Guid betSlipId,
        decimal stakeAmount,
        decimal payoutAmount,
        CancellationToken cancellationToken = default) =>
        _walletRepository.SettleWinAsync(userId, betSlipId, stakeAmount, payoutAmount, cancellationToken);

    public Task SettleLossAsync(
        Guid userId,
        Guid betSlipId,
        decimal stakeAmount,
        CancellationToken cancellationToken = default) =>
        _walletRepository.SettleLossAsync(userId, betSlipId, stakeAmount, cancellationToken);

    public Task RefundVoidAsync(
        Guid userId,
        Guid betSlipId,
        decimal stakeAmount,
        CancellationToken cancellationToken = default) =>
        _walletRepository.RefundVoidAsync(userId, betSlipId, stakeAmount, cancellationToken);

    public Task CashOutAsync(
        Guid userId,
        Guid betSlipId,
        decimal stakeAmount,
        decimal cashOutAmount,
        CancellationToken cancellationToken = default) =>
        _walletRepository.CashOutAsync(userId, betSlipId, stakeAmount, cashOutAmount, cancellationToken);
}
