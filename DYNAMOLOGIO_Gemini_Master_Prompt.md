# GEMINI MASTER BUILD PROMPT
## Project: ΔΥΝΑΜΟΛΟΓΙΟ
### Production-grade offline personnel strength management for legacy Windows environments

You are acting as a combined:

- Principal Software Architect
- Senior C# / WPF Engineer
- Legacy Windows Compatibility Engineer
- Database Architect
- Desktop UX Lead
- Excel/PDF Reverse-Engineering Engineer
- QA Lead
- Security Engineer
- Release Engineer

You are not building a demo, prototype, mockup, student project, or generic CRUD application.

You are building a production-grade Greek administrative desktop application called:

# ΔΥΝΑΜΟΛΟΓΙΟ

The deployed application must operate completely offline and must support legacy Greek military-office computers, including Windows 7 SP1, while also running correctly on Windows 10 and Windows 11.

The application exists to replace repeated manual Excel work with a reliable local personnel-strength management system.

The user will provide the official or currently-used Excel workbook(s) and representative PDFs after this prompt.

Those files are the primary source of truth for reporting structure and field semantics.

Do not invent military administrative procedures that cannot be established from:

1. the supplied files,
2. explicit user requirements,
3. configurable administrative settings.

If an administrative rule is unknown, make it configurable or mark it as an unresolved assumption.

---

# 0. NON-NEGOTIABLE MISSION

The daily workflow must become approximately:

PERSONNEL DATABASE
→ RECORD A CHANGE ONCE
→ STATUS CALCULATED AUTOMATICALLY
→ CURRENT STRENGTH CALCULATED AUTOMATICALLY
→ HISTORICAL STRENGTH RECONSTRUCTABLE
→ OFFICIAL REPORT GENERATED
→ PRINT / EXCEL / PDF

The operator should not manually recalculate totals or manually restore someone to "present" after an absence expires.

Core question answered by the system:

> Ποια είναι η δύναμη τώρα, ποιοι είναι παρόντες, ποιοι απουσιάζουν, για ποιο λόγο, μέχρι πότε, τι υπηρεσία έχουν και τι πρέπει να εμφανιστεί στο επίσημο Δυναμολόγιο;

The software must treat personnel state as time-dependent data, not merely a mutable Present/Absent boolean.

---

# 1. HARD PLATFORM REQUIREMENTS

Target platform:

- Windows 7 SP1 x86
- Windows 7 SP1 x64
- Windows 10 x64
- Windows 11 x64

Primary framework:

- C#
- WPF
- .NET Framework 4.7.2
- C# language features compatible with that toolchain

Do not switch to modern .NET unless you can prove Windows 7 compatibility with the exact deployment model.

Forbidden core runtime dependencies:

- Electron
- Chromium
- WebView2
- Node.js
- React
- Vue
- Angular
- MAUI
- Blazor Hybrid
- local HTTP server
- Docker
- mandatory Internet access
- cloud APIs
- cloud database
- online authentication
- browser rendering engine
- Office Interop as a required reporting dependency

The deployed executable must remain fully usable with the network disconnected.

Do not silently introduce a dependency that breaks Windows 7.

Before adopting any NuGet package, verify:

- exact supported target framework,
- x86/x64 behavior,
- native dependencies,
- Visual C++ runtime requirements,
- Windows API requirements,
- license suitability,
- offline deployment behavior.

Maintain a dependency compatibility table.

---

# 2. ARCHITECTURAL PRINCIPLE

Use a layered architecture.

Recommended solution:

```text
Dynamologio.sln

src/
  Dynamologio.App/
  Dynamologio.Core/
  Dynamologio.Infrastructure/
  Dynamologio.ImportExport/
  Dynamologio.Reporting/

tests/
  Dynamologio.Tests/
```

Responsibilities:

## Dynamologio.App
- WPF shell
- Views
- ViewModels
- Commands
- Navigation
- Validation presentation
- dialogs
- localisation resources
- application bootstrap

## Dynamologio.Core
- domain entities
- domain enums/value objects
- personnel lifecycle rules
- status calculation engine
- strength calculation engine
- validation
- conflict detection
- interfaces
- report contracts

Core must not know about WPF or LiteDB.

## Dynamologio.Infrastructure
- LiteDB persistence
- repositories
- schema migrations
- audit log
- security
- backup/restore
- filesystem
- configuration

## Dynamologio.ImportExport
- Excel analysis
- Excel import
- Excel template mapping
- Excel output
- CSV where useful
- PDF parsing
- import preview
- import provenance

## Dynamologio.Reporting
- report models
- report composition
- Excel report generation
- PDF generation
- print documents
- preview

## Dynamologio.Tests
- unit tests
- integration tests
- regression tests
- golden-template tests
- date-boundary tests
- migration tests
- backup tests

Dependencies point inward.

Business logic must not live in code-behind.

---

# 3. FIRST ACTION AFTER RECEIVING USER FILES

DO NOT begin coding immediately.

Your first task is reverse engineering.

For every supplied Excel workbook, inspect and produce a structured technical analysis.

You must identify:

- file type: XLS / XLSX
- workbook properties
- sheet names
- hidden sheets
- used ranges
- merged ranges
- named ranges
- formulas
- totals
- calculated cells
- manually populated cells
- lookup/reference sections
- static labels
- colours
- borders
- font usage
- row heights
- column widths
- print areas
- page setup
- paper size
- orientation
- scaling
- headers
- footers
- page breaks
- repeated print rows
- protection settings if relevant
- cells likely to represent personnel
- cells likely to represent categories
- cells likely to represent totals
- rows that grow dynamically
- fields that cannot yet be interpreted

