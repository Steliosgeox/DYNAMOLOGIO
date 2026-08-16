# V3 Reality Check & Forensic Verification (docs/V3-REALITY-CHECK.md)

## 1. Security Findings Verification

| Defect ID | Status | File(s) | Live Code Evidence | Verdict / Remediation Plan |
|---|---|---|---|---|
| **SEC-001** | **CONFIRMED** | `src/Dynamologio.Infrastructure/LiteDb/LiteDbContext.cs:37` | `Filename={_dbPath};Connection=shared` — No password or key is provided. DB file is unencrypted plaintext on disk. | Implement DPAPI-protected master database key generated on first run (`ProtectedData.Protect` under `DataProtectionScope.LocalMachine`) and supply to LiteDB connection string: `Filename={_dbPath};Password={key};Connection=shared`. |
| **SEC-002** | **CONFIRMED** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs:107` | `ZipArchive` writes `dynamologio.db` and `manifest.json` in raw plaintext. | Implement AES-256 backup package encryption with password derivation (PBKDF2) or DPAPI container when exporting/importing backup archives. |
| **SEC-003** | **CONFIRMED** | `src/Dynamologio.App/Views/SettingsView.xaml:31` | Label describes backup as "κρυπτογραφικά υπογεγραμμένο ... (SHA-256)". | SHA-256 is an integrity checksum, not a signature. Update UI/docs to "έλεγχος ακεραιότητας SHA-256" and implement HMAC-SHA256 for authenticated manifests. |
| **SEC-004** | **CONFIRMED** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs:32` | Defaults to `Username = "OPERATOR"`. | Use `Environment.UserName` / Windows identity principal for actor tracking in audit logs. |
| **SEC-005** | **CONFIRMED** | `src/Dynamologio.App/ViewModels/AbsencesViewModel.cs:245` | `_uow.StatusEvents.Insert()` and `_auditService.LogAction()` are separate calls outside a unified transaction. | Encapsulate mutations in transactional application services where business mutation and audit logging commit together or rollback on audit failure. |
| **SEC-006** | **CONFIRMED** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs:203` | `RestoreBackup` extracts file directly over active database without closing/disposing LiteDbContext. | Implement 9-step restore lifecycle: verify -> pre-restore copy -> dispose DB context -> extract to temp -> open/validate -> replace live file -> recreate DB context & rebind ViewModels. |
| **SEC-007** | **CONFIRMED** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs:98` | Backup copies open DB directly. | Use explicit database checkpoint/lock and flush before computing SHA-256 and packaging. |
| **SEC-008** | **CONFIRMED** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs:241` | `PerformDailyAutoBackup()` catches all exceptions silently. | Track last successful backup timestamp in `AppSetting`, log technical failure to crash log, and display degraded status in status bar if backup fails. |
| **SEC-009** | **CONFIRMED** | `src/Dynamologio.App/App.xaml.cs:36` | `args.Handled = true` unconditionally for all dispatcher exceptions. | Differentiate recoverable UI exceptions from fatal database/corruption errors. |
| **SEC-010** | **CONFIRMED** | `src/Dynamologio.App/App.xaml.cs:122` | `app_crash.log` written in ProgramData without rotation or size limit. | Implement bounded log rotation (max 2MB, keep 3 files). |
| **SEC-013** | **CONFIRMED** | `.gitignore` | Did not include defensive ignore patterns for operational files. | Add defensive ignore rules for `*.xlsx`, `*.xls`, `*.pdf`, `*.zip`, `*.bak`, `Backups/`, `Diagnostics/`. |

---

## 2. UI / UX Findings Verification

| Defect ID | Status | File(s) | Live Code Evidence | Verdict / Remediation Plan |
|---|---|---|---|---|
| **UX-001** | **CONFIRMED** | `src/Dynamologio.App/Styles/DesignSystem.xaml` | Missing custom ControlTemplates for DatePicker, ComboBox popup, ScrollBars, TabControl, DataGrid row/cell selection. | Implement full Win7-compatible custom templates for all primary controls. |
| **UX-002** | **CONFIRMED** | `src/Dynamologio.App/Views/MainWindow.xaml:63` | Navigation buttons are generic Buttons calling `NavigateCommand` with no `IsSelected` or `ActiveSection` visual indicator. | Bind navigation item styles to `ActiveSection` to show blue accent marker, dark blue surface, and white highlighted text/icon. |
| **UX-003** | **CONFIRMED** | `src/Dynamologio.App/Views/MainWindow.xaml:59` | Fixed `Width="220"` sidebar rail. | Allow adaptive width with clean layout on 1024x768. |
| **UX-004** | **CONFIRMED** | Multiple XAML files | Emoji found in `DashboardView.xaml` (📊, 📝, 👥, 🛡️), `ReportsView.xaml` (📊, 🖨️), `SettingsView.xaml` (💾, ⚠️, 📦), and `AbsencesViewModel.cs` (⚠️). | Remove 100% of emoji from all production sources. Add automated test `AT_REPO_001_ZeroEmojiInProductionSources`. |
| **UX-005** | **CONFIRMED** | `src/Dynamologio.App/Views/DynamologioView.xaml` | Contains old design, emoji buttons, generic filter card, and raw tables. | Complete structural rebuild of `DynamologioView.xaml` matching hero workstation quality. |
| **UX-006** | **CONFIRMED** | `src/Dynamologio.App/Views/` | DatePicker and ComboBox show Windows classic styling. | Build unified custom templates in `DesignSystem.xaml` / `Inputs.xaml`. |
| **UX-007** | **CONFIRMED** | `src/Dynamologio.App/Styles/DesignSystem.xaml` | DataGrid has no empty-state overlay, no custom row selection style. | Implement styled DataGrid headers, alternating row colors, soft indigo selection, and empty-state overlay. |
| **UX-008** | **CONFIRMED** | `src/Dynamologio.App/Views/` | Large blank DataGrids when collections are empty. | Implement reusable `EmptyStatePresenter` / `EmptyState` template across all views. |
| **UX-009** | **CONFIRMED** | `src/Dynamologio.App/Views/AbsencesView.xaml:173` | Every row had a full red `DangerButton` for "Ακύρωση". | Use quiet row actions (`QuietButton` with trash/cancel vector icon and tooltip). |
| **UX-010** | **CONFIRMED** | `src/Dynamologio.App/ViewModels/` | Routine operations trigger modal `MessageBox.Show`. | Use non-blocking status bar/banner feedback for routine actions. |
| **UX-011** | **CONFIRMED** | `src/Dynamologio.App/Views/PersonnelView.xaml:47` | Search is a plain blank TextBox. | Build SearchBox with embedded search icon, placeholder text, and clear button. |
| **UX-012** | **CONFIRMED** | `src/Dynamologio.App/Views/AbsencesView.xaml:56` | Uses a plain ComboBox for person selection. | Implement type-to-filter searchable PersonPicker control showing Rank, Name, Unit, and ASM. |
| **UX-013** | **CONFIRMED** | Multiple XAML files | `Visibility="{Binding ErrorMessage, Converter={x:Null}}"` — invalid converter syntax. | Replace with proper StringToVisibilityConverter / BooleanToVisibilityConverter. |
| **UX-014** | **CONFIRMED** | `src/Dynamologio.App/Views/PersonnelView.xaml:113` | DataGrid binds `Location` while entity property is `DutyLocation`. | Fixed binding to `DutyLocation`. |
| **UX-018** | **CONFIRMED** | `docs/UX-REVIEW.md` | Claimed 47–49/50 scores without screenshot evidence. | Reset scores to unverified until actual screenshot capture passes. |

---

## 3. Functional Findings Verification

| Defect ID | Status | File(s) | Live Code Evidence | Verdict / Remediation Plan |
|---|---|---|---|---|
| **FUNC-001** | **CONFIRMED** | `src/Dynamologio.App/Views/ImportExportView.xaml` | Single page with Browse + Execute instead of real 5-stage stepper wizard. | Implement interactive 5-stage wizard: `1. Αρχείο` -> `2. Ανίχνευση` -> `3. Αντιστοίχιση` -> `4. Επίλυση` -> `5. Προεπισκόπηση & Εισαγωγή`. |
| **FUNC-006** | **CONFIRMED** | `src/Dynamologio.Reporting/Services/ReportGeneratorService.cs` | Did not enforce active `ReportTemplate` entity hash or mapping coordinates. | Wire `ReportTemplate` resolution, SHA-256 verification, and mapping model. |
| **FUNC-008** | **CONFIRMED** | `src/Dynamologio.App/ViewModels/ReportsViewModel.cs` | Multiple report types exposed but Excel/print paths called single generic method. | Implement distinct generators for Daily Dynamologio, Absent List, Present List, and Services Roster. |
| **FUNC-009** | **CONFIRMED** | `src/Dynamologio.App/ViewModels/DataValidationViewModel.cs` | Only scanned orphan ranks/units, duplicate ASMs, and leave overlaps. | Expand validation scanner to include: service duty during leave, service duty outside strength dates, overlapping service duties, and orphan service types. |
| **FUNC-011** | **CONFIRMED** | `src/Dynamologio.App/ViewModels/SettingsViewModel.cs` | Hardcoded `"123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ"` / `"1ο ΓΡΑΦΕΙΟ"`. | Use unconfigured default ("Μη Διαμορφωμένη Μονάδα") until configured by user. |
