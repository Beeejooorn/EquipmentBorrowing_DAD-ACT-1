using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment", table =>
        {
            // Required text must not be only spaces (BR-06, BR-16).
            table.HasCheckConstraint("CK_Equipment_EquipmentCode_NotBlank", "btrim(\"EquipmentCode\") <> ''");
            table.HasCheckConstraint("CK_Equipment_Name_NotBlank", "btrim(\"Name\") <> ''");
            table.HasCheckConstraint("CK_Equipment_Description_NotBlank", "\"Description\" IS NULL OR btrim(\"Description\") <> ''");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.EquipmentCode).IsRequired().HasMaxLength(30);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(200); // optional (nullable)
        builder.Property(e => e.IsAvailable).IsRequired();

        // BR-05 / BR-16: equipment codes must be unique after trimming and ignoring case.
        // Same idea as the student number key (see StudentConfiguration).
        builder.Property<string>("EquipmentCodeKey")
               .HasComputedColumnSql("upper(btrim(\"EquipmentCode\"))", stored: true);

        builder.HasIndex("EquipmentCodeKey")
               .IsUnique()
               .HasDatabaseName("IX_Equipment_EquipmentCodeKey");

        // Names do not have to be unique (two items can both be called "Laptop").
        builder.HasIndex(e => e.Name);
    }
}