Then produce:

```text
WORKBOOK ANALYSIS
CONFIRMED FIELDS
PROBABLE FIELDS
UNKNOWN FIELDS
FORMULAS
DYNAMIC REGIONS
REPORT OUTPUT CONTRACT
MAPPING PROPOSAL
RISKS
QUESTIONS / ASSUMPTIONS
```

Do the same conceptually for supplied PDFs.

The Excel/PDF documents define reporting expectations.

The UI does not need to copy their visual style.

Generated official outputs do.

---

# 4. OFFICIAL TEMPLATE PRESERVATION RULE

When generating an official Excel:

Do not rebuild the workbook visually from scratch unless absolutely necessary.

Preferred method:

1. copy the approved workbook template,
2. open the copy,
3. populate mapped cells/rows,
4. preserve untouched content,
5. save the generated copy.

Preserve whenever possible:

- formulas
- styles
- merged cells
- borders
- print areas
- page setup
- row heights
- column widths
- headers
- footers
- page breaks
- named ranges
- static labels

Never overwrite the source template.

Every official template receives:

- internal TemplateId
- name
- version
- SHA-256 hash
- imported date
- mapping profile version

If the file hash changes and structure differs from the known template:

STOP automatic export for that template.

Show:

"Το πρότυπο αρχείο έχει αλλάξει και απαιτείται επαλήθευση αντιστοίχισης."

Do not guess cell positions.

---

# 5. EXCEL TECHNOLOGY

Prefer NPOI for Excel support because the application must function without Microsoft Office.

Required formats:

- XLS
- XLSX

Do not require Excel installation.

Abstract workbook operations behind services so that implementation can be replaced later if required.

Suggested interfaces:

```csharp
IWorkbookAnalyzer
IExcelImportService
IExcelTemplateWriter
IExcelMappingProfileRepository
```

---

# 6. PRODUCT INFORMATION ARCHITECTURE

Primary navigation should include:

- Αρχική
- Δυναμολόγιο
- Προσωπικό
- Απουσίες
- Υπηρεσίες
- Αναφορές
- Εισαγωγή / Εξαγωγή
- Ιστορικό
- Ρυθμίσεις

The exact final wording may change after document analysis.

Do not clutter the interface with functions not used by the office.

---

# 7. DASHBOARD

The home screen must answer operational questions immediately.

Candidate tiles:

- Συνολική Δύναμη
- Παρόντες
- Απόντες
- Στελέχη
- Στρατιώτες
- Επιστρέφουν Σήμερα
- Επιστρέφουν Αύριο
- Απουσίες που λήγουν
- Εκκρεμείς συγκρούσεις δεδομένων

Quick actions:

- Νέα Απουσία
- Νέα Υπηρεσία
- Προσθήκη Προσωπικού
- Εισαγωγή Excel
- Εισαγωγή PDF
- Εκτύπωση Δυναμολογίου

Do not turn the dashboard into an analytics toy.

Only display information that helps administrative work.

---

# 8. UI / UX DESIGN STANDARD

This application must look professionally designed.

It must NOT look like:

- generic AI-generated dashboard
- Bootstrap admin template
- mobile app enlarged onto desktop
- gaming application
- fake military HUD
- excessive camouflage design
- futuristic neon system
- glassmorphism showcase
- giant cards with wasted space

Visual direction:

"Modern Greek administrative operations workstation."

Use:

- Segoe UI or another guaranteed Windows-safe fallback
- restrained navy / graphite / muted olive accents
- neutral surfaces
- excellent contrast
- compact information density
- clear visual hierarchy
- crisp borders
- restrained rounding
- consistent 6–8px spacing system
- status chips/badges where useful
- meaningful icons only
- excellent keyboard navigation

Primary target resolution:

1366x768

Minimum supported resolution:

1024x768

Everything must remain usable at minimum resolution.

Do not assume touch input.

This is mouse + keyboard software.

Common workflows should require 1–3 interactions.

Tables should be compact and extremely readable.

Use DataGrid virtualization.

Avoid unnecessary animation.

Animations must never slow old machines.

Use WPF rendering-tier detection and gracefully reduce visual effects on weak GPUs.

---

# 9. LOCALISATION

Greek is the default UI language.

Use Greek for:

- labels
- menus
- validation
- warnings
- confirmation dialogs
- reports
- print preview
- status labels

Culture:

```csharp
CultureInfo("el-GR")
```

Display dates:

```text
dd/MM/yyyy
```

Display time:

```text
HH:mm
```

Do not scatter Greek text literals throughout code.

Use resource files/localisation service.

Even if only Greek is shipped initially, structure localisation cleanly.

---

# 10. DATABASE

Preferred V1 database:

LiteDB 5.x

Reasons:

- embedded
- serverless
- local
- pure .NET
- easy offline deployment
- avoids separate DB service
- avoids SQL Server requirement
- avoids unnecessary native SQLite packaging issues

Use a repository abstraction.

Never expose LiteDB directly to ViewModels.

Collections should approximately include:

```text
Personnel
Ranks
OrganisationUnits
StatusTypes
StatusEvents
ServiceTypes
ServiceAssignments
ReportTemplates
ImportProfiles
ImportBatches
AuditEvents
ApplicationUsers
Settings
SchemaInfo
```

Do not treat this proposed schema as final until the source documents are analysed.

---

# 11. DATABASE FILE RULES

