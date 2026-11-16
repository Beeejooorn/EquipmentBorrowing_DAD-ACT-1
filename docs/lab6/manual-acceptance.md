# Lab 6 manual acceptance checks (MA6-01 and MA6-02)

These two checks are manual by design (no UI automation). **Status today: Not Run.** They must be performed by the students on the desktop application and recorded below with at most a few screenshots (the handout asks for concise evidence, not dozens of screenshots). Do not mark either one Passed until it was actually done.

## MA6-01 — Desktop responsiveness

* **Test data:** reset database state S1–S4 / E1–E7 (the Lab 5 controlled seed or the Lab 6 test database).
* **Expected:** the window stays usable while a response is delayed; a loading state is shown; stopping the API gives an understandable "cannot reach the API" message; after the API is started again the screen recovers after Refresh/Retry; no unintended writes.
* **Procedure:** see the click-by-click steps given with the final instructions.

| Observation | Result |
|---|---|
| Delayed HTTP response: loading state visible, window still movable/clickable | Not Run |
| API stopped: message shown, text of the message | Not Run |
| API restored: screen recovers | Not Run |
| Fresh database readback shows no unintended changes | Not Run |

**Status: Not Run.**

## MA6-02 — Independent API and synchronization

* **Expected:** the API runs and answers without the desktop; the published OpenAPI document is consistent with the real API; a change made from an independent HTTP client appears in the desktop after refresh; the desktop project has no database access and talks to the API only through HTTP.
* **Already supported by automation (does not replace the manual check):** `OpenApiContractTests` (16 operations, success codes, routed by the real API) passed in run 6; the desktop project reference list was checked in Lab 5 (no Infrastructure reference).

| Observation | Result |
|---|---|
| API started alone (desktop closed), root URL answers | Not Run |
| OpenAPI document opened and compared with the API list | Not Run |
| Independent client change (for example restrict S1) | Not Run |
| Desktop refresh shows the change | Not Run |
| Source inspection: Desktop `.csproj` has no Infrastructure reference; `DesktopApi` uses `HttpClient` | Not Run |

**Status: Not Run.**
