using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Prueba.Application.Interfaces;
using Prueba.Domain.Enums;
using Prueba.Domain.Exceptions;

namespace Prueba.Api.Extensions;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new UnauthorizedException("No hay un usuario autenticado.");

    public Guid Id =>
        Guid.TryParse(Principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new UnauthorizedException("Token inválido.");

    public UserRole Role =>
        Enum.TryParse<UserRole>(Principal.FindFirstValue("role"), out var role) ? role : UserRole.User;

    public bool IsAdmin => Role == UserRole.Admin;
}