The application owns one local data store.

Example:

```text
%PROGRAMDATA%\Dynamologio\Data\dynamologio.db
```

or a similarly appropriate protected location.

Do not put mutable DB data under Program Files.

Do not use a network share for LiteDB multi-user editing.

V1 is a single-workstation database architecture.

If future shared multi-PC operation is requested, keep domain and repository abstractions capable of migration to a central service/database architecture.

Do not pretend LiteDB is a safe concurrent LAN server database.

---

# 12. PERSONNEL DOMAIN MODEL

Every person receives a permanent internal ID/GUID.

Candidate Personnel fields:

```text
Id
MilitaryRegistryNumber or equivalent if present
LastName
FirstName
FatherName if required
RankId
PersonnelCategory
Branch / Corps if relevant
Specialty if relevant
OrganisationUnitId
Section/Office/Company if relevant
StrengthStartDate
StrengthEndDate
IsArchived
Notes
CreatedAt
ModifiedAt
```

PersonnelCategory should at minimum support:

- ΣΤΕΛΕΧΟΣ
- ΣΤΡΑΤΙΩΤΗΣ / ΟΠΛΙΤΗΣ

Use actual terminology discovered in source files.

Never hard-code organisation structure such that the application only works for one office.

Model hierarchical units.

Example:

```text
Formation
  Unit
    Subunit
      Section
```

But adapt to the actual source documents.

---

# 13. RANKS

Ranks must exist in a configurable lookup table.

Fields:

```text
Id
Name
ShortName
Category
SortOrder
IsActive
```

Do not hard-code rank ordering in UI logic.

The official files and user-provided list determine the actual rank catalogue needed.

---

# 14. PERSONNEL LIFECYCLE

Never normally hard-delete personnel who have existed in legitimate historical records.

Support effective-date lifecycle events such as:

- added to strength
- transferred in
- transferred out
- discharged
- archived
- other administratively relevant changes

If someone leaves active strength on date X:

Before X:
included.

At/after X:
not included.

Historical reports before X must still reconstruct them correctly.

True deletion is allowed only for administrative correction of records that never legitimately participated in history, and should require administrator privileges.

---

# 15. STATUS MODEL

The system must not use only:

```csharp
bool IsPresent
```

This is unacceptable.

Presence is derived from dated status events.

Suggested entity:

```text
StatusEvent
Id
PersonnelId
StatusTypeId
StartAt
EndAtExclusive
IsAllDay
Reference
Comment
CreatedAt
CreatedBy
ModifiedAt
ModifiedBy
ImportBatchId
```

Status types are configurable.

Suggested fields:

```text
StatusType
Id
Name
ShortCode
Category
CountsAs
RequiresEndDate
AllowsTime
ColourKey
SortOrder
IsActive
ReportMappingCode
MutualExclusionGroup
```

CountsAs may initially include:

```text
Present
Absent
ExcludedFromStrength
Other
```

Do not assume these are the only categories if the official workbook proves otherwise.

---

# 16. DATE SEMANTICS

Internally use explicit half-open intervals:

```text
StartAt <= now < EndAtExclusive
```

Example:

User enters:

```text
Άδεια από 16/08/2026
Επιστροφή 21/08/2026
```

Store:

```text
StartAt = 16/08/2026 00:00
EndAtExclusive = 21/08/2026 00:00
```

At 21/08 00:00 the absence is no longer active.

This must be implemented centrally.

Do not duplicate date interpretation logic across screens.

The UI wording should make the semantic distinction clear.

Prefer user fields like:

```text
Από:
Επιστροφή:
```

rather than an ambiguous "Μέχρι:" when possible.

If the official process explicitly uses "μέχρι", convert it safely and display the implied return date.

---

# 17. AUTOMATIC RETURN TO PRESENCE

Do not create a Windows service merely to flip absence records.

Do not depend on Windows Task Scheduler.

Current status is derived at query time.

Pseudo logic:

```text
if person not in active strength at selected timestamp
    Excluded
else
    find active status events
    resolve according to configured rules
    if active absence
        Absent
    else
        Present
```

Therefore:

- PC may be powered off for days
- app launches later
- expired absence is automatically no longer active
- person appears present without a manual return action

Refresh calculations:

- at startup
- when opening Δυναμολόγιο
- after status changes
- after personnel changes
- before report generation
- after imports
- at local midnight while application remains open

---

# 18. HISTORICAL STATE

The application must support:

```text
Δυναμολόγιο τώρα
```

and:

```text
Δυναμολόγιο κατά την 05/08/2026
```

Historical reconstruction is mandatory.

Never overwrite previous state merely to represent the current state.

A dated-event architecture is required so the software can answer:

- who was in strength,
- who was absent,
- why,
- which services were assigned,
- totals,
- report buckets,

for any valid past timestamp.

---

# 19. STRENGTH CALCULATION ENGINE

Create a dedicated service.

Suggested concept:

```csharp
IStrengthCalculator
```

Input:

```text
timestamp
organisation scope
optional filters
```

Output:

```text
active personnel
present personnel
absent personnel
excluded personnel
breakdowns
totals
validation warnings
```

Support grouping by:

- personnel category
- rank
- unit/subunit
- status type
- report-specific category

Core invariant, where appropriate:

```text
Present + Absent = Active Strength
```

unless official rules define additional buckets.

If they do, model those explicitly rather than fudging totals.

---

# 20. CONFLICT ENGINE

Create deterministic validation for contradictory data.

Detect:

- overlapping mutually-exclusive absences
- return date before start
- status before strength-entry date
- service after removal from strength
- duplicate active personnel identities
- invalid rank reference
- invalid organisation reference
- archived person receiving a new active event
- malformed import values
- impossible report mappings

Classify findings:

```text
Error
Warning
Information
```

Errors block save/import.

Warnings require explicit operator acknowledgement if appropriate.

Never silently overwrite conflicting records.

Provide a dedicated screen:

# Έλεγχος Δεδομένων

---

# 21. ABSENCE MANAGEMENT

The absence screen should allow:

- New absence
- Edit planned/active absence
- Cancel incorrect absence
- Search/filter
- Current absences
- Future absences
- Ending today
- Returning tomorrow
- Open-ended absences if allowed
- History

Typical create flow:

1. choose person
2. choose type
3. start date/time
4. return date/time or end date
5. source/reference if required
6. note
7. validate
8. save

Operator should see resulting effect before saving when useful:

```text
Κατάσταση μετά την καταχώρηση: ΑΠΩΝ
Επιστροφή σε ΠΑΡΩΝ: 21/08/2026
```

---

# 22. SERVICE / DUTY MANAGEMENT

Services are not the same entity as absence.

Create configurable ServiceType and ServiceAssignment.

Candidate ServiceAssignment fields:

```text
Id
PersonnelId
ServiceTypeId
Date
StartTime
EndTime
Location
Notes
Status
Reference
CreatedAt
CreatedBy
ModifiedAt
ModifiedBy
```

Initial UI:

- Υπηρεσίες Σήμερα
- Υπηρεσίες Αύριο
- Ημερολόγιο Υπηρεσιών
- ανά πρόσωπο
- ανά υπηρεσία
- ιστορικό

Do not automatically assume a service changes Present/Absent state.

Instead give ServiceType a configurable reporting effect if official documents require one.

---

# 23. DO NOT OVERBUILD V1

Unless source files prove it necessary, V1 should not include:

- AI decision-making
- automatic duty roster optimisation
- biometric attendance
- cloud sync
- mobile app
- remote server
- web UI
- Internet login
- predictive analytics
- complex scheduling solver

Finish the administrative core first.

---

# 24. PERSONNEL UI

Use a high-performance virtualized grid.

Candidate columns:

- Βαθμός
- Επώνυμο
- Όνομα
- Κατηγορία
- Μονάδα / Τμήμα
- Τρέχουσα Κατάσταση
- Επιστροφή
- Υπηρεσία
- Παρατηρήσεις

Features:

- instant search
- Greek-aware search
- accent-insensitive matching when reliable
- filters
- multi-column sort
- category filters
- status filters
- saved filter presets if useful
- keyboard navigation
- double-click personnel details
- bulk actions only where safe

Personnel detail view:

- basic information
- current state
- status history
- absence history
- service history
- lifecycle history
- audit history

Do not spawn endless modal windows.

Use a coherent master/detail workflow.

---

# 25. EXCEL IMPORT

Create a multi-stage import wizard.

Flow:

```text
Επιλογή Αρχείου
→ Ανάλυση
→ Αντιστοίχιση
→ Έλεγχος
→ Προεπισκόπηση Μεταβολών
→ Επιβεβαίωση
→ Εισαγωγή
```

Import preview must classify:

- new personnel
- updates
- unchanged records
- duplicates
- unknown ranks
- invalid dates
- unknown statuses
- conflicts
- ignored rows

No import should modify the live DB before preview approval.

Persist import profiles by workbook type when safe.

Import must be transaction-like:

- validate first
- create pre-import backup
- apply batch
- audit batch
- allow operator to identify all records created by that batch

---

# 26. IMPORT PROVENANCE

Every import has an ImportBatch.

Suggested:

```text
ImportBatch
Id
FileName
SHA256
ImportedAt
ImportedBy
ImportProfileId
RecordCount
WarningCount
Status
```

Imported records/events may reference ImportBatchId.

The system should later answer:

> Από ποιο αρχείο προήλθε αυτή η εγγραφή;

---

# 27. PDF IMPORT

Do not claim universal PDF understanding.

Support known document types.

Create:

```csharp
IPdfDocumentParser
IPdfDocumentClassifier
```

Pipeline:

```text
Select PDF
→ Extract text/positions
→ Detect known template/type
→ Parse candidate values
→ Validate
→ Preview
→ Human confirmation
→ Save
```

Use a library whose exact chosen version is compatible with the .NET target and deployment environment.

Do not add OCR to baseline V1 unless required.

For scanned/image-only PDFs:

Show clearly:

```text
Το PDF δεν περιέχει αναγνώσιμο κείμενο.
```

An offline Greek OCR extension can be designed separately only after its Windows 7 compatibility is proven.

Never import parsed PDF content directly without preview.

---

# 28. REPORTING ENGINE

Create a generic report pipeline.

Suggested:

```text
ReportDefinition
ReportParameters
ReportDataModel
ReportRenderer
```

Reports initially may include:

- Δυναμολόγιο
- Κατάσταση Απόντων
- Κατάσταση Παρόντων
- Επιστρέφοντες
- Υπηρεσίες Ημέρας
- Προσωπικό ανά Κατηγορία
- Προσωπικό ανά Βαθμό
- Ιστορικό Μεταβολών

But source documents determine final required reports.

Every report must accept an as-of date/time when logically applicable.

---

# 29. EXPORT ACTIONS

For each official report, ideally expose:

- Προεπισκόπηση
- Εκτύπωση
- Excel
- PDF

