using Prueba.Domain.Enums;
using Prueba.Domain.Exceptions;

namespace Prueba.Domain.Entities;

public class LoanRequest
{
    public const decimal MinAmount = 100_000m;
    public const decimal MaxAmount = 50_000_000m;
    public const int MinTermMonths = 1;
    public const int MaxTermMonths = 60;
    public const int MaxPendingPerUser = 3;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public int TermMonths { get; private set; }
    public LoanStatus Status { get; private set; } = LoanStatus.Pending;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }

    public uint Version { get; private set; }

    public User? User { get; private set; }

    private LoanRequest() { } 

    public static LoanRequest Create(Guid userId, decimal amount, int termMonths)
    {
        ValidateAmount(amount);
        ValidateTerm(termMonths);
        return new LoanRequest { UserId = userId, Amount = amount, TermMonths = termMonths };
    }

    public void Update(decimal amount, int termMonths)
    {
        EnsurePending("modificar");
        ValidateAmount(amount);
        ValidateTerm(termMonths);
        Amount = amount;
        TermMonths = termMonths;
    }

    public void Approve(Guid adminId) => Resolve(LoanStatus.Approved, adminId);

    public void Reject(Guid adminId) => Resolve(LoanStatus.Rejected, adminId);

    public void EnsurePending(string action)
    {
        if (Status != LoanStatus.Pending)
            throw new ConflictException($"No se puede {action} una solicitud que ya fue {Describe(Status)}.");
    }

    private void Resolve(LoanStatus newStatus, Guid adminId)
    {
        EnsurePending(newStatus == LoanStatus.Approved ? "aprobar" : "rechazar");
        Status = newStatus;
        ResolvedAt = DateTime.UtcNow;
        ResolvedByUserId = adminId;
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount < MinAmount || amount > MaxAmount)
            throw new DomainException($"El monto debe estar entre {MinAmount:N0} y {MaxAmount:N0}.");
    }

    private static void ValidateTerm(int termMonths)
    {
        if (termMonths < MinTermMonths || termMonths > MaxTermMonths)
            throw new DomainException($"El plazo debe estar entre {MinTermMonths} y {MaxTermMonths} meses.");
    }

    private static string Describe(LoanStatus s) => s == LoanStatus.Approved ? "aprobada" : "rechazada";
}
