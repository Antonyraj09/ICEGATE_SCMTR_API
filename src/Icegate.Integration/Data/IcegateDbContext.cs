using Icegate.Integration.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Icegate.Integration.Data;

public class IcegateDbContext : DbContext
{
    public IcegateDbContext(DbContextOptions<IcegateDbContext> options) : base(options)
    {
    }

    public DbSet<IcegateApiTransaction> Transactions => Set<IcegateApiTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<IcegateApiTransaction>(entity =>
        {
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => new { e.ClientId, e.IcegateUniqueId });
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.LocalReferenceNo);
            entity.HasIndex(e => new { e.SenderId, e.MessageId });

            entity.Property(e => e.ApiType).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
        });
    }
}
