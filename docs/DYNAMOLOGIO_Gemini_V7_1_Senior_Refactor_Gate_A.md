# DYNAMOLOGIO V7.1 — SENIOR REFACTOR GATE A
Repository: Steliosgeox/DYNAMOLOGIO

SOURCE BRANCH:
gemini/v6-visual-reboot

EXPECTED REMOTE HEAD:
e337eb84a503c22be2f1e06ea06393dab4a49f89

CREATE:
gemini/v7-code-quality-a

DO NOT MERGE TO MAIN.
DO NOT MODIFY THE VISUAL DESIGN IN THIS PASS.
DO NOT TOUCH REPORT LAYOUTS OR EXCEL TEMPLATE MAPPINGS YET.
DO NOT WRITE GOAL_COMPLETE.
DO NOT CLAIM "PRODUCTION READY".
DO NOT SCORE YOUR OWN CODE.
DO NOT WRITE A CELEBRATORY REPORT.

This phase exists because the current repository still contains AI-generated architectural slop even after the V6 visual changes.

Before coding, run:

git status
git branch --show-current
git rev-parse HEAD
git ls-remote origin refs/heads/gemini/v6-visual-reboot
git log -n 5 --oneline

If local HEAD and remote HEAD do not both equal:
e337eb84a503c22be2f1e06ea06393dab4a49f89

STOP and explain the mismatch.

============================================================
THE SENIOR REVIEWER HAS ALREADY CONFIRMED THESE DEFECTS
============================================================

1. MainViewModel is a god object.
It:
- receives a large service graph;
- constructs every child ViewModel itself;
- creates fallback domain services;
- owns navigation;
- owns deployment header state;
- refreshes every screen;
- uses string route names;
- contains a switch over every screen;
- is injected back into child ViewModels.

This is not acceptable MVVM architecture.

2. Child ViewModels depend on MainViewModel.
Examples:
DashboardViewModel
DynamologioViewModel
PersonnelViewModel
AbsencesViewModel
ServicesViewModel
ReportsViewModel
ImportExportViewModel
etc.

This creates a circular application-controller pattern.

3. ViewModels directly use WPF infrastructure.
Confirmed examples:
- MessageBox
- SaveFileDialog
- OpenFileDialog
- PrintDialog
- SolidColorBrush
- Brush
- FlowDocument-related concerns

A ViewModel must not know how Windows displays dialogs or how a status is colored.

4. ViewModels directly assemble database joins.
Dashboard/Dynamologio/Reports repeatedly call GetAll() across Personnel, Events, Types, Ranks, Units, Services, ServiceTypes.

ServicesViewModel manually constructs dictionaries and joins personnel/rank/service data.

AbsencesViewModel does the same.

This produces duplication and guarantees screens will drift.

5. Domain validation is not authoritative.
ServicesViewModel checks conflicts before calling DutyService.
AbsencesViewModel checks conflicts before calling AbsenceService.

That means a different caller can bypass the UI validation and write invalid data.

Business invariants belong in write/application services.

6. Time/actor metadata is inconsistent.
The codebase mixes:
- DateTime.Now
- DateTime.Today
- DateTime.UtcNow
- IClock
- Environment.UserName
- "SYSTEM"

Repository methods also mutate timestamps.

There must be one explicit time/actor policy.

7. SearchablePersonPicker is over-generic AI code.
It uses:
- object SourceItem
- object SelectedItem
- reflection to discover RankName/Rank/DisplayRank/etc.
- direct Popup/ListBox/TextBox manipulation

Replace it with a typed lookup model.

8. DesignSystem.xaml is still a giant monolith.
Do NOT fix that in Gate A.
It will be handled in a later WPF-specific gate.
This phase is architecture/domain/application code only.

9. ImportExportViewModel still proves the architecture problem:
- receives IUnitOfWork directly;
- receives MainViewModel but does not need it;
- creates fallback ExcelImportService itself;
- opens OpenFileDialog;
- shows MessageBox;
- performs analysis/commit synchronously;
- directly passes UoW into import service methods.

10. ServicesViewModel still proves the architecture problem:
- receives IUnitOfWork directly;
- receives MainViewModel but does not use it;
- performs form parsing;
- queries repositories;
- joins entities;
- performs conflict detection;
- opens MessageBox;
- performs write orchestration;
- refreshes itself.

That is far too many responsibilities for one ViewModel.

