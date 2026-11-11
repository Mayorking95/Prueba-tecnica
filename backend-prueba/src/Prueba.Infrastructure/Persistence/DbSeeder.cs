using Microsoft.EntityFrameworkCore;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Domain.Enums;

namespace Prueba.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task InitializeAsync(AppDbContext db, IPasswordHasher hasher, bool seed)
    {
        await db.Database.EnsureCreatedAsync();
        if (!seed || await db.Users.AnyAsync()) return;

        db.Users.AddRange(
            User.Create("admin", hasher.Hash("admin123"), "Administrador", UserRole.Admin),
            User.Create("maria", hasher.Hash("maria123"), "María Gómez", UserRole.User),
            User.Create("juan", hasher.Hash("juan123"), "Juan Pérez", UserRole.User));
        await db.SaveChangesAsync();
    }
}
