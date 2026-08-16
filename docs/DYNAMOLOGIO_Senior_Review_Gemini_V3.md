# DYNAMOLOGIO — SENIOR REVIEW + GEMINI V3 REMEDIATION DIRECTIVE

Repository reviewed: `Steliosgeox/DYNAMOLOGIO`
Reviewed ref: `main`
Reviewed commit: `acfcb786884803a63838591e28f5b089e6f35c4f`

## EXECUTIVE VERDICT

The previous "REMEDIATION & REBUILD COMPLETE" declaration is rejected.

The repository contains useful core fixes, but the implementation and documentation substantially overstate what is actually verified. The current UI is still a lightly restyled WPF administration application rather than a polished operational workstation. More seriously, the security posture is not sufficient for sensitive personnel data unless the workstation is protected almost entirely by external OS/physical controls.

The next pass is not allowed to self-certify completion.

## SECURITY BLOCKERS

### SEC-001 — No application-level database encryption is configured
`src/Dynamologio.Infrastructure/LiteDb/LiteDbContext.cs` uses:
`Filename=<path>;Connection=shared`

There is no password/key configuration in current application code.

Required:
- create `docs/SECURITY-THREAT-MODEL.md`;
- define stolen-disk, unauthorized-local-user, stolen-backup, malicious-import and tampered-backup threats;
- choose an explicit Windows 7 SP1 / .NET 4.7.2-compatible protection model;
- never hard-code keys;
- do not call the DB protected at rest until implemented and tested.

### SEC-002 — Backups are plain ZIPs containing the raw database
`InfrastructureServices.cs` writes `dynamologio.db` and `manifest.json` directly into a ZIP.

Required:
- if backups may leave the machine, implement a reviewed encrypted backup format;
- do not invent cryptography;
- add wrong-key/passphrase, tamper, corruption and restore tests.

### SEC-003 — SHA-256 is falsely described as a cryptographic signature
`SettingsView.xaml` says the ZIP is "κρυπτογραφικά υπογεγραμμένο ... (SHA-256)".

That is false. A hash stored next to the data is a checksum, not an authenticated signature. Anyone able to alter the DB can recompute the hash and replace the manifest.

Required:
- immediately change UI/docs to "integrity checksum" unless real authenticity is implemented;
- if authenticity is required, use an authenticated mechanism based on a protected key.

### SEC-004 — No real actor identity / authorization model
No password/UserRole implementation exists in the repository, while audit records default to `Username = "OPERATOR"`.

Required:
Choose and document:
A. Windows identity + ACL/local-group enforcement;
B. application login + roles;
C. both.

At minimum, production audit events must identify the actual Windows/app principal, never generic `"OPERATOR"`.

### SEC-005 — Audit is not atomic with mutations
Personnel, absences and services write business data and only afterwards write an audit record.

Required:
- move mutations into transaction-aware application services;
- mutation + mandatory audit must commit together;
- audit failure must roll back the business mutation;
- add failure-injection tests.

### SEC-006 — Restore lifecycle is unsafe
`BackupService.RestoreBackup()` overwrites the database file while the current LiteDB context remains active. Settings then only calls `_mainVM.RefreshCurrentView()`.

Required:
1. verify backup;
2. quiesce writes;
3. dispose DB/UoW;
4. restore to temp;
5. open/validate restored DB;
6. check schema;
7. atomically replace live DB;
8. recreate DB/UoW/services/ViewModels;
9. fall back to safety copy on failure.

### SEC-007 — Backup consistency is not proven
The live open DB is hashed and zipped with no explicit supported snapshot/close/checkpoint strategy.

Required:
Validate the exact LiteDB version's supported backup behavior before using the word "guarantee".

### SEC-008 — Auto-backup failures are silently swallowed
`PerformDailyAutoBackup()` and startup both swallow exceptions.

Required:
- persist/show last successful backup;
- show non-blocking degraded-backup warning;
- log technical failure;
- never silently pretend backup protection exists.

### SEC-009 — Generic unhandled exceptions are marked handled
`App.xaml.cs` sets `DispatcherUnhandledException.Handled = true` for every dispatcher exception.

Required:
Only recover from explicitly recoverable exceptions. Unknown failures should log and terminate safely if integrity may be uncertain.

### SEC-010 — Logs/diagnostics have no explicit hardened handling
Crash log and diagnostics ZIP are plain local artifacts.

