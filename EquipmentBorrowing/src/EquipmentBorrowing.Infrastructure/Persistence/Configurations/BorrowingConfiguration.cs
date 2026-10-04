using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public class BorrowingConfiguration : IEntityTypeConfiguration<Borrowing>
{
    public void Configure(EntityTypeBuilder<Borrowing> builder)
    {
        builder.ToTable("Borrowings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedOnAdd();

        builder.Property(b => b.DateBorrowed).IsRequired();
        builder.Property(b => b.ExpectedReturnDate).IsRequired();
        builder.Property(b => b.ReturnedAt); // optional (nullable)

        builder.Property(b => b.Status)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.HasOne<Student>()
               .WithMany()
               .HasForeignKey(b => b.StudentId)
               .IsRequired();

        builder.HasOne<Equipment>()
               .WithMany()
               .HasForeignKey(b => b.EquipmentId)
               .IsRequired();

        builder.HasIndex(b => b.StudentId);
        builder.HasIndex(b => b.EquipmentId);
        builder.HasIndex(b => b.Status);
    }
}