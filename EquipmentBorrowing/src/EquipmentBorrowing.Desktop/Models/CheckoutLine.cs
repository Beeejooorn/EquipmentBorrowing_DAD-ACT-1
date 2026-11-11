namespace EquipmentBorrowing.Desktop.Models;

using EquipmentBorrowing.Domain;

// One line in the multi-item checkout list.
// We wrap the item in its own object so the same item can appear twice in the list
// (a duplicate is allowed on screen, but the service rejects it when checking out).
public class CheckoutLine
{
    public int LineNumber { get; }
    public Equipment Item { get; }

    public CheckoutLine(int lineNumber, Equipment item)
    {
        LineNumber = lineNumber;
        Item = item;
    }

    public string Code
    {
        get { return Item.EquipmentCode; }
    }

    public string Name
    {
        get { return Item.Name; }
    }

    public bool IsAvailable
    {
        get { return Item.IsAvailable; }
    }
}