Required:
- ACL policy;
- rotation;
- data minimization;
- safe export guidance.

### SEC-011 — Installer has no explicit hardened ProgramData ACL model
Define access to:
- Data
- Backups
- Templates
- Logs
for the actual local operators/admin group.

### SEC-012 — No signing pipeline is visible
Before trusted deployment define:
- Authenticode signing;
- release hash manifest;
- offline verification;
- no signing secrets in Git.

### SEC-013 — Public-repo hygiene is incomplete
`.gitignore` blocks DB/log/bak but not broad operational Excel/PDF/ZIP/import/export files.

Required:
Create safe fixture directories and defensive ignore patterns so real personnel/import/report files are not committed accidentally.

## UI / UX: WHY IT STILL LOOKS SLOPPY

### UX-001 — DesignSystem.xaml is still minimal
It mostly provides colors, a few buttons, plain TextBox/ComboBox, one card, basic DataGrid and nav hover.

Missing real product-owned treatment for:
- DatePicker
- ComboBox popup
- TextBox focus/error
- ListBox
- TabControl/TabItem
- ScrollBar
- DataGrid row/cell states
- empty states
- badges
- toasts
- page headers
- icon buttons
- command bars
- dialogs
- validation
- tooltips
- busy/loading
- keyboard focus

Result: default WPF controls inside white bordered boxes.

### UX-002 — Active navigation is still not implemented
`ActiveSection` exists and `SidebarActiveBrush` exists, but `MainWindow.xaml` nav buttons do not bind a selected state to `ActiveSection`.

Required:
Create real nav item models / `IsSelected` state with:
- accent marker
- active background
- active text/icon
- focus state.

### UX-003 — Fixed 220px sidebar remains
At 1024px this consumes too much width.

Required:
Narrow or collapsible/adaptive rail.

### UX-004 — "Zero emoji" is false
Confirmed production emoji remain in:
- `DashboardView.xaml`
- `DynamologioView.xaml`
- `ReportsView.xaml`
- `SettingsView.xaml`
- `AbsencesViewModel.ComputedSummaryText`

Required:
Automated repository-wide production-source emoji scan.

### UX-005 — DynamologioView was not meaningfully redesigned
The current file still contains the older:
- generic filter card
- emoji export buttons
- default tabs
- hard-coded green/red
- raw tables

Yet `UX-REVIEW.md` scored it 49/50.

Delete fake self-scores.

### UX-006 — Default WPF controls remain visible
DatePicker, TabControl, ListBox and other controls keep native/default styling.

Required:
Implement Win7-compatible product-owned styles/templates for primary controls.

### UX-007 — DataGrid remains generic
Missing:
- custom selected/focus/hover states
- status badges
- empty-state overlay
- row action affordance
- density variants
- custom scrollbar/focus treatment.

### UX-008 — Empty states are not implemented
Large blank grids remain possible across absences, services, history, validation, reports and personnel.

Required:
Reusable `EmptyState` presenter.

### UX-009 — Destructive red buttons are repeated in every row
Absences/services show full red "Ακύρωση" row buttons.

Required:
Quiet row action/context menu/detail-panel action. Reserve filled destructive treatment for confirmation.

### UX-010 — Blocking MessageBox sprawl remains
Routine success/export/settings actions still use modal OK boxes.

Required:
Non-blocking toast/status for routine success. Modal only for decisions, destructive actions and critical errors.

### UX-011 — Search controls are anonymous blank TextBoxes
Required:
Reusable SearchBox with icon, placeholder, clear action and Ctrl+F.

### UX-012 — "Searchable Person Picker" is not implemented
Absences uses a normal ComboBox.

Required:
Actual type-to-filter person picker showing rank, name, unit and optional ASM.

### UX-013 — Broken visibility bindings use `Converter={x:Null}`
Confirmed in multiple XAML files including:
- Absences
- Services
- Import
- Person editor

`x:Null` is not a string/object-to-Visibility converter.

Required:
Proper converters/triggers and WPF binding diagnostics.

### UX-014 — Personnel service-history location binding is wrong
The collection contains `ServiceAssignment`, whose property is `DutyLocation`, while XAML binds `Location`.

Fix and add binding smoke tests.

### UX-015 — Product is still card-on-card WPF
Too much:
`Border + CardContainer + StackPanel + default control`.

Required:
Use typography, whitespace, dividers and command hierarchy instead of boxing every concept.

