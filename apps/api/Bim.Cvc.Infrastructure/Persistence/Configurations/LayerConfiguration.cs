using Bim.Cvc.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bim.Cvc.Infrastructure.Persistence.Configurations;

internal sealed class LayerConfiguration : IEntityTypeConfiguration<Layer>
{
    public void Configure(EntityTypeBuilder<Layer> builder)
    {
        builder.ToTable("layers");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Color).IsRequired().HasMaxLength(20);

        builder.HasOne<Drawing>().WithMany().HasForeignKey(l => l.DrawingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => l.DrawingId);
    }
}
