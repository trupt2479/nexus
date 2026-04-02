using Microsoft.EntityFrameworkCore;
using Nexus.Orchestrator.Models;

namespace Nexus.Orchestrator.Data;

public class SagaDbContext : DbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options) : base(options) { }

    public DbSet<SagaTransaction> SagaTransactions { get; set; }
    public DbSet<SagaStep> SagaSteps { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SagaTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Steps)
                  .WithOne(e => e.Transaction)
                  .HasForeignKey(e => e.TransactionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SagaStep>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}
