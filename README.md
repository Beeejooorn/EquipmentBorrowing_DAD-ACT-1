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


---

## Activity 2: Avalonia UI and MVVM

### 1. Desktop Project

`EquipmentBorrowing.Desktop` is the presentation layer — it displays information, collects user input, and translates user actions into calls on existing Application services. It references `Application` (to call `BorrowEquipmentService`, `ReturnEquipmentService`, and the query services) and `Infrastructure` (only in the composition root, to construct the concrete in-memory repositories). Domain and Application remain completely unaware that Avalonia exists.

### 2. Updated Architecture

```text
Avalonia View
     │
     │ Binding / Command
     ▼
ViewModel
     │
     │ Application Operation
     ▼
Application Service
     │
     ├──────────► Domain
     │
     ▼
Repository Interface
     ▲
     │
Infrastructure Implementation
```

### 3. Borrow Equipment Flow

1. User selects a student, equipment item, and expected return date, then presses **Borrow Equipment**.
2. `EquipmentViewModel.BorrowCommand` runs presentation validation (are all three fields filled?).
3. If valid, it calls `BorrowEquipmentService.BorrowAsync(studentId, equipmentId, expectedReturnDate)`.
4. The service checks all six business rules (student exists/allowed, equipment exists/available, borrowing limit) against the repositories, and either creates the `Borrowing` or returns a failure reason.
5. The ViewModel sets `StatusMessage` from the result and reloads the equipment list so availability updates on screen.

### 4. Return Equipment Flow

1. User selects an active borrowing from the list and presses **Return Equipment**.
2. `BorrowingsViewModel.ReturnCommand` checks a borrowing is actually selected.
3. It calls `ReturnEquipmentService.ReturnAsync(borrowing)`, passing the already-selected `Borrowing` object directly (no re-lookup by ID needed).
4. The service validates it isn't already returned, marks it returned, marks the equipment available again, and saves the equipment update.
5. The ViewModel displays the result and reloads the active borrowings list.

### 5. Architectural Reflection

1. **Why should the View not call a repository directly?** The View would need to know about concrete storage details (in-memory lists, eventually SQL), breaking the separation the whole architecture is built on, and making the UI impossible to test or swap without rewriting screens.
2. **Why should business rules not be implemented in the ViewModel?** Rules like borrowing limits or availability checks would then be duplicated across every screen that needs them, and could drift out of sync. Keeping them in one Application service means one place to fix, one place to trust.
3. **What is the responsibility of the ViewModel?** Hold presentation state (selected items, lists, status messages) and translate user actions into calls on Application services — nothing more.
4. **Why can the existing Application layer work without knowing that Avalonia is being used?** It only depends on repository interfaces and Domain objects — plain C#, no UI framework references. Any UI technology (console, Avalonia, web) can sit on top of it unchanged.
5. **What advantage is gained from registering dependencies in one composition point?** Only `App.axaml.cs` needs to change if repositories are swapped (e.g., for SQLite) — every Service, ViewModel, and View stays untouched because they only ever depend on interfaces.
6. **If the in-memory repository were replaced by SQLite later, which parts of the current interface should remain largely unchanged?** All Views, ViewModels, and Application services — only the repository implementations and the one line in the composition root registering them would change. 