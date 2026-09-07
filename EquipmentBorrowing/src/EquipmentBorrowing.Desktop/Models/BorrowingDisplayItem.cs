namespace EquipmentBorrowing.Desktop.Models;

using EquipmentBorrowing.Domain;

public class BorrowingDisplayItem
{
    public Borrowing Borrowing { get; }
    public string StudentName { get; }
    public string EquipmentName { get; }

    public BorrowingDisplayItem(Borrowing borrowing, string studentName, string equipmentName)
    {
        Borrowing = borrowing;
        StudentName = studentName;
        EquipmentName = equipmentName;
    }
}