============================================================
GATE A — REQUIRED TARGET ARCHITECTURE
============================================================

Implement only the following architecture work.

------------------------------------------------------------
A1. TYPED NAVIGATION
------------------------------------------------------------

Create:

NavigationSection enum:
- Dashboard
- Dynamologio
- Personnel
- Absences
- Services
- Reports
- ImportExport
- DataValidation
- History
- Settings

Create:
INavigationService

It must expose typed navigation:
Navigate(NavigationSection section)

No child ViewModel may call:
_mainVM.Navigate("...")

No navigation string literals outside the navigation mapping layer.

Create:
NavigationItemViewModel
for shell navigation metadata.

MainViewModel becomes shell-only:
- CurrentViewModel
- ActiveSection
- shell status
- unit/office header

MainViewModel must NOT construct child ViewModels.

------------------------------------------------------------
A2. VIEWMODEL FACTORY
------------------------------------------------------------

Create:
IViewModelFactory

Example:
object Create(NavigationSection section)

Use explicit constructor injection.
No reflection container required.
No service locator.

App.xaml.cs remains the composition root and creates the service graph/factory.

MainViewModel receives:
- INavigationService
- IViewModelFactory
- shell-level settings service only if required

Do not pass MainViewModel into feature ViewModels.

------------------------------------------------------------
A3. ACTIVATION CONTRACT
------------------------------------------------------------

Create:
IActivatableViewModel

with:
void Activate()

or:
void Refresh()

Choose one clear convention.

NavigationService/MainViewModel calls it when a screen becomes active.

Remove:
RefreshAllViewModels() knowing every feature explicitly.

------------------------------------------------------------
A4. UI SERVICE ABSTRACTIONS
------------------------------------------------------------

Create:

IFileDialogService
- OpenExcelFile()
- SaveExcelFile(defaultName)
- SelectBackupFile()
- SelectBackupDestination() if needed

IConfirmationService
- Confirm(title, message)

INotificationService
- Info(...)
- Warning(...)
- Error(...)

IPrintService
- PrintDocument(...)

Implement WPF versions in Dynamologio.App/Services.

Move:
MessageBox
OpenFileDialog
SaveFileDialog
PrintDialog

out of ViewModels.

No ViewModel file may reference:
System.Windows.MessageBox
Microsoft.Win32.OpenFileDialog
Microsoft.Win32.SaveFileDialog
System.Windows.Controls.PrintDialog

------------------------------------------------------------
A5. REMOVE VISUAL TYPES FROM VIEWMODELS
------------------------------------------------------------

DynamologioViewModel currently builds Brushes for template state.

Replace visual properties with one semantic property:

TemplateVerificationStatus TemplateStatus

and optionally:
string TemplateStatusMessage

XAML owns:
- colors
- brushes
- border colors
through DataTriggers/converters/styles.

No ViewModel may construct:
SolidColorBrush
Brush
Color

------------------------------------------------------------
A6. READ QUERY SERVICES
------------------------------------------------------------

Create focused read services.

Minimum:

IStrengthQueryService
UnitStrengthSnapshot GetCurrent(Guid? unitId = null)
UnitStrengthSnapshot GetAt(DateTime timestamp, Guid? unitId = null)

IAbsenceQueryService
IReadOnlyList<AbsenceListItem> GetActive(DateTime timestamp)
IReadOnlyList<AbsenceListItem> GetPlanned(DateTime timestamp)
IReadOnlyList<AbsenceListItem> GetHistory(DateTime timestamp)
IReadOnlyList<PersonnelLookupItem> GetPersonnelLookup()

IServiceRosterQueryService
IReadOnlyList<ServiceRosterItem> GetForDate(DateTime date)
IReadOnlyList<PersonnelLookupItem> GetPersonnelLookup()

IPersonnelQueryService
...purpose-built personnel list/detail methods...

Do NOT expose IUnitOfWork to DashboardViewModel, DynamologioViewModel, AbsencesViewModel, ServicesViewModel, ReportsViewModel unless there is an explicit reason documented in code review.

The ViewModels consume read models, not manually assemble rank/unit dictionaries.

------------------------------------------------------------
A7. TYPED PERSONNEL LOOKUP
------------------------------------------------------------

Create:

PersonnelLookupItem
{
    Guid PersonnelId
    Personnel Personnel
    string Rank
    string FullName
    string Unit
    string MilitaryServiceNumber
    string Specialty
    string NormalizedSearchText
}

