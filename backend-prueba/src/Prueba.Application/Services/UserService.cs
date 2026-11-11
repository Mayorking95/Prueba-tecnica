using Prueba.Application.Dtos;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Domain.Exceptions;

namespace Prueba.Application.Services;

public class UserService(
    IUserRepository users,
    IUnitOfWork uow,
    IPasswordHasher hasher,
    ICurrentUser current) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        EnsureAdmin();
        return (await users.ListAsync(ct)).Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        if (!current.IsAdmin && current.Id != id)
            throw new ForbiddenException("Solo puedes consultar tu propio usuario.");

        return (await FindAsync(id, ct)).ToDto();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        EnsureAdmin();

        if (await users.UsernameExistsAsync(request.Username.Trim().ToLowerInvariant(), ct))
            throw new ConflictException("Ya existe un usuario con ese nombre de usuario.");

        var user = User.Create(request.Username, hasher.Hash(request.Password), request.FullName, request.Role);
        await users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        EnsureAdmin();
        var user = await FindAsync(id, ct);

        if (current.Id == id && request.Role != user.Role)
            throw new ConflictException("No puedes cambiar tu propio rol.");

        user.UpdateProfile(request.FullName, request.Role);
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.ChangePasswordHash(hasher.Hash(request.Password));

        await uow.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        EnsureAdmin();
        var user = await FindAsync(id, ct);

        if (current.Id == id)
            throw new ConflictException("No puedes eliminar tu propio usuario.");
        if (await users.HasLoansAsync(id, ct))
            throw new ConflictException("No se puede eliminar un usuario que tiene solicitudes de préstamo.");

        users.Remove(user);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<User> FindAsync(Guid id, CancellationToken ct) =>
        await users.GetByIdAsync(id, ct) ?? throw new NotFoundException("Usuario no encontrado.");

    private void EnsureAdmin()
    {
        if (!current.IsAdmin) throw new ForbiddenException("Solo un administrador puede realizar esta acción.");
    }
}
