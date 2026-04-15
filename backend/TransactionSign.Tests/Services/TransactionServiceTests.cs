using NSubstitute;
using TransactionSign.Application.Interfaces;
using TransactionSign.Application.Services;
using TransactionSign.Domain.Entities;
using TransactionSign.Domain.Enums;
using Xunit;

namespace TransactionSign.Tests.Services;

/// <summary>
/// Core unit tests for TransactionService.
/// Tests essential business logic paths without redundancy.
/// </summary>
public class TransactionServiceTests
{
    private readonly ITransactionRepository _repository;
    private readonly TransactionService _service;

    public TransactionServiceTests()
    {
        _repository = Substitute.For<ITransactionRepository>();
        _service = new TransactionService(_repository);
    }

    #region GetPendingTransactionsAsync

    [Fact]
    public async Task GetPendingTransactions_ReturnsCorrectSignatureInfoAndCanSign()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(1, 1000m),
            CreateTransaction(2, 2000m)
        };

        _repository.GetPendingTransactionsAsync().Returns(transactions);
        _repository.GetRequiredSignaturesAsync().Returns(3);
        _repository.GetSignatureCountAsync(1).Returns(1);
        _repository.GetSignatureCountAsync(2).Returns(2);
        _repository.HasUserSignedAsync(1, 1).Returns(false);
        _repository.HasUserSignedAsync(2, 1).Returns(true); // User already signed tx 2

        // Act
        var result = await _service.GetPendingTransactionsAsync(userId: 1);

        // Assert
        Assert.Equal(2, result.Count);

        var first = result.First(t => t.Id == 1);
        Assert.Equal(1, first.SignatureCount);
        Assert.True(first.CanSign);

        var second = result.First(t => t.Id == 2);
        Assert.Equal(2, second.SignatureCount);
        Assert.False(second.CanSign); // Already signed
    }

    #endregion

    #region SignTransactionsAsync - Validation

    [Fact]
    public async Task Sign_ReturnsError_WhenTransactionNotFound()
    {
        _repository.GetByIdAsync(999).Returns((Transaction?)null);

        var result = await _service.SignTransactionsAsync([999], userId: 1);

        Assert.Single(result);
        Assert.False(result[0].Success);
        Assert.Equal("Transaction not found", result[0].Error);
    }

    [Fact]
    public async Task Sign_ReturnsError_WhenTransactionNotEligible()
    {
        var transaction = CreateTransaction(1, 1000m);
        transaction.Status = TransactionStatus.Completed;
        _repository.GetByIdAsync(1).Returns(transaction);

        var result = await _service.SignTransactionsAsync([1], userId: 1);

        Assert.False(result[0].Success);
        Assert.Equal("Transaction not eligible for signing, Already Finalized!", result[0].Error);
    }

    [Fact]
    public async Task Sign_ReturnsError_WhenUserAlreadySigned()
    {
        var transaction = CreateTransaction(1, 1000m);
        _repository.GetByIdAsync(1).Returns(transaction);
        _repository.HasUserSignedAsync(1, 1).Returns(true);

        var result = await _service.SignTransactionsAsync([1], userId: 1);

        Assert.False(result[0].Success);
        Assert.Equal("Already signed", result[0].Error);
    }

    #endregion

    #region SignTransactionsAsync - Success Paths

    [Fact]
    public async Task Sign_AddsSignature_WhenValid()
    {
        var transaction = CreateTransaction(1, 1000m);
        _repository.GetByIdAsync(1).Returns(transaction);
        _repository.HasUserSignedAsync(1, 1).Returns(false);
        _repository.GetSignatureCountAsync(1).Returns(1);
        _repository.GetRequiredSignaturesAsync().Returns(3);

        var result = await _service.SignTransactionsAsync([1], userId: 1);

        Assert.True(result[0].Success);
        await _repository.Received(1).AddSignatureAsync(
            Arg.Is<Signature>(s => s.TransactionId == 1 && s.UserId == 1));
    }

    [Fact]
    public async Task Sign_TriggersFinalization_WhenThresholdReached()
    {
        var transaction = CreateTransaction(1, 10000m);
        _repository.GetByIdAsync(1).Returns(transaction);
        _repository.HasUserSignedAsync(1, 1).Returns(false);
        _repository.GetSignatureCountAsync(1).Returns(3);
        _repository.GetRequiredSignaturesAsync().Returns(3);
        _repository.TryFinalizeTransactionAsync(transaction, Arg.Any<decimal>(), Arg.Any<decimal>()).Returns(true);

        var result = await _service.SignTransactionsAsync([1], userId: 1);

        Assert.True(result[0].Success);
        await _repository.Received(1).TryFinalizeTransactionAsync(
            transaction, Arg.Any<decimal>(), Arg.Any<decimal>());
    }

    [Fact]
    public async Task Sign_SkipsFinalization_WhenAnotherProcessFinalizedFirst()
    {
        var transaction = CreateTransaction(1, 10000m);
        _repository.GetByIdAsync(1).Returns(transaction);
        _repository.HasUserSignedAsync(1, 1).Returns(false);
        _repository.GetSignatureCountAsync(1).Returns(3);
        _repository.GetRequiredSignaturesAsync().Returns(3);
        _repository.TryFinalizeTransactionAsync(transaction, Arg.Any<decimal>(), Arg.Any<decimal>()).Returns(false);

        var result = await _service.SignTransactionsAsync([1], userId: 1);

        // Signing succeeds, finalization was already done by another process
        Assert.True(result[0].Success);
    }

    #endregion

    #region Fee Calculation

    [Theory]
    [InlineData(5000, 50)]       // Tier 1 (0.3%) clamped to min
    [InlineData(30000, 60)]      // Tier 2 (0.2%): 30000 * 0.002 = 60
    [InlineData(100000, 100)]    // Tier 3 (0.1%): 100000 * 0.001 = 100
    [InlineData(2000000, 1800)]  // Clamped to max
    public async Task Sign_CalculatesFeeCorrectly(decimal amount, decimal expectedFee)
    {
        var transaction = CreateTransaction(1, amount);
        _repository.GetByIdAsync(1).Returns(transaction);
        _repository.HasUserSignedAsync(1, 1).Returns(false);
        _repository.GetSignatureCountAsync(1).Returns(3);
        _repository.GetRequiredSignaturesAsync().Returns(3);

        decimal capturedFee = 0;
        _repository.TryFinalizeTransactionAsync(
            Arg.Any<Transaction>(),
            Arg.Do<decimal>(f => capturedFee = f),
            Arg.Any<decimal>()).Returns(true);

        await _service.SignTransactionsAsync([1], userId: 1);

        Assert.Equal(expectedFee, capturedFee);
    }

    [Theory]
    [InlineData(100000, 5)]   // Fee=100, Settlement=100*0.05=5
    [InlineData(2000000, 90)] // Fee=1800 (clamped), Settlement=1800*0.05=90
    public async Task Sign_CalculatesSettlementAs5PercentOfFee(decimal amount, decimal expectedSettlement)
    {
        var transaction = CreateTransaction(1, amount);
        _repository.GetByIdAsync(1).Returns(transaction);
        _repository.HasUserSignedAsync(1, 1).Returns(false);
        _repository.GetSignatureCountAsync(1).Returns(3);
        _repository.GetRequiredSignaturesAsync().Returns(3);

        decimal capturedSettlement = 0;
        _repository.TryFinalizeTransactionAsync(
            Arg.Any<Transaction>(),
            Arg.Any<decimal>(),
            Arg.Do<decimal>(s => capturedSettlement = s)).Returns(true);

        await _service.SignTransactionsAsync([1], userId: 1);

        Assert.Equal(expectedSettlement, capturedSettlement);
    }

    #endregion

    #region Helper

    private static Transaction CreateTransaction(int id, decimal amount) => new()
    {
        Id = id,
        Type = "Payment",
        ValueDate = DateTime.UtcNow,
        LastModifiedDate = DateTime.UtcNow,
        Reason = $"Test {id}",
        Company = "Test Co",
        Counterparty = "Counter Co",
        Amount = amount,
        Status = TransactionStatus.Internal,
        InternalStatus = InternalStatus.ToSign
    };

    #endregion
}
