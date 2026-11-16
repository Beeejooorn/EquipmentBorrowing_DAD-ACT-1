# Activity 5 OpenAPI contract audit

Compared `EquipmentBorrowing/src/EquipmentBorrowing.Api/OpenApi/v1.json` (OpenAPI 3.0.3) with Section 2 of the supplied *API and Acceptance Reference* on 2026-11-16. The published document contains exactly 16 operations on 11 paths. Each required method, route, success response schema, and listed 400/404/409 failure status appears in the document. All operations also document safe 500 errors.

| API | Published method and path | Success | Request / response schema |
|---|---|---:|---|
| 01 | GET `/api/v1/equipment` | 200 | optional `available`; Equipment array |
| 02 | GET `/api/v1/students` | 200 | optional `search`; Student array |
| 03 | GET `/api/v1/students/{id}` | 200 | Student |
| 04 | POST `/api/v1/students` | 201 | StudentInput → Student |
| 05 | PUT `/api/v1/students/{id}` | 200 | StudentInput → Student |
| 06 | PATCH `/api/v1/students/{id}/eligibility` | 200 | EligibilityInput → Student |
| 07 | GET `/api/v1/equipment/{id}` | 200 | Equipment |
| 08 | POST `/api/v1/equipment` | 201 | EquipmentInput → Equipment |
| 09 | PUT `/api/v1/equipment/{id}` | 200 | EquipmentInput → Equipment |
| 10 | POST `/api/v1/borrowings` | 201 | BorrowingInput → Borrowing |
| 11 | POST `/api/v1/checkout-requests` | 201 | CheckoutInput → CheckoutResult |
| 12 | POST `/api/v1/borrowings/{id}/return` | 200 | empty object → Borrowing |
| 13 | GET `/api/v1/borrowings/{id}` | 200 | BorrowingDetail |
| 14 | GET `/api/v1/borrowings` | 200 | `search`, `status`, `sort`, `direction`; BorrowingDetail array |
| 15 | GET `/api/v1/students/{id}/borrowings` | 200 | BorrowingDetail array |
| 16 | GET `/api/v1/equipment/{id}/borrowings` | 200 | BorrowingDetail array |

Required input properties match the reference: StudentInput has `studentNumber` and `fullName`; EquipmentInput has `equipmentCode` and `name`; BorrowingInput has `studentId`, `equipmentId`, and `expectedReturnDate`; CheckoutInput has `studentId`, `equipmentIds`, and `expectedReturnDate`; EligibilityInput has `isEligible`. The remaining contact, description, and search/filter fields are optional. Student, Equipment, Borrowing, and CheckoutResult publish the expected success fields. BorrowingDetail adds student and equipment display fields.

The desktop sends the reference's empty JSON object `{}` for return. The return operation documents an optional body because the implemented route has no request body binding; both `{}` and no body are accepted. This is a permissive server difference from the reference example. The document lists additional safe 400/500 responses on some reads to cover malformed requests and server failures.

The controlled T01 run retrieved `/openapi/v1.json` over HTTP, confirmed version 3.0.3 and counted 16 operations. The route and schema comparison above inspects the committed machine-readable file; live data behavior is recorded in `acceptance-record.md`.
