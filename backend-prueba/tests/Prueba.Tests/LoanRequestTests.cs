using Prueba.Domain.Entities;
using Prueba.Domain.Enums;
using Prueba.Domain.Exceptions;

namespace Prueba.Tests;

public class LoanRequestTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public void Create_ValidAmount_StartsPending()
    {
        var loan = LoanRequest.Create(UserId, 500_000m, 12);

        Assert.Equal(LoanStatus.Pending, loan.Status);
        Assert.Equal(500_000m, loan.Amount);
        Assert.Null(loan.ResolvedAt);
        Assert.Null(loan.ResolvedByUserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99_999)]
    [InlineData(50_000_001)]
    public void Create_AmountOutOfRange_Throws(decimal amount) =>
        Assert.Throws<DomainException>(() => LoanRequest.Create(UserId, amount, 12));

    [Theory]
    [InlineData(100_000)]
    [InlineData(50_000_000)]
    public void Create_AmountAtLimits_IsAllowed(decimal amount) =>
        Assert.Equal(amount, LoanRequest.Create(UserId, amount, 12).Amount);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(61)]
    public void Create_TermOutOfRange_Throws(int term) =>
        Assert.Throws<DomainException>(() => LoanRequest.Create(UserId, 500_000m, term));

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    public void Create_TermAtLimits_IsAllowed(int term) =>
        Assert.Equal(term, LoanRequest.Create(UserId, 500_000m, term).TermMonths);

    [Fact]
    public void Update_TermOutOfRange_ThrowsAndKeepsValues()
    {
        var loan = LoanRequest.Create(UserId, 500_000m, 12);

        Assert.Throws<DomainException>(() => loan.Update(900_000m, 99));

        Assert.Equal(500_000m, loan.Amount);
        Assert.Equal(12, loan.TermMonths);
    }

    [Fact]
    public void Approve_SetsStatusAndAudit()
    {
        var loan = LoanRequest.Create(UserId, 500_000m, 12);

        loan.Approve(AdminId);

        Assert.Equal(LoanStatus.Approved, loan.Status);
        Assert.Equal(AdminId, loan.ResolvedByUserId);
        Assert.NotNull(loan.ResolvedAt);
    }

    [Fact]
    public void Reject_SetsStatusAndAudit()
    {
        var loan = LoanRequest.Create(UserId, 500_000m, 12);

        loan.Reject(AdminId);

        Assert.Equal(LoanStatus.Rejected, loan.Status);
        Assert.Equal(AdminId, loan.ResolvedByUserId);
    }

    [Fact]
    public void Approve_AlreadyApproved_ThrowsConflict()
    {
        var loan = LoanRequest.Create(UserId, 500_000m, 12);
        loan.Approve(AdminId);

        Assert.Throws<ConflictException>(() => loan.Approve(AdminId));
    }

    [Fact]
    public void Reject_AfterApproved_ThrowsConflictAndKeepsStatus()
    {
        var loan = LoanRequest.Create(UserId, 500_000m, 12);
        loan.Approve(AdminId);

        Assert.Throws<ConflictException>(() => loan.Reject(AdminId));
        Assert.Equal(LoanStatus.Approved, loan.Status);
    }

    [Fact]
    public void Update_WhenPending_Changes()
    {
        var loan = LoanRequest.Create(UserId, 500_000m, 12);

        loan.Update(900_000m, 24);

        Assert.Equal(900_000m, loan.Amount);
        Assert.Equal(24, loan.TermMonths);
    }

    [Fact]
    public void Update_WhenResolved_ThrowsConflict()
    {
        var loan = LoanRequest.Create(UserId, 500_000m, 12);
        loan.Reject(AdminId);

        Assert.Throws<ConflictException>(() => loan.Update(900_000m, 24));
    }
}