Replace SearchablePersonPicker:
- object SourceItem
- object SelectedItem
- reflection mapping

with typed:
IEnumerable<PersonnelLookupItem>
PersonnelLookupItem SelectedItem

Filter using ICollectionView or a clear typed LINQ collection.

Minimal code-behind may remain for:
- focus
- keyboard
- popup open/close

No runtime reflection.

------------------------------------------------------------
A8. WRITE-SIDE VALIDATION MUST BE AUTHORITATIVE
------------------------------------------------------------

AbsenceService.CreateAbsence must itself:
- load/check person;
- load/check StatusType;
- validate active type;
- validate dates;
- validate lifecycle;
- validate overlap/conflicts;
- reject invalid writes.

DutyService.AssignDuty must itself:
- validate person;
- validate ServiceType;
- validate lifecycle;
- validate times;
- validate overlapping duty;
- validate absence conflict according to explicit policy.

PersonnelService Create/Update must itself:
- validate required fields;
- validate ASM uniqueness;
- validate rank/category consistency.

UI validation may remain for instant feedback.
It is NOT the authority.

Add service-level tests that call write services directly without ViewModels.

------------------------------------------------------------
A9. CLOCK + CURRENT ACTOR
------------------------------------------------------------

Create:
ICurrentActor

Properties:
string UserName
string DisplayName

Windows implementation resolves the local Windows operator.

Use IClock consistently.

Business/application mutation paths may NOT use:
DateTime.Now
DateTime.Today
Environment.UserName
directly.

Allowed direct use only inside:
SystemClock
WindowsCurrentActor
low-level platform adapters that explicitly own that concern.

Define:
- domain effective dates = local date semantics where appropriate
- audit timestamps = UTC
- CreatedAt / ModifiedAt = UTC

Do not mix these casually.

------------------------------------------------------------
A10. ENTITY METADATA OWNERSHIP
------------------------------------------------------------

Repository Insert/Update must stop inventing:
CreatedAt
ModifiedAt
CreatedBy
ModifiedBy

Application/write services set metadata before persistence.

EntityBase defaults must not pretend every record was created by "SYSTEM".

For imported records, actor comes from ICurrentActor / explicit import actor.

------------------------------------------------------------
A11. TRANSACTION EXECUTION
------------------------------------------------------------

Remove repeated:

BeginTransaction
try
Commit
catch
Rollback

from every service.

Create a small abstraction/helper:

ITransactionRunner
void Execute(Action action)
T Execute<T>(Func<T> action)

Implementation wraps the UoW transaction.

Do not hide domain validation in it.
It only owns transaction mechanics.

------------------------------------------------------------
A12. NOT-FOUND IS NOT SUCCESS
------------------------------------------------------------

Current cancellation/archive methods can silently do nothing if entity ID does not exist.

Change application-service behavior.

ArchivePerson(nonexistent)
CancelAbsence(nonexistent)
CancelDuty(nonexistent)
UpdatePerson(nonexistent)

must return an explicit result or throw a known application exception.

Do not silently Commit() an empty mutation.

============================================================
REQUIRED TESTS FOR GATE A
============================================================

Create focused test classes/files.

Do NOT add everything to one DynamologioTests.cs.

Required tests:

NavigationTests
1. Navigate typed enum -> correct VM created
2. feature VM has no MainViewModel dependency
3. unknown/unsupported navigation fails predictably

ViewModelArchitectureTests
4. no file under ViewModels contains "MessageBox."
5. no file under ViewModels contains "OpenFileDialog"
6. no file under ViewModels contains "SaveFileDialog"
7. no file under ViewModels contains "SolidColorBrush"
8. no route string literals such as Navigate("Dashboard") in feature ViewModels

AbsenceServiceTests
9. invalid interval rejected directly by service
10. overlapping mutually-exclusive absence rejected directly by service
11. missing person rejected
12. missing StatusType rejected

DutyServiceTests
13. invalid interval rejected directly
14. overlapping duty rejected directly
15. missing person/service type rejected
16. absence conflict policy tested

PersonnelServiceTests
17. duplicate normalized ASM rejected directly
18. invalid rank/category combination rejected

MetadataTests
19. mutation timestamps come from FixedClock
20. actor fields come from fake ICurrentActor
21. repository does not overwrite caller-provided metadata

