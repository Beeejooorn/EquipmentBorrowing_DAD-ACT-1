using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students", table =>
        {
            // Required text must not be only spaces (BR-02, BR-16).
            table.HasCheckConstraint("CK_Students_StudentNumber_NotBlank", "btrim(\"StudentNumber\") <> ''");
            table.HasCheckConstraint("CK_Students_Name_NotBlank", "btrim(\"Name\") <> ''");
            table.HasCheckConstraint("CK_Students_ContactInfo_NotBlank", "\"ContactInfo\" IS NULL OR btrim(\"ContactInfo\") <> ''");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();

        builder.Property(s => s.StudentNumber).IsRequired().HasMaxLength(30);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.ContactInfo).HasMaxLength(100); // optional (nullable)
        builder.Property(s => s.IsAllowedToBorrow).IsRequired();

        // BR-01 / BR-16: student numbers must be unique after trimming and ignoring case.
        // PostgreSQL itself calculates this column as upper(btrim(StudentNumber)),
        // so " 2023-0001 " and "2023-0001" get the same key, and the unique index
        // below rejects the second one even if the row is inserted with plain SQL.
        builder.Property<string>("StudentNumberKey")
               .HasComputedColumnSql("upper(btrim(\"StudentNumber\"))", stored: true);

        builder.HasIndex("StudentNumberKey")
               .IsUnique()
               .HasDatabaseName("IX_Students_StudentNumberKey");

        builder.HasIndex(s => s.Name);
    }
}
