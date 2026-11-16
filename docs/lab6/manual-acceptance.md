# Lab 6 manual acceptance checks (MA6-01 and MA6-02)

Both checks were done by hand on the real desktop app, using the Lab 5 acceptance database (`equipmentborrowing_lab5_acceptance`, 4 students, 7 equipment items, 4 borrowings). The API was started from the Release build on http://localhost:5087. Only a few screenshots are kept, as the handout asks.

Done on 2026-11-17 by Hervy Bjorn C. Tanamal. Screenshots are in the `evidence` folder.

## MA6-01 - Desktop responsiveness

What we did: we locked the Equipment table in PostgreSQL (`BEGIN; LOCK TABLE "Equipment" IN ACCESS EXCLUSIVE MODE;`) so the API had to wait, clicked Refresh in the desktop, then released the lock with `ROLLBACK;`. After that we stopped the API process, clicked Refresh, started the API again and clicked Refresh.

| What we checked | What we saw | Result | Screenshot |
|---|---|---|---|
| Starting data | 4 students, 7 equipment, 4 borrowings | OK | [ma6-01-1](evidence/ma6-01-1-starting-counts.png) |
| Loading state while the API is waiting | "Loading equipment from the database..." with a progress bar, Refresh greyed out | Passed | [ma6-01-2](evidence/ma6-01-2-loading-state.png) |
| Window still usable while waiting | On the second try, the window could be moved and the menu buttons still worked (reported by the student, no screenshot because a screenshot cannot show it) | Passed | none |
| Delayed response ends with a clear message | After the 30 second wait the app showed "The API did not respond in time. Try refreshing this screen." with a Retry button | Passed | [ma6-01-3](evidence/ma6-01-3-timeout-message.png) |
| API stopped | "Cannot reach the API. Start the API, then refresh this screen." with a Retry button | Passed | [ma6-01-4](evidence/ma6-01-4-api-stopped-message.png) |
| API started again | After Refresh the catalog showed all 7 items again | Passed | [ma6-01-5](evidence/ma6-01-5-api-restored.png) |
| No unintended writes | Counts after everything: 4 students, 7 equipment, 4 borrowings (unchanged) | Passed | [ma6-01-6](evidence/ma6-01-6-counts-after.png) |

Notes:
* On the first delayed try the window was not tested for movement, so we repeated it. Only the repeat is counted.
* The "restricted = 2" in the last screenshot is ST-200 (already restricted in the seed data) plus ST-100, which we restricted on purpose in MA6-02.

**Status: Passed.**

## MA6-02 - Independent API and synchronization

| What we checked | What we saw | Result | Screenshot |
|---|---|---|---|
| API answers without the desktop | Opened `http://localhost:5087/api/v1/equipment` in a normal browser tab. It returned the 7 items as JSON. | Passed | [ma6-02-1](evidence/ma6-02-1-api-alone-equipment.png) |
| OpenAPI document matches the API | Opened `http://localhost:5087/openapi/v1.json`. Searching for `operationId` gives 16 matches, the same as the 16 APIs in the handout. The automated `OpenApiContractTests` also check this. | Passed | [ma6-02-2](evidence/ma6-02-2-openapi-16-operations.png) |
| Change from an independent client | In PowerShell: `Invoke-RestMethod -Method Patch -Uri "http://localhost:5087/api/v1/students/1/eligibility" -ContentType "application/json" -Body '{"isEligible": false}'`. The reply showed ST-100 with `isEligible : False`. | Passed | (command output, not kept) |
| Desktop shows the change | After restarting the API and desktop, the Students screen shows ST-100 Ada Cruz as Restricted | Passed | [ma6-02-3](evidence/ma6-02-3-desktop-shows-restriction.png) |
| Desktop only uses HTTP | `EquipmentBorrowing.Desktop.csproj` has one project reference, to `EquipmentBorrowing.Application` (which only references Domain). No Npgsql, EntityFramework or DbContext text in any `.cs` file of the Desktop project. `DesktopApi.cs` and `App.axaml.cs` use `HttpClient`. | Passed | [ma6-02-4](evidence/ma6-02-4-source-inspection.png) |

Notes:
* We first guessed a wrong route for restricting (equipment instead of student). The real route is `PATCH /api/v1/students/{id}/eligibility`, found by reading `Program.cs`.
* The desktop was reopened after the change, which also restarts the API. The restriction was still there because it is saved in PostgreSQL, not in the desktop.
* Our first source check used `Select-String -Recurse`, which does not exist in Windows PowerShell 5.1, so we used `Get-ChildItem -Recurse | Select-String` instead.

**Status: Passed.**
