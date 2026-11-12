# Lab 4 – Use Case Suite (14 required cases)

This suite is adapted from the supplied *ITSD 81 Lab Activity 4 Use Case Suite*. All the required fields, actor steps, branches, business rules and acceptance outcomes are kept. The wording now names the real screens, buttons and messages of this application. One row was added, **Implemented by**, which shows the code path; [`traceability-matrix.md`](traceability-matrix.md) links each case to its evidence.

Common to every case: the supporting system is **PostgreSQL**, validation is part of the case (no `<<include>>`/`<<extend>>` relationships), and all cases follow business rules BR-01 to BR-19. Dates are local calendar dates (`DateOnly`), and the checkout date is always today.

**Contents:** [UC-01](#uc-01--view-equipment-catalog) · [UC-02](#uc-02--find-available-equipment) · [UC-03](#uc-03--borrow-equipment) · [UC-04](#uc-04--view-active-borrowings) · [UC-05](#uc-05--return-equipment) · [UC-06](#uc-06--check-out-multiple-equipment-items) · [UC-07](#uc-07--view-student-borrowing-history) · [UC-08](#uc-08--view-equipment-borrowing-history) · [UC-09](#uc-09--search-filter-and-sort-borrowings) · [UC-12](#uc-12--register-student) · [UC-13](#uc-13--update-student-information) · [UC-14](#uc-14--change-student-borrowing-eligibility) · [UC-15](#uc-15--register-equipment) · [UC-16](#uc-16--update-equipment-information)


## UC-01 — View Equipment Catalog

| Field | Specification |
|---|---|
| Use Case ID | UC-01 |
| Use Case Name | View Equipment Catalog |
| Scope | Equipment catalog |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff opens the **Equipment Catalog** screen (it is also the first screen when the app starts). |
| Preconditions | Application is running. |
| Success Guarantee | Current equipment codes, names, descriptions and availability are displayed; no records change. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff opens the Equipment Catalog screen.<br>2. System retrieves all equipment from PostgreSQL (`EquipmentQueryService.GetAllEquipmentAsync`).<br>3. System displays each item's code, name, description and Available/Borrowed status. |
| Alternative Flows | 2a. No equipment: system shows the empty state "The equipment catalog is empty." |
| Exception Flows | 2e. Database unavailable: system shows "Could not load data: the PostgreSQL database cannot be reached…" with a **Retry** button. Staff can retry after the database is back. |
| Business Rules | BR-05–07, BR-10, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). |
| Includes / Extends | None required. |
| Acceptance Criteria | Catalog matches database records, including availability; empty and database-error states are distinguishable. See A01. |
| Open Issues | None. |
| Implemented by | `EquipmentView` → `EquipmentViewModel.LoadAsync` → `EquipmentQueryService` → `IEquipmentRepository.GetAllAsync` (`EfEquipmentRepository`, `AsNoTracking`). |

## UC-02 — Find Available Equipment

| Field | Specification |
|---|---|
| Use Case ID | UC-02 |
| Use Case Name | Find Available Equipment |
| Scope | Equipment catalog |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff ticks **Show available equipment only** on the Equipment Catalog screen. |
| Preconditions | Application is running. |
| Success Guarantee | Only currently available items are shown; no records change. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff ticks "Show available equipment only".<br>2. System retrieves the current equipment state from PostgreSQL (`GetAvailableEquipmentAsync`).<br>3. System displays only the available items. |
| Alternative Flows | 3a. No items available: system shows the empty result "No equipment is available right now." |
| Exception Flows | 2e. Database failure: system shows the load error with a **Retry** button. |
| Business Rules | BR-10, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). |
| Includes / Extends | None required. |
| Acceptance Criteria | Available items appear, unavailable items do not, and no-match/error states differ. See A02. |
| Open Issues | None. |
| Implemented by | `EquipmentViewModel` (`ShowAvailableOnly`) → `EquipmentQueryService.GetAvailableEquipmentAsync` → `IEquipmentRepository.GetAvailableAsync`. |

## UC-03 — Borrow Equipment