The operator should not need:

Excel → Open → File → Print

for every normal workflow.

Direct print must use the Windows printing subsystem.

Do not require vendor-specific printer SDKs.

---

# 30. PDF GENERATION

Use a PDF library compatible with the chosen .NET Framework target and deployment.

Requirements:

- Greek text
- embedded or safely resolved fonts
- A4
- portrait/landscape
- tables
- repeat headers
- page numbers
- margins
- signature placeholders if required
- no Internet
- deterministic layout

Do not bundle a proprietary font unless licensing permits it.

Prefer system fonts available on target Windows versions when practical.

---

# 31. SECURITY MODEL

The system processes personnel data.

Security requirements:

- zero telemetry
- zero analytics
- zero advertising
- zero external APIs
- no cloud upload
- no automatic Internet updates
- no background network traffic
- no third-party tracking

Implement local roles:

- Administrator
- Operator
- Read Only

Use application authentication if operationally desired.

Store passwords securely.

Never store plaintext passwords.

Use a modern password hash function available in the chosen environment.

Protect local secrets using Windows DPAPI where suitable.

If using LiteDB encryption, design key management explicitly.

Do not place hard-coded passwords or encryption keys in source.

---

# 32. AUDIT LOG

Every important mutation must be auditable.

Audit record should include:

```text
Timestamp
User
Action
EntityType
EntityId
OldValueSummary
NewValueSummary
ImportBatchId if applicable
ApplicationVersion
```

Audit:

- personnel create/edit/archive
- status create/edit/cancel
- service create/edit/cancel
- template import/update
- imports
- backup restore
- security/user changes
- configuration changes
- data repair operations

Normal operators must not be able to erase audit history.

---

# 33. BACKUP / RECOVERY

Backup is mandatory.

Create backup:

- before bulk import
- before schema migration
- before restore
- automatically once per day on first application launch
- manually on demand

Backup package should contain:

- encrypted or protected database
- schema version
- app version
- template mapping/config
- checksum manifest

Use rotating backup generations.

Restore workflow:

1. select backup
2. verify manifest
3. verify checksum
4. validate schema
5. automatically backup current live state
6. restore
7. verify restored DB
8. only then finalise

Never destroy the only valid live database before verifying the replacement.

---

# 34. SCHEMA MIGRATIONS

Maintain explicit schema versioning.

Example:

```text
SchemaVersion = 1
```

Then deterministic migrations:

```text
1 → 2
2 → 3
```

Each application version must know which schema versions it supports.

Before migration:

create backup.

Migration failure:

leave original DB recoverable.

Never "hope" a document database magically matches new model definitions.

---

# 35. ERROR HANDLING

Do not show users raw stack traces in normal dialogs.

User-facing errors must be concise Greek.

Example:

```text
Αποτυχία εισαγωγής.
Το αρχείο δεν τροποποίησε τη βάση δεδομένων.
Δείτε τις λεπτομέρειες για περισσότερες πληροφορίες.
```

Technical logs may contain:

- exception
- stack trace
- operation
- version
- filename
- correlation ID

Do not log sensitive data unnecessarily.

---

# 36. LOGGING

Implement local logging.

Suggested locations:

```text
%PROGRAMDATA%\Dynamologio\Logs\
```

Rotate logs.

Do not let logs grow forever.

Include application version.

Support an administrator action:

```text
Εξαγωγή Τεχνικού Πακέτου
```

which can package non-sensitive diagnostic logs and application/version metadata.

Never automatically upload it anywhere.

---

# 37. PERFORMANCE

Reference legacy test VM:

- Windows 7 SP1
- 2 CPU cores
- 2–4 GB RAM
- HDD-like storage
- 1024x768

Target:

- startup usable within about 5 seconds
- search appears immediate on normal dataset
- smooth DataGrid scrolling
- no UI blocking during Excel/PDF processing
- report generation within several seconds on expected data sizes

Use async/background worker patterns supported by .NET Framework.

Never access WPF controls from worker threads incorrectly.

Show progress for long operations.

Allow cancellation where safe.

---

# 38. WINDOWS 7 COMPATIBILITY GATE

Before release, inspect the entire dependency graph.

For every dependency record:

```text
Package
Version
Target Framework
Native DLL?
x86?
x64?
Windows APIs used
VC++ runtime requirement
Offline install notes
License
```

Run real tests or VM tests on:

- Win7 SP1 x86
- Win7 SP1 x64
- Win10 x64
- Win11 x64

A successful Windows 11 build proves nothing about Windows 7 compatibility.

---

# 39. INSTALLER

Build an offline installer.

Prefer a proven Windows installer system such as Inno Setup.

Installer must:

- reject OS older than Win7 SP1
- detect .NET Framework 4.7.2+
- optionally package/offline-chain required runtime if appropriate
- install application files
- create protected writable data directory
- preserve DB during upgrade
- preserve backups
- preserve templates
- create Start Menu shortcut
- support clean uninstall without deleting user DB by default

No Internet required.

Do not silently delete data on uninstall.

---

# 40. DOMAIN RULE: "CURRENT" IS DERIVED

Do not persist redundant "CurrentStatus" as the source of truth.

It may be cached for UI performance only if invalidation is absolutely correct.

Source of truth is:

- personnel lifecycle
- status events
- service assignments
- configuration
- selected timestamp

All current-state views must be reproducible from those records.

---

# 41. SAMPLE AUTOMATION CASES

## Case A – active leave

Today:

16/08/2026

Operator records:

```text
Λοχίας Παπαδόπουλος Ιωάννης
Κανονική Άδεια
Από: 16/08/2026
Επιστροφή: 21/08/2026
```

Expected:

16–20 Aug:
ΑΠΩΝ

21 Aug:
ΠΑΡΩΝ

No manual return click.

---

## Case B – future leave

Today:
16/08

Leave starts:
20/08

Expected:

16–19:
ΠΑΡΩΝ

20 until return:
ΑΠΩΝ

At return:
ΠΑΡΩΝ

---

## Case C – personnel leaves strength

Effective removal:

01/09/2026

Report at 31/08:
included.

Report at 01/09:
excluded.

Historical report at 20/08:
included.

---

## Case D – overlapping absence

Person already has:

16/08 → 21/08

Operator tries to add:

19/08 → 23/08

Expected:

block or flag according to mutual-exclusion rules.

Never silently replace.

---

# 42. TESTING REQUIREMENTS

Testing is mandatory.

Do not consider the project finished if UI "looks right".

## Status engine tests

Test:

- starts today
- ends today
- return today
- future status
- open-ended event
- midnight transition
- leap year
- month boundary
- year boundary
- same-day timed event
- overlapping exclusive events
- archived personnel
- historical lookup

## Strength engine tests

Test:

- no absences
- one absence
- multiple categories
- personnel enters strength
- personnel exits strength
- historical snapshot
- unit filter
- category filter
- rank grouping

Validate totals.

## Excel tests

Use golden copies.

After export compare:

- static cell values
- formulas
- merged ranges
- styles where technically measurable
- row heights
- column widths
- print areas
- orientation
- page settings

Any unexpected template mutation is a regression.

## Import tests

Test:

- XLS
- XLSX
- blank rows
- duplicate records
- malformed dates
- unknown ranks
- IDs stored as numbers
- IDs stored as text
- reordered columns
- Greek text
- trailing spaces
- duplicate names
- missing required values

## Backup tests

- create
- validate
- corrupt archive
- reject corrupt archive
- restore
- verify restored counts
- verify historical data

## Migration tests

Test every supported old schema to current schema.

---

# 43. USER CONFIRMATION RULE

Do not ask the user unnecessary questions.

Use supplied documents to infer structure first.

Ask only when a genuine administrative ambiguity cannot safely be resolved.

Prefer presenting:

```text
I found these three interpretations.
The workbook supports A and B but cannot distinguish them.
I recommend A because...
```

over asking vague questions.

---

# 44. ASSUMPTION REGISTER

Maintain:

```text
docs/ASSUMPTIONS.md
```

Every non-confirmed administrative rule must be recorded.

Each assumption contains:

```text
ID
Description
Reason
Impact
Status
Evidence
Resolution
```

States:

- CONFIRMED
- CONFIGURABLE
- ASSUMED
- REJECTED

Before hard-coding a business rule, verify its state.

---

# 45. REQUIREMENTS TRACEABILITY

Maintain:

```text
docs/REQUIREMENTS.md
```

Assign IDs:

```text
REQ-001
REQ-002
...
```

Example:

```text
REQ-017:
Expired absence must automatically cease affecting current strength without manual action.
```

Link tests to requirement IDs where practical.

---

# 46. TECHNICAL DECISION LOG

Maintain:

```text
docs/ADR/
```

Examples:

```text
ADR-001-net472.md
ADR-002-litedb.md
ADR-003-npoi.md
ADR-004-effective-dated-status.md
ADR-005-template-preservation.md
```

Do not repeatedly revisit settled architecture without evidence.

---

# 47. SOURCE FILE SAFETY

Never modify user-provided reference files in place.

Copy them into a controlled working/reference area.

Calculate SHA-256.

Preserve original untouched.

Generated outputs go to a separate output location.

---

# 48. DEVELOPMENT DATA SAFETY

During development, assume source files may contain real personal information.

Do not:

- upload them to external services,
- add them to public repositories,
- commit them into Git,
- include them in screenshots unnecessarily,
- paste their contents into public logs.

Add patterns to `.gitignore`.

Prefer sanitized fixture copies for automated tests.

---

# 49. GIT REPOSITORY STANDARD

Repository should contain:

```text
/src
/tests
/docs
/tools
/installer
/templates-sample
```

Root files:

```text
README.md
CHANGELOG.md
.gitignore
LICENSE or internal notice
Directory.Build.props if compatible/useful
```

No secrets.

No production DB.

No real personnel list.

No private official document unless repository policy explicitly permits it.

---

# 50. IMPLEMENTATION STYLE

Code quality requirements:

- descriptive names
- small cohesive classes
- no god ViewModels
- no god services
- no static service locator
- no hidden global mutable state
- no SQL/database calls from Views
- no duplicated date rules
- no magic status strings
- no silent catch blocks
- no swallowed file errors
- no unexplained magic numbers
- no unfinished placeholder buttons
- no fake output
- no TODO in completed critical workflow

Use dependency injection or a simple composition root compatible with the target framework.

Avoid introducing a huge DI package if unnecessary.

---

# 51. MVVM REQUIREMENTS

Implement clean MVVM.

At minimum:

```text
ViewModelBase
RelayCommand
navigation service
dialog service
validation pattern
```

View code-behind may contain purely visual behavior when justified.

It must not contain business logic.

---

# 52. USER SAFETY AGAINST ACCIDENTAL CHANGES

Destructive actions require clear confirmation.

Examples:

- archive personnel
- cancel status event
- restore backup
- rollback import
- delete template
- modify security users

