using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prueba.Api.Controllers;
using Prueba.Api.Middleware;
using Prueba.Application.Dtos;
using Prueba.Domain.Entities;
using Prueba.Domain.Enums;
using Prueba.Domain.Exceptions;
using Prueba.Infrastructure.Caching;
using Prueba.Infrastructure.Security;

namespace Prueba.Tests;

public class ControllerSecurityTests
{
    [Theory]
    [InlineData(nameof(LoansController.Approve))]
    [InlineData(nameof(LoansController.Reject))]
    [InlineData(nameof(LoansController.ListAll))]
    public void LoanAdminEndpoints_RequireAdminRole(string action)
    {
        var attr = typeof(LoansController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("Admin", attr!.Roles);
    }

    [Theory]
    [InlineData(typeof(LoansController))]
    [InlineData(typeof(UsersController))]
    public void Controllers_RequireAuthenticationByDefault(Type controller) =>
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());

    [Theory]
    [InlineData(nameof(UsersController.List))]
    [InlineData(nameof(UsersController.Create))]
    [InlineData(nameof(UsersController.Update))]
    [InlineData(nameof(UsersController.Delete))]
    public void UserManagementEndpoints_RequireAdminRole(string action) =>
        Assert.Equal("Admin", typeof(UsersController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);

    [Fact]
    public void AuthController_IsAnonymous() =>
        Assert.NotNull(typeof(AuthController).GetCustomAttribute<AllowAnonymousAttribute>());
}

public class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int> Cases => new()
    {
        { new NotFoundException("x"), 404 },
        { new ConflictException("x"), 409 },
        { new ForbiddenException("x"), 403 },
        { new UnauthorizedException("x"), 401 },
        { new DomainException("x"), 422 },
        { new InvalidOperationException("secreto interno"), 500 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MapsExceptionToHttpStatus(Exception ex, int expected)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, ex, default);

        Assert.True(handled);
        Assert.Equal(expected, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedError_DoesNotLeakInternalDetails()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(context, new InvalidOperationException("secreto interno"), default);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("secreto interno", body);
        Assert.Equal(500, JsonDocument.Parse(body).RootElement.GetProperty("status").GetInt32());
    }
}

public class InfrastructureTests
{
    [Fact]
    public void Cache_SetGetRemove()
    {
        var cache = new MemoryLoanStatusCache(new MemoryCache(new MemoryCacheOptions()));
        var status = new LoanStatusDto(Guid.NewGuid(), Guid.NewGuid(), LoanStatus.Pending, null);

        Assert.False(cache.TryGet(status.Id, out _));
        cache.Set(status);
        Assert.True(cache.TryGet(status.Id, out var hit));
        Assert.Equal(status, hit);

        cache.Remove(status.Id);
        Assert.False(cache.TryGet(status.Id, out _));
    }

    [Fact]
    public void PasswordHasher_HashesAndVerifies()
    {
        var hasher = new BCryptPasswordHasher();
        var hash = hasher.Hash("clave123");

        Assert.NotEqual("clave123", hash);
        Assert.True(hasher.Verify("clave123", hash));
        Assert.False(hasher.Verify("otra", hash));
    }

    [Fact]
    public void JwtToken_ContainsIdAndRoleClaims()
    {
        var options = Options.Create(new JwtOptions { Key = new string('k', 40), ExpirationMinutes = 10 });
        var user = User.Create("admin", "hash", "Administrador", UserRole.Admin);

        var (token, expiresAt) = new JwtTokenService(options).Generate(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("Admin", jwt.Claims.First(c => c.Type == "role").Value);
        Assert.True(expiresAt > DateTime.UtcNow);
    }
}