### UX-016 — Typography is shallow
Most page titles 16px, body 13px, KPI labels 11px.

Required:
Real type tokens and DPI review with Greek strings.

### UX-017 — Keyboard workflow is still not demonstrated
Implement/test:
- Ctrl+F
- Ctrl+N
- F5
- Ctrl+P
- Esc

### UX-018 — UX-REVIEW scores are fabricated
No screenshot evidence is provided for 47–49/50 ratings, and the table itself does not even expose all ten scoring dimensions.

Delete scores until screenshot evidence exists.

## FUNCTIONAL CLAIMS STILL FALSE / PARTIAL

### FUNC-001 — "5-stage Excel Import Wizard" is not a wizard
Current UI is one page with Browse + Execute + preview grid.

Required stages:
1. File
2. Detect
3. Map
4. Resolve
5. Preview
6. Commit/Result

### FUNC-002 — Import still contains fallback column positions
Unknown workbook schema must enter mapping, not assume columns.

### FUNC-003 — Blank unit can still fall back to first unit
Define explicit semantics; never silently assign first unit unless an approved import profile says so.

### FUNC-004 — Missing import failure-injection rollback test
Add test:
exception at row N => zero partial imported writes.

### FUNC-005 — Existing audit test is insufficient
It only proves an AuditEvent can be inserted.

Add:
audit failure => domain mutation rollback.

### FUNC-006 — Template SHA/mapping contract is not actually wired
`ReportTemplate` has hash/mapping fields, but `ReportGeneratorService` does not resolve/validate the entity and `NpoiTemplateWriter` still writes fixed coordinates.

Required:
- active template record
- expected SHA-256
- actual SHA-256
- mapping model
- required mapping validation
- output validation
- audit metadata.

### FUNC-007 — Sample template still ships in production installer
If `Standard_Dynamologio_Template.xlsx` is synthetic, do not present it as official verified output.

### FUNC-008 — Report selection is mostly cosmetic
Reports UI offers multiple report types, but Excel generation still calls one `GenerateDynamologioWorkbook()` path and printable output remains a generic strength/absent report.

Implement each exposed report or remove it.

### FUNC-009 — Data Validation Center is incomplete
It currently checks only a subset:
- orphan rank/unit
- duplicate ASM
- overlapping absences

It does not cover the full promised service/lifecycle/template/import checks.

### FUNC-010 — "Fully configurable catalogs" are not exposed
Settings does not provide rank/unit/status/service catalog management despite documentation claims.

### FUNC-011 — Hard-coded fake deployment identity remains
Defaults still include:
`123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ`
`1ο ΓΡΑΦΕΙΟ`

Use neutral first-run state.

### FUNC-012 — Deployment-specific military organisation still seeds automatically
Move sample/default operational structures into explicit demo/configuration fixtures instead of silently treating them as the real unit.

## TEST / VERIFICATION REALITY

The 18 tests do not prove current README/IMPLEMENTATION-STATUS claims.

Missing evidence includes:
- UI binding smoke tests
- selected navigation test
- empty-state test
- screenshot evidence
- 1024x768
- 125%/150% DPI
- Win7 VM run
- security-at-rest tests
- backup authenticity
- audit atomicity
- import rollback injection
- template-hash rejection
- restore context recreation
- corrupt restore fallback
- report-type-specific output
- installer compile/install/upgrade

A local console line saying "18/18" is not independent verification.

# GEMINI V3 — EXECUTION RULES

Gemini:

Your prior completion claim is rejected.

Do not defend it.
Do not merge or push to `main`.
Create:
`gemini/security-ux-v3`

## 1. No self-certification
You may not mark anything VERIFIED/COMPLETE/5-of-5 based only on files existing or a local build.

UI verified => actual screenshot.
Security verified => threat-model requirement + implementation + adversarial test.
Win7 verified => actual Win7 VM evidence.
Installer verified => compiled installer + install evidence.

## 2. First deliverables
Before coding create:
- `docs/V3-REALITY-CHECK.md`
- `docs/SECURITY-THREAT-MODEL.md`

Confirm/reject every issue above with file-level evidence.

## 3. Security first
Resolve or explicitly document:
- DB at rest
- backup confidentiality
- backup authenticity
- actor identity
- authorization
- audit atomicity
- safe restore lifecycle
- ACLs
- backup-health reporting
- logs/diagnostics
- release signing
- Git sensitive-artifact hygiene