| Field | Specification |
|---|---|
| Use Case ID | UC-03 |
| Use Case Name | Borrow Equipment |
| Scope | Borrowing |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff clicks **Borrow Equipment** in the Borrow Equipment box. |
| Preconditions | Application is running; staff can select a student and an item. |
| Success Guarantee | One active borrowing and the item's unavailable state are saved together in one transaction. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff selects a student, an item and an expected return date (checkout date is today's local date).<br>2. System opens a transaction and re-reads the student, the item and the student's active count, then checks eligibility, the limit of 3, availability and the date.<br>3. System saves the active borrowing and marks the item unavailable, then commits.<br>4. System shows a green confirmation and reloads the catalog. |
| Alternative Flows | 2a. Missing, restricted, over-limit, unavailable or invalid-date request: system shows an orange message with the reason and "Nothing was saved." |
| Exception Flows | 2e. Stale record or conflict (for example the item was borrowed on another computer a moment ago): PostgreSQL's unique index or the serializable transaction stops it; system says nothing was saved and reloads.<br>3e. Database failure: system reports "Nothing was saved", or "result is UNKNOWN" if the connection was lost during commit (BR-18). |
| Business Rules | BR-08–11, BR-14–16, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). |
| Includes / Extends | None required. |
| Acceptance Criteria | Valid single issue persists; each invalid condition is rejected without changes; outcome survives restart. See A03. |
| Open Issues | None. |
| Implemented by | `EquipmentViewModel.BorrowCommand` → `BorrowEquipmentService.BorrowAsync` → `IUnitOfWork` (serializable transaction) → repositories. |

## UC-04 — View Active Borrowings

| Field | Specification |
|---|---|
| Use Case ID | UC-04 |
| Use Case Name | View Active Borrowings |
| Scope | Borrowing |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff opens the **Active Borrowings** screen. |
| Preconditions | Application is running. |
| Success Guarantee | Only active records with their student and equipment are displayed; nothing changes. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff opens Active Borrowings.<br>2. System retrieves active records together with student and equipment (one joined query).<br>3. System displays borrowing #, student number and name, equipment code and name, checkout and due date. |
| Alternative Flows | 3a. No active records: system shows "There are no active borrowings." |
| Exception Flows | 2e. Database failure: system shows the load error with a **Retry** button. |
| Business Rules | BR-14, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). |
| Includes / Extends | None required. |
| Acceptance Criteria | Active records and related identities are correct; returned records are excluded; empty/error states differ. See A04. |
| Open Issues | None. |
| Implemented by | `BorrowingsView` → `BorrowingsViewModel.LoadAsync` → `BorrowingQueryService.GetActiveBorrowingsAsync` → `IBorrowingRepository.GetActiveRecordsAsync`. |

## UC-05 — Return Equipment

| Field | Specification |
|---|---|
| Use Case ID | UC-05 |
| Use Case Name | Return Equipment |
| Scope | Borrowing |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff selects an active borrowing, clicks **Return Selected Equipment…** and confirms with **Yes, return it**. |
| Preconditions | An active borrowing can be selected. |
| Success Guarantee | The same borrowing row becomes Returned with today's date and the item becomes available; history is kept. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff selects an active borrowing and confirms the return.<br>2. System re-reads the borrowing inside a transaction and checks it is still Active and that today is not before the checkout date.<br>3. System marks the borrowing Returned (ReturnedAt = today) and the item available, then commits.<br>4. System confirms and reloads the list. |
| Alternative Flows | 2a. Already returned: system rejects the repeat ("Borrowing #1 was already returned on 2026-10-27. Nothing was changed."). |
| Exception Flows | 2e. Missing or stale record: system reports that the borrowing no longer exists and reloads.<br>3e. Database failure: system reports failure or unknown outcome under BR-18. |
| Business Rules | BR-10, BR-13–16, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). |
| Includes / Extends | None required. |
| Acceptance Criteria | Return persists across restart, equipment is available, history remains, and repeat return is rejected. See A05. |
| Open Issues | None. |
| Implemented by | `BorrowingsViewModel` (Ask/Confirm/Cancel return) and `BorrowingSearchViewModel.ReturnSelectedCommand` → `ReturnEquipmentService.ReturnAsync`. |

## UC-06 — Check Out Multiple Equipment Items

