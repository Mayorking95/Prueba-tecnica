using NSubstitute;
using Prueba.Application.Dtos;
using Prueba.Application.Interfaces;
using Prueba.Application.Services;
using Prueba.Domain.Entities;
using Prueba.Domain.Enums;
using Prueba.Domain.Exceptions;
using Prueba.Infrastructure.Security;

namespace Prueba.Tests;

public class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = new BCryptPasswordHasher();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tokens.Generate(Arg.Any<User>()).Returns(("token-123", DateTime.UtcNow.AddHours(1)));
        _sut = new AuthService(_users, _uow, _hasher, _tokens);
    }

    private User ExistingUser(UserRole role = UserRole.User)
    {
        var user = User.Create("maria", _hasher.Hash("maria123"), "María Gómez", role);
        _users.GetByUsernameAsync("maria", Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var user = ExistingUser();

        var result = await _sut.LoginAsync(new LoginRequest("  MARIA ", "maria123"), default);

        Assert.Equal("token-123", result.Token);
        Assert.Equal(user.Id, result.User.Id);
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        ExistingUser();
        await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(new LoginRequest("maria", "mala"), default));
    }

    [Fact]
    public async Task Login_UnknownUser_ThrowsUnauthorizedWithSameMessage()
    {
        ExistingUser();
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(new LoginRequest("nadie", "x"), default));
        var wrongPass = await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(new LoginRequest("maria", "x"), default));
        Assert.Equal(wrongPass.Message, ex.Message);
    }

    [Fact]
    public async Task Register_NewUser_AlwaysGetsUserRoleAndHashedPassword()
    {
        User? saved = null;
        await _users.AddAsync(Arg.Do<User>(u => saved = u), Arg.Any<CancellationToken>());

        var result = await _sut.RegisterAsync(new RegisterRequest("nuevo", "secreto1", "Nuevo Usuario"), default);

        Assert.Equal(UserRole.User, saved!.Role);
        Assert.NotEqual("secreto1", saved.PasswordHash);
        Assert.True(_hasher.Verify("secreto1", saved.PasswordHash));
        Assert.Equal("token-123", result.Token);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ThrowsConflict()
    {
        _users.UsernameExistsAsync("maria", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.RegisterAsync(new RegisterRequest("maria", "secreto1", "Otra María"), default));
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public class UserServiceTests
{
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = new BCryptPasswordHasher();

    private UserService AsAdmin() => new(_users, _uow, _hasher, TestHelpers.CurrentUser(_adminId, UserRole.Admin));
    private UserService AsUser(Guid id) => new(_users, _uow, _hasher, TestHelpers.CurrentUser(id, UserRole.User));

    private User StoredUser(string username = "juan")
    {
        var user = User.Create(username, _hasher.Hash("juan123"), "Juan Pérez", UserRole.User);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task List_AsUser_IsForbidden() =>
        await Assert.ThrowsAsync<ForbiddenException>(() => AsUser(Guid.NewGuid()).ListAsync(default));

    [Fact]
    public async Task Create_AsAdmin_StoresHashedPassword()
    {
        User? saved = null;
        await _users.AddAsync(Arg.Do<User>(u => saved = u), Arg.Any<CancellationToken>());

        var dto = await AsAdmin().CreateAsync(new CreateUserRequest("Ana", "clave123", "Ana Ruiz", UserRole.Admin), default);

        Assert.Equal("ana", dto.Username);
        Assert.Equal(UserRole.Admin, dto.Role);
        Assert.True(_hasher.Verify("clave123", saved!.PasswordHash));
    }

    [Fact]
    public async Task Create_AsUser_IsForbidden() =>
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AsUser(Guid.NewGuid()).CreateAsync(new CreateUserRequest("x1y", "clave123", "Xy", UserRole.Admin), default));

    [Fact]
    public async Task Create_DuplicateUsername_ThrowsConflict()
    {
        _users.UsernameExistsAsync("ana", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            AsAdmin().CreateAsync(new CreateUserRequest("ana", "clave123", "Ana Ruiz", UserRole.User), default));
    }

    [Fact]
    public async Task GetById_AnotherUsersProfile_IsForbiddenForUser()
    {
        var other = StoredUser();
        await Assert.ThrowsAsync<ForbiddenException>(() => AsUser(Guid.NewGuid()).GetByIdAsync(other.Id, default));
    }

    [Fact]
    public async Task Delete_Self_ThrowsConflict()
    {
        var admin = User.Create("admin", "hash", "Admin", UserRole.Admin);
        _users.GetByIdAsync(_adminId, Arg.Any<CancellationToken>()).Returns(admin);

        await Assert.ThrowsAsync<ConflictException>(() => AsAdmin().DeleteAsync(_adminId, default));
    }

    [Fact]
    public async Task Delete_UserWithLoans_ThrowsConflict()
    {
        var user = StoredUser();
        _users.HasLoansAsync(user.Id, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() => AsAdmin().DeleteAsync(user.Id, default));
        _users.DidNotReceive().Remove(Arg.Any<User>());
    }

    [Fact]
    public async Task Delete_UserWithoutLoans_RemovesIt()
    {
        var user = StoredUser();

        await AsAdmin().DeleteAsync(user.Id, default);

        _users.Received(1).Remove(user);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ChangingOwnRole_ThrowsConflict()
    {
        var admin = User.Create("admin", "hash", "Admin", UserRole.Admin);
        _users.GetByIdAsync(_adminId, Arg.Any<CancellationToken>()).Returns(admin);

        await Assert.ThrowsAsync<ConflictException>(() =>
            AsAdmin().UpdateAsync(_adminId, new UpdateUserRequest("Admin", UserRole.User, null), default));
    }

    [Fact]
    public async Task Update_UnknownUser_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            AsAdmin().UpdateAsync(Guid.NewGuid(), new UpdateUserRequest("Nombre", UserRole.User, null), default));
}
