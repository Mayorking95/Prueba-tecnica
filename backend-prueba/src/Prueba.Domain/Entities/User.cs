using Prueba.Domain.Enums;
using Prueba.Domain.Exceptions;

namespace Prueba.Domain.Entities;

public class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Username { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public ICollection<LoanRequest> Loans { get; private set; } = [];

    private User() { } 

    public static User Create(string username, string passwordHash, string fullName, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new DomainException("El usuario es obligatorio.");
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("La contraseña es obligatoria.");

        return new User
        {
            Username = username.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Role = role
        };
    }

    public void UpdateProfile(string fullName, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("El nombre es obligatorio.");
        FullName = fullName.Trim();
        Role = role;
    }

    public void ChangePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("La contraseña es obligatoria.");
        PasswordHash = passwordHash;
    }
}