| Field | Specification |
|---|---|
| Use Case ID | UC-06 |
| Use Case Name | Check Out Multiple Equipment Items |
| Scope | Borrowing |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff clicks **Check Out All Items** on the Multi-Item Checkout screen. |
| Preconditions | Application is running; student and equipment can be selected. |
| Success Guarantee | One active borrowing per selected item and all the availability changes are committed together. |
| Failure Guarantee | A known failed or rejected transaction leaves no changes; if the commit acknowledgement is lost, the app says the outcome is UNKNOWN until staff refresh and check. There is never a partial commit. |
| Main Success Scenario | 1. Staff selects a student, adds at least two items to the checkout list and picks one expected return date.<br>2. System opens one serializable transaction and re-checks the student, the active count + number of items (max 3), every item's availability and the date.<br>3. System writes one borrowing per item, marks each item unavailable and commits everything at once.<br>4. System confirms ("…issued to Ana Reyes. Expected return: …"), clears the list and reloads. |
| Alternative Flows | 2a. Duplicate item, fewer than two items, invalid date, restricted student, limit exceeded, or an unavailable/missing item: system rejects with the reason and "Nothing was saved." The list stays so staff can correct it. |
| Exception Flows | 2e. Stale or conflicting state: the database constraint or serializable transaction stops it; system rejects and reloads.<br>3e. Known write failure: the transaction is rolled back; system reports "…was rolled back. NOTHING was saved." (shown with the test seam "Simulate a failure AFTER the first item is written").<br>3f. Commit outcome unknown: system follows BR-18 (shown with the test seam "Simulate a lost connection right after COMMIT"). |
| Business Rules | BR-08–12, BR-14–16, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). The two test-seam checkboxes are only for demonstrations on a disposable database and are off by default. |
| Includes / Extends | None required. |
| Acceptance Criteria | Two valid items produce two persisted active records; each invalid request produces none; controlled in-transaction failure leaves no partial changes. See A06. |
| Open Issues | None. |
| Implemented by | `CheckoutViewModel.CheckOutCommand` → `BorrowEquipmentService.CheckOutMultipleAsync` (shares `IssueAsync` with UC-03) → `IUnitOfWork`. |

## UC-07 — View Student Borrowing History

| Field | Specification |
|---|---|
| Use Case ID | UC-07 |
| Use Case Name | View Student Borrowing History |
| Scope | Student history |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff selects a student on the **Students** screen; the history panel loads. |
| Preconditions | Application is running; student selection is available. |
| Success Guarantee | Only the selected student's active and returned records, with their equipment, are shown; no data changes. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff selects a student.<br>2. System asynchronously retrieves that student's borrowings with equipment ("Loading history…" is shown).<br>3. System displays the student's name and number, then each borrowing's #, equipment code and name, checkout, due, returned date (when present) and status. |
| Alternative Flows | 3a. No history: "Eve Garcia has no borrowing history yet."<br>1a. Staff selects another student: the panel is replaced with that student's data (older, slower answers are ignored). |
| Exception Flows | 2e. Missing/stale student: "This student no longer exists." (a not-found state, different from empty), staff can reselect; database failure: error message, staff can click **Refresh History**. |
| Business Rules | BR-14, BR-18–19 |
| Special Requirements | PostgreSQL-backed asynchronous loading (`async`/`await`, the window stays responsive), with separate loading, empty, not-found and error states. |
| Includes / Extends | None required. |
| Acceptance Criteria | Two students' distinguishable records do not mix; active, returned and empty results are accurate; loading is responsive. See A07. |
| Open Issues | None. |
| Implemented by | `StudentsViewModel.LoadHistoryAsync` → `BorrowingQueryService.GetStudentHistoryAsync` → `IBorrowingRepository.GetStudentHistoryAsync`. |

## UC-08 — View Equipment Borrowing History

| Field | Specification |
|---|---|
| Use Case ID | UC-08 |
| Use Case Name | View Equipment Borrowing History |
| Scope | Equipment history |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff selects an item on the **Manage Equipment** screen; the history panel loads. |
| Preconditions | Application is running; equipment selection is available. |
| Success Guarantee | Only the selected item's active and returned records, with their students, are shown; no data changes. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff selects an item.<br>2. System asynchronously retrieves the item's borrowings with students.<br>3. System displays the item's code and name, then each borrowing's #, student number and name, checkout, due, returned date (when present) and status. |
| Alternative Flows | 3a. No history: "Speaker 01 has never been borrowed."<br>1a. Staff selects another item: the panel is replaced. |
| Exception Flows | 2e. Missing/stale item: "This equipment no longer exists." (not-found state), staff can reselect; database failure: error message, staff can click **Refresh History**. |
| Business Rules | BR-14, BR-18–19 |
| Special Requirements | PostgreSQL-backed asynchronous loading (`async`/`await`, the window stays responsive), with separate loading, empty, not-found and error states. |
| Includes / Extends | None required. |
| Acceptance Criteria | Two items' distinguishable records do not mix; active, returned and empty results are accurate; loading is responsive. See A08. |
| Open Issues | None. |
| Implemented by | `EquipmentManagementViewModel.LoadHistoryAsync` → `BorrowingQueryService.GetEquipmentHistoryAsync` → `IBorrowingRepository.GetEquipmentHistoryAsync`. |

## UC-09 — Search, Filter, and Sort Borrowings

