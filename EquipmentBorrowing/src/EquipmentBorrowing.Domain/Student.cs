namespace EquipmentBorrowing.Domain;

public class Student
{
    public int Id { get; }
    public string Name { get; }
    public bool IsAllowedToBorrow { get; }

    public Student(int Id, string Name, bool IsAllowedToBorrow)
    {
        this.Id = Id;
        this.Name = Name;
        this.IsAllowedToBorrow = IsAllowedToBorrow;
    }

}