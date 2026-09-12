using Bim.Cvc.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bim.Cvc.Infrastructure.Persistence.Configurations;

internal sealed class CvcObjectConfiguration : IEntityTypeConfiguration<CvcObject>
{
    public void Configure(EntityTypeBuilder<CvcObject> builder)
    {
        builder.ToTable("cvc_objects");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Type).HasConversion<string>().HasMaxLength(30).IsRequired();

        // Gaines (Start/End) et accessoires/terminaux/équipements (Position) sont
        // mutuellement exclusifs — trois owned "optionnels" séparés plutôt qu'un
        // seul, puisqu'un objet ne renseigne jamais les deux familles à la fois.
        builder.OwnsOne(o => o.Start, s =>
        {
            s.Property(p => p.X).HasColumnName("start_x");
            s.Property(p => p.Y).HasColumnName("start_y");
        });
        builder.OwnsOne(o => o.End, e =>
        {
            e.Property(p => p.X).HasColumnName("end_x");
            e.Property(p => p.Y).HasColumnName("end_y");
        });
        builder.OwnsOne(o => o.Position, p2 =>
        {
            p2.Property(p => p.X).HasColumnName("position_x");
            p2.Property(p => p.Y).HasColumnName("position_y");
        });

        builder.Property(o => o.ConnectedObjectIds).HasColumnType("uuid[]").HasColumnName("connected_object_ids");

        builder.HasOne<Drawing>().WithMany().HasForeignKey(o => o.DrawingId).OnDelete(DeleteBehavior.Cascade);
        // Restrict : LayerService réaffecte toujours les objets avant de supprimer
        // un calque (module 3) — un objet ne doit jamais se retrouver orphelin.
        builder.HasOne<Layer>().WithMany().HasForeignKey(o => o.LayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => o.DrawingId);
        builder.HasIndex(o => o.LayerId);
    }
}
