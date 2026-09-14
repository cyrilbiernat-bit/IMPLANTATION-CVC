using System.Text.Json;
using Bim.Cvc.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bim.Cvc.Infrastructure.Persistence.Configurations;

internal sealed class BuildingModelConfiguration : IEntityTypeConfiguration<BuildingModel>
{
    public void Configure(EntityTypeBuilder<BuildingModel> builder)
    {
        builder.ToTable("building_models");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.FileName).IsRequired().HasMaxLength(500);
        builder.Property(m => m.UploadedAt).IsRequired();

        // La géométrie triangulée (souvent volumineuse) est écrite une
        // seule fois à l'import et jamais modifiée en place ensuite —
        // l'égalité de référence suffit au suivi de changements d'EF Core.
        builder.Property(m => m.Elements)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<BuildingElement>>(v, (JsonSerializerOptions?)null)!,
                new ValueComparer<IReadOnlyList<BuildingElement>>(
                    (a, b) => ReferenceEquals(a, b),
                    v => v == null ? 0 : v.Count,
                    v => v))
            .HasColumnType("jsonb")
            .HasColumnName("elements");

        builder.HasOne<Project>().WithMany().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => m.ProjectId).IsUnique();
    }
}
