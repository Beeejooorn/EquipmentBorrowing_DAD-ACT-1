namespace EquipmentBorrowing.Domain;

public class Student
{
    public int Id { get; }

    // Institutional student number, e.g. "2023-0001" (BR-01).
    public string StudentNumber { get; private set; }

    // Full name of the student (BR-02).
    public string Name { get; private set; }

    // Optional contact information. Null means "no contact saved" (BR-02).
    public string? ContactInfo { get; private set; }

    // true = eligible to borrow, false = restricted (BR-04).
    public bool IsAllowedToBorrow { get; private set; }

    public Student(int Id, string StudentNumber, string Name, string? ContactInfo, bool IsAllowedToBorrow)
    {
        this.Id = Id;
        this.StudentNumber = StudentNumber;
        this.Name = Name;
        this.ContactInfo = ContactInfo;
        this.IsAllowedToBorrow = IsAllowedToBorrow;
    }

    // UC-13: only the descriptive fields change. The Id stays the same,
    // so every borrowing that points to this student is kept (BR-03).
    public void UpdateDetails(string studentNumber, string name, string? contactInfo)
    {
        this.StudentNumber = studentNumber;
        this.Name = name;
        this.ContactInfo = contactInfo;
    }

    // UC-14: permit (true) or restrict (false) new borrowing.
    public void SetEligibility(bool isAllowedToBorrow)
    {
        this.IsAllowedToBorrow = isAllowedToBorrow;
    }
}
