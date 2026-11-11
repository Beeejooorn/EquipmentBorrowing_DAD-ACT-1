namespace EquipmentBorrowing.Domain;

public class Equipment
{
    public int Id { get; }

    // Unique identifier printed on the physical item, e.g. "EQ-LAP-01" (BR-05).
    public string EquipmentCode { get; private set; }

    public string Name { get; private set; }

    // Optional description or type. Null means "no description" (BR-06).
    public string? Description { get; private set; }

    // Changed only by the borrow and return workflows, never by editing (BR-07, BR-10).
    public bool IsAvailable { get; private set; }

    public Equipment(int Id, string EquipmentCode, string Name, string? Description, bool IsAvailable)
    {
        this.Id = Id;
        this.EquipmentCode = EquipmentCode;
        this.Name = Name;
        this.Description = Description;
        this.IsAvailable = IsAvailable;
    }

    // UC-16: only the descriptive fields can be edited.
    // There is intentionally no availability parameter here (BR-07).
    public void UpdateDetails(string equipmentCode, string name, string? description)
    {
        this.EquipmentCode = equipmentCode;
        this.Name = name;
        this.Description = description;
    }

    public void MarkAsUnAvailable()
    {
        this.IsAvailable = false;
    }

    public void MarkAsAvailable()
    {
        this.IsAvailable = true;
    }
}
