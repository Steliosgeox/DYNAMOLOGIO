# Implementation Status: ΔΥΝΑΜΟΛΟΓΙΟ (docs/IMPLEMENTATION-STATUS.md)

| Subsystem / Feature | Current Status | Evidence | Remaining Gaps / Remediation Plan |
|---|---|---|---|
| **Domain Lifecycle & Intervals** | **PARTIAL** | `StatusIntervalMath.cs`, `StatusEngine.cs` | Fix historical archive bug (`AUD-001`), remove silent invalid date repair (`AUD-003`), fix returning-today calculation (`AUD-004`). |
| **Strength Calculation Engine** | **PARTIAL** | `StrengthCalculationEngine.cs` | Fix civilian category fallthrough (`AUD-006`), integrate `IClock` (`AUD-016`). |
| **Conflict Detection Engine** | **IMPLEMENTED** | `ConflictEngine.cs` | Wire service-assignment conflict checks, validate dates without pre-mutation. |
| **LiteDB Persistence Layer** | **PARTIAL** | `LiteDbContext.cs`, `LiteDbUnitOfWork.cs` | Implement real atomic transactions/rollback (`AUD-005`), database lifecycle coordination on backup/restore. |
| **Audit Trail System** | **PARTIAL** | `AuditService.cs`, `AuditEvent.cs` | Remove blanket exception catch (`AUD-010`), create dedicated History view (`AUD-021`). |
| **Excel Template Preservation** | **PARTIAL** | `NpoiTemplateWriter.cs` | Enforce SHA-256 hash checks and halt export on mismatch (`AUD-009`). Remove synthetic production fallback. |
| **Excel Import Wizard** | **PARTIAL** | `ExcelImportPipeline.cs` | Add unresolved rank/unit resolution queue, prevent silent default mappings, add field diff preview. |
| **Desktop UI / UX System** | **PARTIAL** | `Dynamologio.App` | Full redesign: Remove all emoji (`AUD-014`), split god ViewModels (`AUD-013`), create vector icons, rebuild Absences workstation (`AUD-008`), Dashboard overview (`AUD-018`), Person Editor dialog (`AUD-002`). |
| **Unit & Integration Tests** | **PARTIAL** | `DynamologioTests.cs` (13 tests) | Remove machine-specific hardcoded paths (`AUD-011`), expand to 40+ comprehensive tests covering lifecycle, rollback, boundary math, and template verification. |
| **Installer & Release Packaging** | **PARTIAL** | `installer/setup.iss` | Switch to Release build output (`AUD-012`), clean unauthorized branding. |
| **Documentation & Assumptions** | **PARTIAL** | `docs/` | Correct unconfirmed assumptions (`AUD-023`), update README with transparent status. |
