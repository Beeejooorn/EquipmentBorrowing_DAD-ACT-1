# Equipment Borrowing System

A small C#/.NET solution demonstrating a layered application structure (Domain / Application / Infrastructure) for a campus equipment borrowing scenario. Built for ITSD 81 Laboratory Activity 1.

---

## 1. Solution Structure

- **Domain** — Contains the core concepts of the problem itself: `Student`, `Equipment`, `Borrowing`, and `BorrowingStatus`. These classes hold data and simple state-changing behavior (e.g. `MarkAsUnAvailable()`, `MarkAsReturned()`), but know nothing about how they are saved, fetched, or displayed.
- **Application** — Contains the use cases and coordination logic. This includes the repository interfaces (`IStudentRepository`, `IEquipmentRepository`, `IBorrowingRepository`) that describe *what* data operations the application needs, and `BorrowEquipmentService`, which coordinates domain objects and repositories to execute the "Borrow Equipment" use case.
- **Infrastructure** — Contains the concrete, technical implementations of the repository interfaces. For this activity, that means simple in-memory repositories (`InMemoryStudentRepository`, `InMemoryEquipmentRepository`, `InMemoryBorrowingRepository`) that store data in C# `List<T>` collections instead of a real database.
- **Tests** — Reserved for automated tests of application/domain behavior (initial project structure only, per activity scope).
- **ConsoleDemo** — A minimal runnable console program that wires everything together and demonstrates one successful and one failed borrow request.

---

## 2. Dependency Direction

```text
      ConsoleDemo
          │
          ▼
     Application
       │      ▲
       ▼      │
     Domain   │
              │
     Infrastructure
```

- **ConsoleDemo** depends on **Application** (to call `BorrowEquipmentService`) and on **Infrastructure** (to construct the concrete in-memory repositories).
- **Application** depends on **Domain** (it uses `Student`, `Equipment`, `Borrowing` directly) but does **not** depend on Infrastructure — it only knows about the repository *interfaces* it defines itself.
- **Infrastructure** depends on both **Domain** (to store domain objects) and **Application** (to implement its repository interfaces).
- **Domain** depends on nothing else in the solution — it is the most stable, independent layer.

---

## 3. Use Case Mapping

```text
Actor: Student
Use Case: Borrow Equipment
Application Service: BorrowEquipmentService.BorrowAsync
Domain Objects Used: Student, Equipment, Borrowing, BorrowingStatus
Repository Interfaces Used: IStudentRepository, IEquipmentRepository, IBorrowingRepository
Infrastructure Implementations Used: InMemoryStudentRepository, InMemoryEquipmentRepository, InMemoryBorrowingRepository
```

---

## 4. Reflection

**1. Why should the application service depend on a repository interface instead of directly depending on a database implementation?**

Depending on an interface means `BorrowEquipmentService` only needs to know *what* operations are available (e.g. "get a student by id"), not *how* they're carried out. This keeps the service usable regardless of where data actually lives — in-memory today, a real database later — without changing a single line of the service itself. It also makes the service easier to test, since a fake/in-memory repository can stand in for a real one.

**2. Which parts of your current solution could remain unchanged if SQLite were added later?**

The Domain layer (`Student`, `Equipment`, `Borrowing`, `BorrowingStatus`) and the Application layer (the repository interfaces and `BorrowEquipmentService`) would not need to change at all. Only the Infrastructure layer would change — the in-memory repositories would be replaced (or supplemented) with new classes that implement the same interfaces using SQLite/Entity Framework Core underneath.

**3. Which project would eventually contain Avalonia Views?**

None of the current four — a new UI project (e.g. `EquipmentBorrowing.UI` or similar, using Avalonia) would be added alongside them, referencing Application (and possibly Infrastructure, for startup/dependency wiring) the same way ConsoleDemo does now.

**4. Should an Avalonia button directly execute database queries? Why or why not?**

No. A button's click handler should call into the Application layer (e.g. `BorrowEquipmentService.BorrowAsync`), the same way `Program.cs` does in this activity. If UI code executed database queries directly, business rules (like the six borrowing checks) would end up duplicated or bypassed across every screen that needed them, and the UI would become tightly coupled to a specific database technology — defeating the whole purpose of separating these layers.

**5. What part of your implementation represents the actual business operation requested by the actor?**

`BorrowEquipmentService.BorrowAsync` is the actual business operation. It is the one place where all the real rules of "Borrow Equipment" — does the student exist, are they allowed to borrow, does the equipment exist and is it available, has the student hit their borrowing limit, and finally creating the record — are expressed and enforced.

---

## Demonstration

Run the `EquipmentBorrowing.ConsoleDemo` project (`F5` or `dotnet run`) to see:
- **Success case:** Student 1 (allowed to borrow) borrows Equipment 1 (available) → borrowing is created.
- **Failure case:** Student 2 (not allowed to borrow) attempts to borrow Equipment 1 → request is rejected with an explanation.

"Note: Domain models and repository interfaces were part of earlier local work, committed together during service implementation"