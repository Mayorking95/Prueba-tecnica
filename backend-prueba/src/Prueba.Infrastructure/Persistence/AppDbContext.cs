using Microsoft.EntityFrameworkCore;
using Prueba.Domain.Entities;
using Prueba.Domain.Enums;

namespace Prueba.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<LoanRequest> Loans => Set<LoanRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.HasKey(x => x.Id);
            b.Property(x => x.Username).HasMaxLength(50).IsRequired();
            b.HasIndex(x => x.Username).IsUnique();
            b.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
            b.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.CreatedAt);
        });

        modelBuilder.Entity<LoanRequest>(b =>
        {
            b.ToTable("loan_requests", t =>
            {
                t.HasCheckConstraint("ck_loan_amount_positive", "\"Amount\" > 0");
                t.HasCheckConstraint("ck_loan_term_positive", "\"TermMonths\" > 0");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(x => new { x.UserId, x.Status });
            b.HasIndex(x => x.Status);
            b.Property(x => x.Version)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            b.HasOne(x => x.User)
                .WithMany(u => u.Loans)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