PersonnelLookupTests
22. rank search
23. name search
24. unit search
25. ASM search
26. specialty search
27. selection returns typed PersonnelLookupItem
28. no reflection fallback required

============================================================
CODE QUALITY RULES
============================================================

1. No new god classes.
2. No service locator.
3. No static mutable globals.
4. No "Manager" class that simply becomes the next god object.
5. No fallback "new Service()" inside ViewModel constructors.
6. Constructor dependency count above ~6 requires justification/refactor.
7. No empty catch blocks.
8. No comments claiming capability the implementation does not prove.
9. No duplicate read-model joining across feature ViewModels.
10. No UI framework objects in Core or Infrastructure.
11. No business rules in XAML code-behind.
12. No reflection-based DTO/control mapping.
13. Preserve net472 / C# 7.3.
14. Build must remain warning-clean for modified projects.

============================================================
FILES / STRUCTURE SUGGESTION
============================================================

src/Dynamologio.App/
  Navigation/
    NavigationSection.cs
    INavigationService.cs
    NavigationService.cs
    IViewModelFactory.cs
    ViewModelFactory.cs
    NavigationItemViewModel.cs

  Services/
    IFileDialogService.cs
    WpfFileDialogService.cs
    IConfirmationService.cs
    WpfConfirmationService.cs
    INotificationService.cs
    WpfNotificationService.cs
    IPrintService.cs
    WpfPrintService.cs

  ViewModels/
    MainViewModel.cs
    ...

src/Dynamologio.Core/
  Application/
    ICurrentActor.cs
    ITransactionRunner.cs
    ...
  ReadModels/
    PersonnelLookupItem.cs
    AbsenceListItem.cs
    ServiceRosterItem.cs

src/Dynamologio.Infrastructure/
  Queries/
    StrengthQueryService.cs
    AbsenceQueryService.cs
    ServiceRosterQueryService.cs
    PersonnelQueryService.cs
  Identity/
    WindowsCurrentActor.cs
  Transactions/
    LiteDbTransactionRunner.cs

Exact namespaces may differ if cleaner.
Keep dependencies pointing inward correctly.

============================================================
COMMIT PLAN
============================================================

Commit 1:
refactor(app): introduce typed navigation and viewmodel factory

Build + test.

Commit 2:
refactor(app): isolate dialogs notifications and printing from viewmodels

Build + test.

Commit 3:
refactor(read): move feature joins into typed query services

Build + test.

Commit 4:
refactor(domain): enforce write invariants inside mutation services

Build + test.

Commit 5:
refactor(metadata): unify clock actor and transaction handling

Build + test.

Commit 6:
refactor(app): replace reflection personnel picker with typed lookup model

Build + test.

Commit 7:
test(architecture): add gate-a architecture and service regression suites

Build + test.

Do not make visual redesign commits in this branch.

============================================================
HARD STOP
============================================================

After Gate A is implemented:

1. Run:
dotnet build Dynamologio.sln
dotnet test Dynamologio.sln
git status
git log -n 10 --oneline

2. Create:
docs/V7-GATE-A-EVIDENCE.md

Table:

| Requirement | Before | Exact New Code Path | Test | Result |
|---|---|---|---|---|

Allowed result values:
PASS
FAIL
MANUAL CHECK REQUIRED
BLOCKED

3. Push:
git push -u origin gemini/v7-code-quality-a

4. Verify:
git ls-remote origin refs/heads/gemini/v7-code-quality-a

5. STOP.

DO NOT START:
- reporting rewrite
- import rewrite
- security rewrite
- visual redesign
- resource dictionary redesign
- installer changes

Those are later gates after a senior review of Gate A.

============================================================
FINAL RESPONSE
============================================================

Return only:

V7 GATE A STATUS

Source remote SHA:
V7 branch remote SHA:

Commits:
1.
2.
3.
4.
5.
6.
7.

Typed navigation:
MainViewModel coupling:
Feature -> MainViewModel dependencies:
ViewModel WPF dependencies:
Read query services:
Write-side validation:
Clock policy:
Current actor:
Metadata ownership:
Transaction runner:
Not-found semantics:
Typed personnel lookup:

Architecture tests:
Service tests:
Build:
Tests:

Known failures:
Known manual checks:

STOPPED FOR SENIOR REVIEW

No emoji.
No GOAL_COMPLETE.
No production-ready claim.
