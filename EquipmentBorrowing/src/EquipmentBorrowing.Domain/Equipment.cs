namespace EquipmentBorrowing.Domain;

public class Equipment
{
    public int Id { get; }
    public string Name { get; }
    public bool IsAvailable { get; private set; }

    public Equipment(int Id, string Name, bool IsAvailable)
    {
        this.Id = Id;
        this.Name = Name;
        this.IsAvailable = IsAvailable;
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