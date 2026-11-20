namespace BETter.Domain.Ports;

public interface IWalletPort
{
    Task<bool> HoldStakeAsync(Guid userId, Guid betSlipId, decimal amount, CancellationToken cancellationToken = default);
    Task SettleWinAsync(Guid userId, Guid betSlipId, decimal stakeAmount, decimal payoutAmount, CancellationToken cancellationToken = default);
    Task SettleLossAsync(Guid userId, Guid betSlipId, decimal stakeAmount, CancellationToken cancellationToken = default);
    Task RefundVoidAsync(Guid userId, Guid betSlipId, decimal stakeAmount, CancellationToken cancellationToken = default);
    Task CashOutAsync(Guid userId, Guid betSlipId, decimal stakeAmount, decimal cashOutAmount, CancellationToken cancellationToken = default);
}
