using Microsoft.EntityFrameworkCore;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Domain.Enums;

namespace Prueba.Infrastructure.Persistence;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Username == username, ct);

    public Task<bool> HasLoansAsync(Guid userId, CancellationToken ct) =>
        db.Loans.AnyAsync(l => l.UserId == userId, ct);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct) => await db.Users.AddAsync(user, ct);

    public void Remove(User user) => db.Users.Remove(user);
}

public class LoanRepository(AppDbContext db) : ILoanRepository
{
    public Task<LoanRequest?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Loans.Include(l => l.User).FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<LoanRequest>> ListByUserAsync(Guid userId, CancellationToken ct) =>
        await db.Loans.AsNoTracking().Include(l => l.User)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LoanRequest>> ListAsync(LoanStatus? status, CancellationToken ct)
    {
        var query = db.Loans.AsNoTracking().Include(l => l.User).AsQueryable();
        if (status is not null) query = query.Where(l => l.Status == status);
        return await query.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
    }

    public Task<int> CountPendingAsync(Guid userId, CancellationToken ct) =>
        db.Loans.CountAsync(l => l.UserId == userId && l.Status == LoanStatus.Pending, ct);

    public async Task AddAsync(LoanRequest loan, CancellationToken ct) => await db.Loans.AddAsync(loan, ct);

    public void Remove(LoanRequest loan) => db.Loans.Remove(loan);
}
