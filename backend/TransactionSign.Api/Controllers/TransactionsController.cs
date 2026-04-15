using Microsoft.AspNetCore.Mvc;
using TransactionSign.Application.DTOs;
using TransactionSign.Application.Services;

namespace TransactionSign.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;

    public TransactionsController(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<List<TransactionDto>>> GetPending([FromHeader(Name = "X-User-Id")] int userId = 1)
    {
        List<TransactionDto> transactions = await _transactionService.GetPendingTransactionsAsync(userId);
        return Ok(transactions);
    }

    [HttpGet("completed")]
    public async Task<ActionResult<List<CompletedTransactionDto>>> GetCompleted()
    {
        List<CompletedTransactionDto> transactions = await _transactionService.GetCompletedTransactionsAsync();
        return Ok(transactions);
    }

    [HttpPost("sign")]
    public async Task<ActionResult<List<SignResult>>> Sign([FromBody] SignTransactionsRequest request, [FromHeader(Name = "X-User-Id")] int userId = 1)
    {
        List<SignResult> results = await _transactionService.SignTransactionsAsync(request.TransactionIds, userId);
        return Ok(results);
    }

    [HttpPost("{id}/sign")]
    public async Task<ActionResult<SignResult>> SignSingle(int id, [FromHeader(Name = "X-User-Id")] int userId = 1)
    {
        List<SignResult> results = await _transactionService.SignTransactionsAsync([id], userId);
        return Ok(results.First());
    }

    [HttpPost("restore-all")]
    public async Task<ActionResult> RestoreAll()
    {
        await _transactionService.RestoreAllTransactionsAsync();
        return Ok();
    }
}
