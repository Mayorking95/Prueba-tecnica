using Prueba.Application.Dtos;
using Prueba.Domain.Entities;

namespace Prueba.Application.Services;

internal static class Mappings
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Username, u.FullName, u.Role, u.CreatedAt);

    public static LoanDto ToDto(this LoanRequest l) =>
        new(l.Id, l.UserId, l.User?.FullName ?? string.Empty, l.Amount, l.TermMonths, l.Status, l.CreatedAt, l.ResolvedAt);

    public static LoanStatusDto ToStatusDto(this LoanRequest l) => new(l.Id, l.UserId, l.Status, l.ResolvedAt);
}
