using Prueba.Application.Dtos;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Domain.Enums;
using Prueba.Domain.Exceptions;

namespace Prueba.Application.Services;

public class LoanService(
    ILoanRepository loans,
    IUnitOfWork uow,
    ILoanStatusCache cache,
    ICurrentUser current) : ILoanService
{
    public async Task<LoanDto> CreateAsync(CreateLoanRequest request, CancellationToken ct)
    {
        if (current.IsAdmin)
            throw new ForbiddenException("Un administrador no puede solicitar préstamos.");

        if (await loans.CountPendingAsync(current.Id, ct) >= LoanRequest.MaxPendingPerUser)
            throw new DomainException(
                $"Ya tienes {LoanRequest.MaxPendingPerUser} solicitudes pendientes. Espera a que se resuelvan.");

        var loan = LoanRequest.Create(current.Id, request.Amount, request.TermMonths);
        await loans.AddAsync(loan, ct);
        await uow.SaveChangesAsync(ct);

        return (await loans.GetByIdAsync(loan.Id, ct))!.ToDto();
    }

    public async Task<IReadOnlyList<LoanDto>> ListMineAsync(CancellationToken ct) =>
        (await loans.ListByUserAsync(current.Id, ct)).Select(l => l.ToDto()).ToList();

    public async Task<IReadOnlyList<LoanDto>> ListAllAsync(LoanStatus? status, CancellationToken ct)
    {
        EnsureAdmin();
        return (await loans.ListAsync(status, ct)).Select(l => l.ToDto()).ToList();
    }

    public async Task<LoanDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var loan = await FindAsync(id, ct);
        EnsureOwnerOrAdmin(loan.UserId);
        return loan.ToDto();
    }

    public async Task<LoanStatusDto> GetStatusAsync(Guid id, CancellationToken ct)
    {
        if (!cache.TryGet(id, out var status) || status is null)
        {
            status = (await FindAsync(id, ct)).ToStatusDto();
            cache.Set(status);
        }

        EnsureOwnerOrAdmin(status.UserId);
        return status;
    }

    public async Task<LoanDto> UpdateAsync(Guid id, UpdateLoanRequest request, CancellationToken ct)
    {
        var loan = await FindAsync(id, ct);
        if (loan.UserId != current.Id)
            throw new ForbiddenException("Solo puedes modificar tus propias solicitudes.");

        loan.Update(request.Amount, request.TermMonths);
        await uow.SaveChangesAsync(ct);
        cache.Remove(id);
        return loan.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var loan = await FindAsync(id, ct);
        EnsureOwnerOrAdmin(loan.UserId);

        loan.EnsurePending("eliminar");

        loans.Remove(loan);
        await uow.SaveChangesAsync(ct);
        cache.Remove(id);
    }

    public Task<LoanDto> ApproveAsync(Guid id, CancellationToken ct) => ResolveAsync(id, approve: true, ct);

    public Task<LoanDto> RejectAsync(Guid id, CancellationToken ct) => ResolveAsync(id, approve: false, ct);

    private async Task<LoanDto> ResolveAsync(Guid id, bool approve, CancellationToken ct)
    {
        EnsureAdmin();

        var dto = await uow.ExecuteInTransactionAsync(async () =>
        {
            var loan = await FindAsync(id, ct);
            if (approve) loan.Approve(current.Id); else loan.Reject(current.Id);
            await uow.SaveChangesAsync(ct);
            return loan.ToDto();
        }, ct);

        cache.Remove(id);
        return dto;
    }

    private async Task<LoanRequest> FindAsync(Guid id, CancellationToken ct) =>
        await loans.GetByIdAsync(id, ct) ?? throw new NotFoundException("Solicitud no encontrada.");

    private void EnsureAdmin()
    {
        if (!current.IsAdmin) throw new ForbiddenException("Solo un administrador puede realizar esta acción.");
    }

    private void EnsureOwnerOrAdmin(Guid ownerId)
    {
        if (!current.IsAdmin && current.Id != ownerId)
            throw new ForbiddenException("No tienes acceso a esta solicitud.");
    }
}