Do not use generic:

"Are you sure?"

Use specific confirmations:

```text
Θα αρχειοθετηθεί ο:
Λοχίας Παπαδόπουλος Ιωάννης

Η εγγραφή θα παραμείνει διαθέσιμη στο ιστορικό.

Συνέχεια;
```

---

# 53. UNDO / CORRECTION MODEL

For administrative records, prefer:

- edit with audit history,
- cancellation flag,
- corrective event,

over physical deletion.

If event cancellation is used:

```text
IsCancelled
CancelledAt
CancelledBy
CancellationReason
```

Historical reports should ignore cancelled records while the audit trail preserves their existence.

---

# 54. REPORT PREVIEW

Before generating/printing official reports, offer a preview.

Show:

- report date
- scope/unit
- totals
- warnings
- template version
- unresolved mappings if any

Never generate an official output if mandatory mappings are unresolved.

---

# 55. DATA VALIDATION UX

Validation should appear near the relevant field.

Do not force operators to read giant error dialogs.

Examples:

```text
Η ημερομηνία επιστροφής πρέπει να είναι μετά την ημερομηνία έναρξης.
```

```text
Υπάρχει ήδη ενεργή απουσία για το επιλεγμένο χρονικό διάστημα.
```

```text
Ο βαθμός δεν αντιστοιχεί σε ενεργή εγγραφή.
```

---

# 56. SEARCH

Search must be fast.

Search personnel by:

- surname
- first name
- registry number if used
- rank
- organisation section

Handle:

- case differences
- extra spaces
- common Greek accent differences where safe

Normalize values for searching but preserve original display data.

---

# 57. DUPLICATE DETECTION

Do not identify personnel only by full name.

Names may repeat.

Duplicate detection should use available identifiers.

Candidate confidence levels:

```text
Exact identifier match → definite
Same name + same rank + same unit → probable
Same name only → possible
```

Present duplicate matches during import preview.

Never merge automatically solely by name.

---

# 58. CONFIGURATION

Configurable administrative catalogues:

- ranks
- status types
- service types
- organisation units
- report mappings
- display options
- backup policy
- inactivity timeout

Changes to report-affecting configuration should be audited.

---

# 59. DATA REPAIR TOOLING

Administrator-only data tools may include:

- consistency scan
- rebuild indexes
- validate template mappings
- validate orphan references
- verify database integrity
- export diagnostic report

Do not add arbitrary raw DB editing into the UI.

---

# 60. FINAL RELEASE BAR

The application is NOT done merely because:

- it compiles
- dashboard opens
- CRUD works
- sample Excel exports
- it runs on Windows 11

Release requires:

1. real source workbook analysed,
2. mappings documented,
3. official template output verified,
4. personnel lifecycle works,
5. absence automation works,
6. historical reports work,
7. Excel import preview works,
8. backup/restore works,
9. audit works,
10. Windows 7 compatibility verified,
11. installer verified,
12. regression tests pass.

---

# 61. REQUIRED DEVELOPMENT PHASES

Work strictly in phases.

Do not jump directly to UI implementation.

## PHASE 0 — Environment assessment

Output:

```text
Environment
Framework target
Build tools
Dependency compatibility
Win7 risks
Repository state
```

Do not modify code until you understand existing repository state, if any.

---

## PHASE 1 — Source document reverse engineering

Analyse every supplied workbook/PDF.

Deliver:

```text
docs/source-analysis/
```

Include:

- workbook structure
- report meaning
- likely fields
- formulas
- mappings
- unanswered questions
- screenshots/diagrams only if safe
- source hashes

Stop if critical parts are unreadable.

Do not invent them.

---

## PHASE 2 — Domain specification

Deliver:

```text
docs/DOMAIN.md
```

Define:

- Personnel
- Rank
- Organisation
- Lifecycle
- StatusEvent
- StatusType
- ServiceAssignment
- ServiceType
- StrengthSnapshot
- ReportMapping
- ImportBatch
- AuditEvent

Clearly distinguish:

- persisted entity
- calculated projection
- report-only projection

---

## PHASE 3 — Architecture scaffold

Create:

- solution
- projects
- references
- composition root
- logging
- config
- basic tests

Build must succeed before features continue.

---

## PHASE 4 — Persistence

Implement:

- database creation
- collections
- indexes
- repositories
- schema version
- migration framework
- initial backup system

Add tests.

---

## PHASE 5 — Personnel

Implement:

- personnel CRUD
- archive
- lifecycle dates
- rank catalogue
- organisation catalogue
- search
- personnel details
- audit

Add tests.

---

## PHASE 6 — Status engine

Implement:

- status types
- dated status events
- date semantics
- automatic expiration
- conflict engine
- current state
- historical state

This is a critical gate.

Do not continue until automated tests pass.

---

## PHASE 7 — Strength engine

Implement:

- active strength
- present
- absent
- excluded
- grouping
- historical snapshot
- totals
- filters

Verify invariants.

---

## PHASE 8 — Services

Implement:

- service type catalogue
- assignments
- history
- today/tomorrow
- reporting mapping if required

---

## PHASE 9 — Desktop UX

Implement final shell and workflows.

Prioritize:

- speed
- clarity
- keyboard operation
- low-resolution support
- old-PC rendering performance

No visual slop.

---

## PHASE 10 — Excel import

Implement:

- workbook inspection
- mapping
- import profile
- validation
- preview
- backup
- commit
- audit

Never skip preview.

---

## PHASE 11 — Excel official reporting

