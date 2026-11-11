using NSubstitute;
using Prueba.Application.Dtos;
using Prueba.Application.Interfaces;
using Prueba.Application.Services;
using Prueba.Domain.Entities;
using Prueba.Domain.Enums;
using Prueba.Domain.Exceptions;

namespace Prueba.Tests;

public class LoanServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly ILoanRepository _loans = Substitute.For<ILoanRepository>();
    private readonly ILoanStatusCache _cache = Substitute.For<ILoanStatusCache>();
    private readonly IUnitOfWork _uow = TestHelpers.PassThroughUnitOfWork();
    private readonly CancellationToken _ct = CancellationToken.None;

    private LoanService Sut(ICurrentUser current) => new(_loans, _uow, _cache, current);
    private LoanService AsUser() => Sut(TestHelpers.CurrentUser(_userId, UserRole.User));
    private LoanService AsAdmin() => Sut(TestHelpers.CurrentUser(_adminId, UserRole.Admin));

    private LoanRequest PendingLoan(Guid? ownerId = null)
    {
        var loan = LoanRequest.Create(ownerId ?? _userId, 500_000m, 12);
        _loans.GetByIdAsync(loan.Id, Arg.Any<CancellationToken>()).Returns(loan);
        return loan;
    }

    // ---------- Crear ----------

    [Fact]
    public async Task Create_AsUser_PersistsPendingLoan()
    {
        LoanRequest? saved = null;
        await _loans.AddAsync(Arg.Do<LoanRequest>(l => saved = l), Arg.Any<CancellationToken>());
        _loans.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ci => saved);

        var dto = await AsUser().CreateAsync(new CreateLoanRequest(750_000m, 18), _ct);

        Assert.Equal(LoanStatus.Pending, dto.Status);
        Assert.Equal(750_000m, dto.Amount);
        Assert.Equal(18, dto.TermMonths);
        Assert.Equal(_userId, saved!.UserId);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_AsAdmin_IsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => AsAdmin().CreateAsync(new CreateLoanRequest(750_000m, 18), _ct));
        await _loans.DidNotReceive().AddAsync(Arg.Any<LoanRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_InvalidTerm_ThrowsAndDoesNotSave()
    {
        await Assert.ThrowsAsync<DomainException>(() => AsUser().CreateAsync(new CreateLoanRequest(750_000m, 0), _ct));
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithTooManyPending_ThrowsDomainException()
    {
        _loans.CountPendingAsync(_userId, Arg.Any<CancellationToken>()).Returns(LoanRequest.MaxPendingPerUser);

        await Assert.ThrowsAsync<DomainException>(() => AsUser().CreateAsync(new CreateLoanRequest(750_000m, 18), _ct));
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_InvalidAmount_ThrowsAndDoesNotSave()
    {
        await Assert.ThrowsAsync<DomainException>(() => AsUser().CreateAsync(new CreateLoanRequest(10m, 18), _ct));
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Aprobar / rechazar (transaccional, solo admin) ----------

    [Fact]
    public async Task Approve_AsAdmin_RunsInTransactionApprovesAndInvalidatesCache()
    {
        var loan = PendingLoan();

        var dto = await AsAdmin().ApproveAsync(loan.Id, _ct);

        Assert.Equal(LoanStatus.Approved, dto.Status);
        Assert.Equal(_adminId, loan.ResolvedByUserId);
        await _uow.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<Task<LoanDto>>>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _cache.Received(1).Remove(loan.Id);
    }

    [Fact]
    public async Task Reject_AsAdmin_RejectsLoan()
    {
        var loan = PendingLoan();

        var dto = await AsAdmin().RejectAsync(loan.Id, _ct);

        Assert.Equal(LoanStatus.Rejected, dto.Status);
        _cache.Received(1).Remove(loan.Id);
    }

    [Fact]
    public async Task Approve_AsUser_IsForbiddenAndTouchesNothing()
    {
        var loan = PendingLoan();

        await Assert.ThrowsAsync<ForbiddenException>(() => AsUser().ApproveAsync(loan.Id, _ct));

        Assert.Equal(LoanStatus.Pending, loan.Status);
        await _uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<Task<LoanDto>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_AlreadyResolved_ThrowsConflictAndKeepsCache()
    {
        var loan = PendingLoan();
        loan.Reject(_adminId);

        await Assert.ThrowsAsync<ConflictException>(() => AsAdmin().ApproveAsync(loan.Id, _ct));

        Assert.Equal(LoanStatus.Rejected, loan.Status);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _cache.DidNotReceive().Remove(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Approve_WhenSaveFails_DoesNotInvalidateCache()
    {
        var loan = PendingLoan();
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConflictException("concurrencia")));

        await Assert.ThrowsAsync<ConflictException>(() => AsAdmin().ApproveAsync(loan.Id, _ct));

        _cache.DidNotReceive().Remove(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Approve_UnknownLoan_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => AsAdmin().ApproveAsync(Guid.NewGuid(), _ct));

    // ---------- Caché del estado ----------

    [Fact]
    public async Task GetStatus_CacheHit_DoesNotQueryRepository()
    {
        var id = Guid.NewGuid();
        var cached = new LoanStatusDto(id, _userId, LoanStatus.Approved, DateTime.UtcNow);
        _cache.TryGet(id, out Arg.Any<LoanStatusDto?>()).Returns(ci =>
        {
            ci[1] = cached;
            return true;
        });

        var result = await AsUser().GetStatusAsync(id, _ct);

        Assert.Equal(LoanStatus.Approved, result.Status);
        await _loans.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStatus_CacheMiss_LoadsFromRepositoryAndStoresInCache()
    {
        var loan = PendingLoan();
        _cache.TryGet(loan.Id, out Arg.Any<LoanStatusDto?>()).Returns(false);

        var result = await AsUser().GetStatusAsync(loan.Id, _ct);

        Assert.Equal(LoanStatus.Pending, result.Status);
        _cache.Received(1).Set(Arg.Is<LoanStatusDto>(s => s.Id == loan.Id));
    }

    [Fact]
    public async Task GetStatus_OfAnotherUsersLoan_IsForbiddenEvenIfCached()
    {
        var id = Guid.NewGuid();
        var cached = new LoanStatusDto(id, Guid.NewGuid(), LoanStatus.Pending, null);
        _cache.TryGet(id, out Arg.Any<LoanStatusDto?>()).Returns(ci =>
        {
            ci[1] = cached;
            return true;
        });

        await Assert.ThrowsAsync<ForbiddenException>(() => AsUser().GetStatusAsync(id, _ct));
    }

    // ---------- Modificar / eliminar ----------

    [Fact]
    public async Task Update_ByOwner_ChangesAmountAndInvalidatesCache()
    {
        var loan = PendingLoan();

        var dto = await AsUser().UpdateAsync(loan.Id, new UpdateLoanRequest(900_000m, 24), _ct);

        Assert.Equal(900_000m, dto.Amount);
        Assert.Equal(24, dto.TermMonths);
        _cache.Received(1).Remove(loan.Id);
    }

    [Fact]
    public async Task Update_ByAnotherUser_IsForbidden()
    {
        var loan = PendingLoan(ownerId: Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() => AsUser().UpdateAsync(loan.Id, new UpdateLoanRequest(900_000m, 24), _ct));
    }

    [Fact]
    public async Task Delete_ResolvedLoan_ThrowsConflict()
    {
        var loan = PendingLoan();
        loan.Approve(_adminId);

        await Assert.ThrowsAsync<ConflictException>(() => AsUser().DeleteAsync(loan.Id, _ct));

        _loans.DidNotReceive().Remove(Arg.Any<LoanRequest>());
    }

    [Fact]
    public async Task Delete_PendingByOwner_RemovesLoan()
    {
        var loan = PendingLoan();

        await AsUser().DeleteAsync(loan.Id, _ct);

        _loans.Received(1).Remove(loan);
        _cache.Received(1).Remove(loan.Id);
    }

    // ---------- Consultas ----------

    [Fact]
    public async Task ListAll_AsUser_IsForbidden() =>
        await Assert.ThrowsAsync<ForbiddenException>(() => AsUser().ListAllAsync(null, _ct));

    [Fact]
    public async Task GetById_OfAnotherUsersLoan_IsForbiddenForUserButAllowedForAdmin()
    {
        var loan = PendingLoan(ownerId: Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() => AsUser().GetByIdAsync(loan.Id, _ct));
        Assert.Equal(loan.Id, (await AsAdmin().GetByIdAsync(loan.Id, _ct)).Id);
    }
}
