using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

/// <summary>
/// Contexto do Entity Framework Core para o Integration Hub
/// </summary>
public class IntegrationHubDbContext : DbContext
{
    public IntegrationHubDbContext(DbContextOptions<IntegrationHubDbContext> options) : base(options)
    {
    }

    public DbSet<IntegrationRequest> IntegrationRequests => Set<IntegrationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuração da entidade IntegrationRequest
        modelBuilder.Entity<IntegrationRequest>(entity =>
        {
            entity.ToTable("IntegrationRequests");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExternalId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.SourceSystem)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.TargetSystem)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Payload)
                .IsRequired();

            entity.Property(e => e.CorrelationId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .IsRequired();

            // Índices para consultas comuns
            entity.HasIndex(e => e.ExternalId);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
