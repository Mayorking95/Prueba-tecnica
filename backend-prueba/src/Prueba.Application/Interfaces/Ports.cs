using Prueba.Application.Dtos;
using Prueba.Domain.Entities;
using Prueba.Domain.Enums;

namespace Prueba.Application.Interfaces;


public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct);
    Task<bool> HasLoansAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    void Remove(User user);
}

public interface ILoanRepository
{
    Task<LoanRequest?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<LoanRequest>> ListByUserAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<LoanRequest>> ListAsync(LoanStatus? status, CancellationToken ct);
    Task<int> CountPendingAsync(Guid userId, CancellationToken ct);
    Task AddAsync(LoanRequest loan, CancellationToken ct);
    void Remove(LoanRequest loan);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) Generate(User user);
}

public interface ILoanStatusCache
{
    bool TryGet(Guid loanId, out LoanStatusDto? status);
    void Set(LoanStatusDto status);
    void Remove(Guid loanId);
}

public interface ICurrentUser
{
    Guid Id { get; }
    UserRole Role { get; }
    bool IsAdmin { get; }
}

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
}

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public interface ILoanService
{
    Task<LoanDto> CreateAsync(CreateLoanRequest request, CancellationToken ct);
    Task<IReadOnlyList<LoanDto>> ListMineAsync(CancellationToken ct);
    Task<IReadOnlyList<LoanDto>> ListAllAsync(LoanStatus? status, CancellationToken ct);
    Task<LoanDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<LoanStatusDto> GetStatusAsync(Guid id, CancellationToken ct);
    Task<LoanDto> UpdateAsync(Guid id, UpdateLoanRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<LoanDto> ApproveAsync(Guid id, CancellationToken ct);
    Task<LoanDto> RejectAsync(Guid id, CancellationToken ct);
}
