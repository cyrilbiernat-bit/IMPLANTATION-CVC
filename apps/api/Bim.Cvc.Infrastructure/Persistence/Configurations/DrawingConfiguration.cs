using System.Text.Json;
using Bim.Cvc.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bim.Cvc.Infrastructure.Persistence.Configurations;

internal sealed class DrawingConfiguration : IEntityTypeConfiguration<Drawing>
{
    public void Configure(EntityTypeBuilder<Drawing> builder)
    {
        builder.ToTable("drawings");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(500);
        builder.Property(d => d.StoragePath).IsRequired().HasMaxLength(1000);
        builder.Property(d => d.Format).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.UploadedAt).IsRequired();

        builder.Property(d => d.VectorEntities)
            .HasConversion(
                v => PlanEntityJsonConverter.Serialize(v),
                v => PlanEntityJsonConverter.Deserialize(v),
                new ValueComparer<IReadOnlyList<PlanEntity>?>(
                    (a, b) => ReferenceEquals(a, b) || (a != null && b != null && a.SequenceEqual(b)),
                    v => v == null ? 0 : v.Aggregate(0, (hash, e) => HashCode.Combine(hash, e)),
                    v => v))
            .HasColumnType("jsonb")
            .HasColumnName("vector_entities");

        // Calibration (module 2) : petit objet-valeur imbriqué (Calibration >
        // CalibrationPoint), jamais interrogé indépendamment — stocké tel quel en
        // jsonb plutôt qu'aplati en colonnes. EF Core ne sait pas lier son
        // constructeur à des types owned imbriqués (CalibrationPoint), donc
        // l'aplatissement via OwnsOne échoue ; le JSON évite ce problème.
        builder.Property(d => d.Calibration)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Calibration>(v, (JsonSerializerOptions?)null)!)
            .HasColumnType("jsonb")
            .HasColumnName("calibration");

        builder.HasOne<Project>().WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.ProjectId);
    }
}