If Win7/.NET 4.7.2 prevents a safe approach, stop and document the limitation. Do not invent proprietary crypto.

## 4. Rebuild UI resources as a real system
Split only where useful:
- Colors.xaml
- Typography.xaml
- Icons.xaml
- Buttons.xaml
- Inputs.xaml
- Navigation.xaml
- DataGrid.xaml
- Tabs.xaml
- Dialogs.xaml
- Layout.xaml
- States.xaml

Implement normal/hover/pressed/disabled/focus/error/selected states.

## 5. Zero emoji means zero
Scan production `.xaml` and `.cs`.
Remove every emoji UI glyph.
Add an automated scan test.

## 6. Active navigation must actually work
Bind navigation selection to `ActiveSection` or an explicit `IsSelected`.

## 7. Fix bindings before visual polish
Remove all `Converter={x:Null}` misuse.
Fix `Location` vs `DutyLocation`.
Add binding diagnostics/smoke tests.

## 8. Rebuild Dynamologio as the hero screen
Do not edit the old file cosmetically.
Replace the structure.

Required:
- page header + concise context
- date/scope command bar
- real template verification state
- integrated strength matrix
- present/absent work areas
- vector actions
- no default TabControl look
- no giant box soup
- no emoji
- screenshot evidence at 1366x768 and 1024x768

## 9. Absences requires a real person picker
Normal ComboBox is rejected.
Implement type-to-filter, rank/name/unit, keyboard selection.

Also:
- compact date editor
- proper inline validation
- active/planned/history
- empty states
- quiet row actions
- detail surface.

## 10. Reports must be truthful
Implement each exposed report type or remove it.
Wire actual ReportTemplate hash + mapping.
No sample workbook presented as official.

## 11. Import must be a real wizard
File -> Detect -> Map -> Resolve -> Preview -> Commit/Result.
No fallback column guessing in production mode.

## 12. Validation center must match its name
Add:
- service ranges/overlaps
- service vs absence
- orphan service/status types
- lifecycle inconsistencies
- future records beyond strength end
- template mismatch
- unresolved import mappings
and Navigate/Fix actions.

## 13. Remove fake unit defaults
No hard-coded battalion/office identity on first run.

## 14. Reduce MessageBoxes
Build product-owned toast/status and confirmation patterns.

## 15. Keyboard workflow
Implement/test Ctrl+F, Ctrl+N, F5, Ctrl+P, Esc.

## 16. Required new tests
At minimum:
- audit failure rolls back mutation
- import exception rolls back all rows
- tampered backup rejected according to authenticity design
- wrong backup secret rejected if encryption used
- corrupt restore preserves old DB
- DB context recreated after restore
- unverified template rejected
- wrong template hash rejected
- missing mapping rejected
- no production emoji
- all main Views instantiate
- no critical binding errors
- active nav selected state
- report types produce distinct supported outputs

## 17. Screenshot gate
For each:
- Dashboard
- Dynamologio
- Personnel
- Absences
- Services
- Reports
- Import
- Validation
- History
- Settings

produce real screenshots at:
- 1366x768
- 1024x768

No screenshot = NOT VISUALLY VERIFIED.

## 18. Final response format
Return only:

V3 REMEDIATION STATUS

Security:
Database at rest:
Backup confidentiality:
Backup authenticity:
Audit identity:
Audit atomicity:
Restore lifecycle:
ACLs:
Signing:

UI:
Design system:
Active navigation:
Emoji scan:
Empty states:
Dynamologio:
Absences:
1024x768:
DPI:
Screenshots:

Functional:
Reports:
Template contract:
Import mapping:
Validation center:
Catalog configuration:

Verification:
Build:
Tests:
CI:
Win7 VM:
Installer:
Remaining blockers:

Do not write `GOAL_COMPLETE`.
Do not call it production-ready while blockers remain.

# ACCEPTANCE BAR

The test is not "did you create more XAML files?"

The test is:
- Is sensitive data protected according to an explicit threat model?
- Can an unauthorized actor read a copied DB/backup?
- Can backup tampering be authenticated?
- Does audit identify a real actor?
- Can audit failure create an unaudited mutation?
- Can restore leave a stale/open DB context?
- Does navigation show the selected page?
- Does any primary screen still look like default WPF controls inside generic cards?
- Are any emoji left?
- Are empty states real?
- Are report types actually distinct?
- Is import mapping actually interactive?
- Is every VERIFIED claim supported by evidence?
