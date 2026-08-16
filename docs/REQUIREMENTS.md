# Requirements Traceability Matrix (docs/REQUIREMENTS.md)

| Req ID | Category | Requirement Description | Architecture / Implementation | Test Coverage |
| :--- | :--- | :--- | :--- | :--- |
| **REQ-001** | Platform | Must execute 100% offline on Windows 7 SP1 x86/x64, Windows 10 x64, Windows 11 x64. | .NET Framework 4.7.2, zero telemetry, zero cloud/web dependencies. | Win7 Compatibility verification. |
| **REQ-002** | Domain | Personnel state must be time-dependent; presence derived at query time without mutable flags. | `StatusEvent`, `[StartAt, EndAtExclusive)` intervals, `StatusEngine.cs`. | `StatusEngineTests.cs` |
| **REQ-003** | Domain | Expired absence must automatically cease affecting current strength with zero manual user actions. | Query-time timestamp evaluation in `StatusEngine`. | `AutomaticReturnTests.cs` |
| **REQ-004** | Domain | Reconstruct exact unit strength, present/absent personnel, and duties for any historical timestamp. | `IStrengthCalculator.CalculateSnapshot(DateTime asOf)`. | `HistoricalStrengthTests.cs` |
| **REQ-005** | Domain | Strength mathematical invariant: $\text{Present} + \text{Absent} = \text{Active Strength}$ (for active personnel). | `StrengthCalculationEngine.cs`. | `StrengthInvariantTests.cs` |
| **REQ-006** | Validation | Conflict engine must detect and prevent overlapping absences, invalid dates, and archived mutations. | `ConflictEngine.cs` classifying Error / Warning / Info. | `ConflictEngineTests.cs` |
| **REQ-007** | Persistence | Embedded local storage using LiteDB 5.x with repository abstractions and schema migrations. | `LiteDbContext`, `Repository<T>`, `MigrationRunner`. | `RepositoryIntegrationTests.cs` |
| **REQ-008** | Backup | Automatic daily backup on first launch and manual backup/restore with SHA-256 verification. | `BackupService.cs`, manifest verification. | `BackupRestoreTests.cs` |
| **REQ-009** | Audit | Comprehensive audit trail logging user, timestamp, entity, before/after values, and batch ID. | `AuditService.cs`, `AuditEvent` collection. | `AuditServiceTests.cs` |
| **REQ-010** | Excel Engine | Read/write `.xls` and `.xlsx` via NPOI without requiring Microsoft Office or COM Interop. | `NpoiWorkbookAnalyzer`, `NpoiTemplateWriter`. | `ExcelEngineTests.cs` |
| **REQ-011** | Template Pres. | Preserve original styles, formulas, merged cells, page setup, and headers/footers in official Excel output. | `IExcelTemplateWriter.PopulateTemplate(...)`. | `GoldenTemplateComparisonTests.cs` |
| **REQ-012** | Template Integrity | Calculate SHA-256 hash for templates; halt export if template is mutated without updated mapping. | `ReportTemplateManager.cs`. | `TemplateIntegrityTests.cs` |
| **REQ-013** | Import Pipeline | Multi-stage import wizard (Analyze -> Map -> Validate -> Preview -> Commit) with provenance tracking. | `ExcelImportPipeline.cs`, `ImportBatch`. | `ImportPipelineTests.cs` |
| **REQ-014** | Reporting | Support official reports (Δυναμολόγιο, Κατάσταση Απόντων, Υπηρεσίες) with Print, Excel, and PDF exports. | `DynamologioReportService.cs`. | `ReportingTests.cs` |
| **REQ-015** | UI / UX | High-density WPF desktop workstation interface in `el-GR` with keyboard shortcuts and virtualized DataGrids. | `Dynamologio.App` Views & ViewModels, `Culture = el-GR`. | UI manual & integration validation. |
| **REQ-016** | Diagnostic | Admin tool to export diagnostic logs, system info, and anonymized metadata without external network transfer. | `DiagnosticPackageService.cs`. | `DiagnosticPackageTests.cs` |