| Field | Specification |
|---|---|
| Use Case ID | UC-09 |
| Use Case Name | Search, Filter, and Sort Borrowings |
| Scope | Borrowing queries |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff opens **Search Borrowings** and clicks **Search** (or **Clear**). |
| Preconditions | Application is running. |
| Success Guarantee | Only matching records are shown in the requested order; no data changes. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff types search text (part of a student name, student number or equipment name) and/or picks Status (All/Active/Returned), Sort by (Checkout date/Expected return date) and Direction.<br>2. System trims the text and asynchronously runs one database query with all criteria.<br>3. System displays student, equipment, dates and status in the chosen order (borrowing # breaks ties) and the number found.<br>4. Staff may click **Clear**. |
| Alternative Flows | 1a. Criteria may be used separately or together.<br>3a. No matches: "No borrowings match these criteria. Try other words or click Clear."<br>4a. Clear resets all criteria and shows the full list. |
| Exception Flows | 2e. Database failure: error message with **Retry**. |
| Business Rules | BR-17–19 |
| Special Requirements | PostgreSQL-backed asynchronous loading (`async`/`await`, the window stays responsive), with separate loading, empty, not-found and error states. |
| Includes / Extends | None required. |
| Acceptance Criteria | Search each specified field, each status, both sort fields/directions, combined criteria and reset against known data. See A09. |
| Open Issues | None. |
| Implemented by | `BorrowingSearchViewModel.SearchCommand` → `BorrowingQueryService.SearchBorrowingsAsync(BorrowingSearchCriteria)` → `IBorrowingRepository.SearchAsync`. |

## UC-12 — Register Student

| Field | Specification |
|---|---|
| Use Case ID | UC-12 |
| Use Case Name | Register Student |
| Scope | Student management |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff clicks **Register New Student**. |
| Preconditions | Application is running; the Students screen (registration form) is open. |
| Success Guarantee | One uniquely numbered student with the given details and eligibility = eligible is saved. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff clicks Clear Form, enters student number, full name and optional contact.<br>2. System trims the values and checks required fields and that the number is not used yet (ignoring case and spaces).<br>3. System saves the student.<br>4. System confirms ("Student … was registered and is eligible to borrow.") and shows the student in the list. |
| Alternative Flows | 2a. Blank number/name or duplicate number: system rejects and names the problem; nothing is saved.<br>1a. Contact left blank: stored as empty (NULL). |
| Exception Flows | 3e. Two staff save the same number at once or the database fails: PostgreSQL's unique index `IX_Students_StudentNumberKey` still blocks the duplicate; system reports failure or unknown outcome (BR-18). |
| Business Rules | BR-01–02, BR-16, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). |
| Includes / Extends | None required. |
| Acceptance Criteria | Valid student can be retrieved after restart; empty name/number and duplicate number are rejected. See A12. |
| Open Issues | None. |
| Implemented by | `StudentsViewModel.RegisterCommand` → `StudentManagementService.RegisterStudentAsync` → `IStudentRepository`. |

## UC-13 — Update Student Information

| Field | Specification |
|---|---|
| Use Case ID | UC-13 |
| Use Case Name | Update Student Information |
| Scope | Student management |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff clicks **Save Changes to Selected**. |
| Preconditions | A student exists and can be found (search box or Show All) and selected. |
| Success Guarantee | Allowed changes are saved on the same student Id; borrowing rows and history stay linked. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff searches for and selects a student; the form is filled with the current values.<br>2. Staff edits number, name and/or contact and clicks Save Changes to Selected.<br>3. System trims, validates (required fields, number unique among OTHER students) and saves.<br>4. System confirms ("Details of … were updated.") and refreshes the list. |
| Alternative Flows | 2a. Contact left blank: the stored contact is cleared (NULL).<br>3a. Blank required field or a number used by another student: rejected, nothing changes. |
| Exception Flows | 3e. Student no longer exists, conflicting update or database failure: system reports it and refreshes (BR-18). |
| Business Rules | BR-01–03, BR-14, BR-16, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). Eligibility is not part of this form; it uses UC-14. |
| Includes / Extends | None required. |
| Acceptance Criteria | Locate and update a student, restart, and verify changed details and unchanged linked borrowings; reject invalid and duplicate edits. See A13. |
| Open Issues | None. |
| Implemented by | `StudentsViewModel.SaveChangesCommand` → `StudentManagementService.UpdateStudentAsync` → `Student.UpdateDetails`. |

## UC-14 — Change Student Borrowing Eligibility

