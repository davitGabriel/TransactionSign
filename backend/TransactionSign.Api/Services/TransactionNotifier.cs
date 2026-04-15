using Microsoft.AspNetCore.SignalR;
using TransactionSign.Api.Hubs;
using TransactionSign.Application.Interfaces;

namespace TransactionSign.Api.Services;

public class TransactionNotifier : ITransactionNotifier
{
    private readonly IHubContext<TransactionHub> _hubContext;

    public TransactionNotifier(IHubContext<TransactionHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyTransactionSignedAsync(int transactionId, int signatureCount, int requiredSignatures)
    {
        await _hubContext.Clients.All.SendAsync("TransactionSigned", new
        {
            TransactionId = transactionId,
            SignatureCount = signatureCount,
            RequiredSignatures = requiredSignatures,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task NotifyTransactionFinalizedAsync(int transactionId, decimal fee, decimal settlement)
    {
        await _hubContext.Clients.All.SendAsync("TransactionFinalized", new
        {
            TransactionId = transactionId,
            Fee = fee,
            Settlement = settlement,
            Timestamp = DateTime.UtcNow
        });
    }
}