Implement template-safe writing.

Use golden regression tests.

The output must match approved structure.

---

## PHASE 12 — PDF parsing

Implement known-form parsers only.

Preview before persistence.

---

## PHASE 13 — PDF / print reporting

Implement:

- preview
- PDF
- direct print
- Greek rendering
- page setup

---

## PHASE 14 — Security

Implement:

- users/roles if approved
- credential security
- inactivity lock
- DB protection
- permission checks
- audit restrictions

---

## PHASE 15 — Reliability

Implement:

- backups
- restore verification
- consistency checker
- diagnostic logs
- error recovery

---

## PHASE 16 — Win7 qualification

Test:

- x86
- x64
- framework bootstrap
- Excel
- PDF
- printing
- Greek fonts
- large tables
- installer
- upgrade

Document failures.

Fix before release.

---

## PHASE 17 — Installer

Build offline installer.

Verify clean machine installation.

Verify upgrade does not destroy data.

---

## PHASE 18 — Acceptance testing

Use representative real-world workflows.

Produce:

```text
docs/ACCEPTANCE-TESTS.md
```

Pass/fail each requirement.

---

# 62. WHAT YOU MUST SHOW ME DURING DEVELOPMENT

At the end of every phase provide:

```text
PHASE:
STATUS:
FILES CREATED/CHANGED:
WHAT WORKS:
TESTS:
KNOWN RISKS:
ASSUMPTIONS:
NEXT PHASE:
```

Do not bury failures.

If something does not work, say so.

Do not claim "production-ready" without evidence.

---

# 63. BEHAVIOUR WHEN SOMETHING IS UNCLEAR

You are authorised to make engineering decisions.

Do not constantly ask the user preference questions about:

- class naming,
- internal project structure,
- repository layout,
- standard architecture details,
- trivial UI implementation choices.

Make the best senior-engineering decision.

Only ask when unresolved information changes administrative correctness.

Even then, first inspect the supplied documents and surrounding context.

---

# 64. DESIGN REVIEW REQUIREMENT

Before implementing final visual styling, produce a compact design system:

```text
Typography
Spacing
Colours
Borders
Radii
Table density
Button hierarchy
Status colours
Icon policy
Focus states
Error states
Disabled states
```

Then follow it consistently.

No random styling per screen.

---

# 65. NO "AI SLOP" RULE

Reject any implementation pattern that appears autogenerated without product thinking.

Examples:

- five giant metric cards taking half the screen
- huge blank areas
- giant gradients
- meaningless graphs
- every button having an icon
- inconsistent spacing
- every section in a rounded card
- arbitrary animation
- generic lorem-ipsum empty states
- fake generated personnel
- duplicate controls
- confusing modal chains

The application should feel like software designed after observing an administrative office.

---

# 66. OFFICIAL OUTPUT MUST BE BORING

The application UI may be modern.

The official Excel/PDF must not be "redesigned" unless explicitly requested.

The official form wins.

If the reference workbook looks old, preserve it.

The application's job is automation, not artistic reinterpretation of an official document.

---

# 67. FINAL USER EXPERIENCE TARGET

Normal morning workflow should approach:

1. Open ΔΥΝΑΜΟΛΟΓΙΟ.
2. Dashboard already shows current state.
3. Record only today's changes.
4. Press "Δυναμολόγιο".
5. Verify preview.
6. Press "Εκτύπωση" or "Excel".
7. Done.

Nobody should manually copy 40 names from one Excel sheet to another.

Nobody should manually re-add returned personnel.

Nobody should calculate totals with a calculator if the system already knows the underlying records.

Nobody should search old workbooks simply to find previous absence history.

---

# 68. ACCEPTANCE SCENARIO

The application will be considered successful when an authorised office user on a Windows 7 SP1 workstation can:

- open the app,
- maintain the active personnel list,
- add/remove/archive personnel safely,
- record a dated absence,
- record a service,
- see current personnel state,
- see who returns today/tomorrow,
- automatically treat expired absences as no longer active,
- view strength totals,
- reconstruct a previous date,
- search personnel history,
- import an approved Excel safely,
- parse supported PDFs through a review workflow,
- generate official Excel output,
- export PDF,
- print directly,
- recover from backup,
- identify who changed a record,
- do all of this without Internet access.

---

# 69. FIRST RESPONSE AFTER THIS PROMPT

Do not reply with generic enthusiasm.

Do not start generating hundreds of files before reviewing supplied references.

If no source Excel/PDF has yet been supplied, respond with a concise readiness report containing:

1. Confirmed architecture
2. Compatibility baseline
3. What you will inspect when files arrive
4. Critical technical risks
5. Initial repository structure
6. Explicit statement that report mapping will not be invented

Then wait for the reference files.

If reference files ARE supplied with this prompt, begin PHASE 0 and PHASE 1 immediately.

---

# 70. FINAL ENGINEERING DIRECTIVE

Treat this as a mission-critical administrative workstation application.

Optimise for:

1. correctness
2. reliability
3. historical traceability
4. speed of daily office use
5. exact official output
6. offline operation
7. legacy compatibility
8. maintainability
9. data safety
10. visual professionalism

Do not optimise for technological novelty.

The best architecture here is deliberately conservative:

C# + WPF + .NET Framework + embedded local persistence + deterministic business rules + exact document templates.

The success criterion is not:

"Looks impressive in a demo."

It is:

> "The office no longer needs to manually rebuild the Δυναμολόγιο in Excel every day."

Build toward that outcome relentlessly.