| Field | Specification |
|---|---|
| Use Case ID | UC-14 |
| Use Case Name | Change Student Borrowing Eligibility |
| Scope | Student management |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff clicks **Permit Borrowing** or **Restrict Borrowing**. |
| Preconditions | A student exists and can be selected. |
| Success Guarantee | The chosen eligibility is saved; existing active borrowings and history are unchanged. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff finds and selects a student.<br>2. Staff clicks Permit Borrowing or Restrict Borrowing.<br>3. System saves and confirms the new status ("… is now restricted from borrowing. Existing active borrowings are not affected and can still be returned."). |
| Alternative Flows | 2a. The student already has that status: "… is already restricted. No changes were made." (NoChange result).<br>3a. A restricted student with active loans can still return them (UC-05 does not check eligibility). |
| Exception Flows | 3e. Student missing/stale or database failure: system reports failure or unknown outcome (BR-18). |
| Business Rules | BR-04, BR-08, BR-13–14, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). |
| Includes / Extends | None required. |
| Acceptance Criteria | Restriction persists and blocks a new issue without changing active loans; return remains possible; permitting allows an otherwise valid request. See A14. |
| Open Issues | None. |
| Implemented by | `StudentsViewModel.PermitCommand`/`RestrictCommand` → `StudentManagementService.ChangeEligibilityAsync` → `Student.SetEligibility`. |

## UC-15 — Register Equipment

| Field | Specification |
|---|---|
| Use Case ID | UC-15 |
| Use Case Name | Register Equipment |
| Scope | Equipment management |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff clicks **Register New Equipment**. |
| Preconditions | Application is running; the Manage Equipment screen (registration form) is open. |
| Success Guarantee | One uniquely coded item is saved as Available with no active borrowing. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff clicks Clear Form, enters equipment code, name and optional description/type.<br>2. System trims and checks required fields and that the code is not used yet (ignoring case and spaces).<br>3. System saves the item as available.<br>4. System confirms ("… was registered and is available for borrowing.") and shows it in the list. |
| Alternative Flows | 2a. Blank code/name or duplicate code: rejected with the reason; nothing is saved.<br>1a. Description left blank: stored as empty (NULL). |
| Exception Flows | 3e. Concurrent duplicate or database failure: the unique index `IX_Equipment_EquipmentCodeKey` blocks the duplicate; system reports failure or unknown outcome (BR-18). |
| Business Rules | BR-05–06, BR-10, BR-16, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). |
| Includes / Extends | None required. |
| Acceptance Criteria | Valid item appears as available after restart; missing required fields and duplicate code are rejected. See A15. |
| Open Issues | None. |
| Implemented by | `EquipmentManagementViewModel.RegisterCommand` → `EquipmentManagementService.RegisterEquipmentAsync`. |

## UC-16 — Update Equipment Information

| Field | Specification |
|---|---|
| Use Case ID | UC-16 |
| Use Case Name | Update Equipment Information |
| Scope | Equipment management |
| Level | User Goal |
| Primary Actor | Laboratory Staff / Equipment Custodian |
| Stakeholders and Interests | Custodian: accurate, understandable workflow; students: correct records and preserved history. |
| Trigger | Staff clicks **Save Changes to Selected**. |
| Preconditions | An item exists and can be found and selected. |
| Success Guarantee | Allowed descriptive changes are saved on the same item Id; availability and borrowing history stay the same. |
| Failure Guarantee | A rejected or known failed operation preserves existing data; an unknown commit outcome is handled under BR-18 (the app says the result is UNKNOWN and asks staff to refresh before trying again). |
| Main Success Scenario | 1. Staff searches for and selects an item.<br>2. Staff edits code, name and/or description and clicks Save Changes to Selected.<br>3. System trims, validates and saves.<br>4. System confirms ("Details of … were updated. Availability was not changed."). |
| Alternative Flows | 2a. Description cleared: stored as empty (NULL).<br>3a. Blank required field or a code used by another item: rejected, nothing changes.<br>2b. Availability cannot be edited here: the form has no availability field (the panel says it changes only through borrowing and return) and `UpdateEquipmentAsync` has no availability parameter. |
| Exception Flows | 3e. Item missing/stale, conflicting update or database failure: system reports it and refreshes (BR-18). |
| Business Rules | BR-05–07, BR-10, BR-14, BR-16, BR-18–19 |
| Special Requirements | PostgreSQL persistence; meaningful feedback and separation of responsibilities (View → ViewModel → Application Service → Repository → EF Core → PostgreSQL). |
| Includes / Extends | None required. |
| Acceptance Criteria | Edit and restart preserves new details and linked history/availability; duplicate and blank required edits are rejected. See A16. |
| Open Issues | None. |
| Implemented by | `EquipmentManagementViewModel.SaveChangesCommand` → `EquipmentManagementService.UpdateEquipmentAsync` → `Equipment.UpdateDetails`. |
