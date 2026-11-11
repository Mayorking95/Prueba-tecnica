using NSubstitute;
using Prueba.Application.Interfaces;
using Prueba.Domain.Enums;

namespace Prueba.Tests;

internal static class TestHelpers
{
    public static ICurrentUser CurrentUser(Guid id, UserRole role)
    {
        var current = Substitute.For<ICurrentUser>();
        current.Id.Returns(id);
        current.Role.Returns(role);
        current.IsAdmin.Returns(role == UserRole.Admin);
        return current;
    }

    /// <summary>Unit of work que ejecuta la acción tal cual (sin BD) para poder probar la lógica transaccional.</summary>
    public static IUnitOfWork PassThroughUnitOfWork()
    {
        var uow = Substitute.For<IUnitOfWork>();
        uow.ExecuteInTransactionAsync(Arg.Any<Func<Task<Prueba.Application.Dtos.LoanDto>>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<Task<Prueba.Application.Dtos.LoanDto>>>()());
        return uow;
    }
}
