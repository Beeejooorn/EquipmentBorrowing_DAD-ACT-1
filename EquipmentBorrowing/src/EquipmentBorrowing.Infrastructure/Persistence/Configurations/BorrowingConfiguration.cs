using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipmentBorrowing.Infrastructure.Persistence.Configurations;

public class BorrowingConfiguration : IEntityTypeConfiguration<Borrowing>
{
    public void Configure(EntityTypeBuilder<Borrowing> builder)
    {
        builder.ToTable("Borrowings", table =>
        {
            // BR-11: expected return date must be later than the checkout date.
            table.HasCheckConstraint("CK_Borrowings_ExpectedAfterCheckout",
                "\"ExpectedReturnDate\" > \"DateBorrowed\"");

            // BR-13: a return date can never be before the checkout date.
            table.HasCheckConstraint("CK_Borrowings_ReturnNotBeforeCheckout",
                "\"ReturnedAt\" IS NULL OR \"ReturnedAt\" >= \"DateBorrowed\"");

            // Status and ReturnedAt must agree: Active has no return date, Returned has one.
            table.HasCheckConstraint("CK_Borrowings_StatusMatchesReturnDate",
                "(\"Status\" = 'Active' AND \"ReturnedAt\" IS NULL) OR (\"Status\" = 'Returned' AND \"ReturnedAt\" IS NOT NULL)");
        });

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedOnAdd();

        // DateOnly is stored as a PostgreSQL "date" column (calendar date, no time zone).
        builder.Property(b => b.DateBorrowed).IsRequired();
        builder.Property(b => b.ExpectedReturnDate).IsRequired();
        builder.Property(b => b.ReturnedAt); // optional (nullable)

        builder.Property(b => b.Status)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        // BR-14: every borrowing points to exactly one existing student and one existing item.
        // Restrict = PostgreSQL refuses to delete a student/item that still has history.
        builder.HasOne(b => b.Student)
               .WithMany()
               .HasForeignKey(b => b.StudentId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Equipment)
               .WithMany()
               .HasForeignKey(b => b.EquipmentId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.StudentId);
        builder.HasIndex(b => b.Status);

        // BR-10 / BR-16: at most ONE active borrowing per item, enforced by PostgreSQL.
        // This is a "partial unique index": it only looks at rows where Status = 'Active',
        // so an item can have many Returned rows but never two Active rows.
        builder.HasIndex(b => b.EquipmentId)
               .IsUnique()
               .HasFilter("\"Status\" = 'Active'")
               .HasDatabaseName("IX_Borrowings_OneActivePerEquipment");
    }
}
