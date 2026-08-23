using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Repositories;

var studentRepo = new InMemoryStudentRepository();
var equipmentRepo = new InMemoryEquipmentRepository();
var borrowingRepo = new InMemoryBorrowingRepository();

studentRepo.Seed(new List<Student>
{
    new Student(1, "Juan Dela Cruz", true),
    new Student(2, "Maria Santos", false) // not allowed to borrow
});

equipmentRepo.Seed(new List<Equipment>
{
    new Equipment(1, "Digital Multimeter", true),
    new Equipment(2, "Oscilloscope", false) // already unavailable
});

var service = new BorrowEquipmentService(studentRepo, equipmentRepo, borrowingRepo);

Console.WriteLine("=== SUCCESS CASE ===");
Console.WriteLine("Student 1 (allowed) borrowing Equipment 1 (available)...");
var successResult = await service.BorrowAsync(studentId: 1, equipmentId: 1);

if (successResult.IsSuccess)
{
    Console.WriteLine($"SUCCESS: Borrowing created. StudentId={successResult.Borrowing!.StudentId}, EquipmentId={successResult.Borrowing.EquipmentId}, Status={successResult.Borrowing.Status}");
}
else
{
    Console.WriteLine($"FAILED: {successResult.ErrorMessage}");
}

Console.WriteLine();
Console.WriteLine("=== FAILURE CASE ===");
Console.WriteLine("Student 2 (NOT allowed) trying to borrow Equipment 1...");
var failureResult = await service.BorrowAsync(studentId: 2, equipmentId: 1);

if (failureResult.IsSuccess)
{
    Console.WriteLine("Unexpectedly succeeded.");
}
else
{
    Console.WriteLine($"FAILED (as expected): {failureResult.ErrorMessage}");
}

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();