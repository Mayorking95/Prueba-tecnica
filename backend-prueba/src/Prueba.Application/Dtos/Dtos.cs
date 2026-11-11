using System.ComponentModel.DataAnnotations;
using Prueba.Domain.Enums;

namespace Prueba.Application.Dtos;

public record LoginRequest(
    [Required(ErrorMessage = "El usuario es obligatorio.")] string Username,
    [Required(ErrorMessage = "La contraseña es obligatoria.")] string Password);

public record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, StringLength(100, MinimumLength = 6)] string Password,
    [Required, StringLength(100, MinimumLength = 2)] string FullName);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public record UserDto(Guid Id, string Username, string FullName, UserRole Role, DateTime CreatedAt);

public record CreateUserRequest(
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, StringLength(100, MinimumLength = 6)] string Password,
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    UserRole Role);

public record UpdateUserRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    UserRole Role,
    [StringLength(100, MinimumLength = 6)] string? Password);

public record CreateLoanRequest(
    [Range(typeof(decimal), "1", "999999999999", ErrorMessage = "El monto debe ser mayor a 0.")] decimal Amount,
    [Range(1, 600, ErrorMessage = "El plazo debe ser un número de meses mayor a 0.")] int TermMonths);

public record UpdateLoanRequest(
    [Range(typeof(decimal), "1", "999999999999", ErrorMessage = "El monto debe ser mayor a 0.")] decimal Amount,
    [Range(1, 600, ErrorMessage = "El plazo debe ser un número de meses mayor a 0.")] int TermMonths);

public record LoanDto(
    Guid Id,
    Guid UserId,
    string UserName,
    decimal Amount,
    int TermMonths,
    LoanStatus Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

public record LoanStatusDto(Guid Id, Guid UserId, LoanStatus Status, DateTime? ResolvedAt);
