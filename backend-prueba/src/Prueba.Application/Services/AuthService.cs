using Prueba.Application.Dtos;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Domain.Enums;
using Prueba.Domain.Exceptions;

namespace Prueba.Application.Services;

public class AuthService(
    IUserRepository users,
    IUnitOfWork uow,
    IPasswordHasher hasher,
    ITokenService tokens) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.GetByUsernameAsync(request.Username.Trim().ToLowerInvariant(), ct);

        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Usuario o contraseña incorrectos.");

        return BuildResponse(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (await users.UsernameExistsAsync(request.Username.Trim().ToLowerInvariant(), ct))
            throw new ConflictException("Ya existe un usuario con ese nombre de usuario.");

        var user = User.Create(request.Username, hasher.Hash(request.Password), request.FullName, UserRole.User);
        await users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);

        return BuildResponse(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = tokens.Generate(user);
        return new AuthResponse(token, expiresAt, user.ToDto());
    }
}
