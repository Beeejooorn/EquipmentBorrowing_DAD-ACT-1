using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.IsAvailable).IsRequired();
        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasData(
            new Equipment(1, "Laptop 01", true),
            new Equipment(2, "Laptop 02", true),
            new Equipment(3, "Projector 01", true),
            new Equipment(4, "Camera 01", false)
        );

    }
}