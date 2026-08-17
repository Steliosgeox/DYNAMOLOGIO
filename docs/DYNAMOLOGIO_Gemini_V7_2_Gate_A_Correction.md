# DYNAMOLOGIO V7.2 — GATE A COMPLETION / SENIOR CORRECTION

Repository: `Steliosgeox/DYNAMOLOGIO`

SOURCE BRANCH:
`gemini/v7-code-quality-a`

EXPECTED REMOTE HEAD:
`38a5f605f2014304a2d1db0193bab029207831d3`

CREATE:
`gemini/v7-code-quality-a2`

DO NOT MERGE TO MAIN.
DO NOT CREATE A PR.
DO NOT START GATE B.
DO NOT TOUCH VISUAL DESIGN OR REPORT LAYOUTS IN THIS PASS.
DO NOT WRITE GOAL_COMPLETE.
DO NOT SAY PRODUCTION-READY.
DO NOT GENERATE A CELEBRATORY SUMMARY.

The senior review rejects Gate A as incomplete.

Before changing code run:

```powershell
git status
git branch --show-current
git rev-parse HEAD
git ls-remote origin refs/heads/gemini/v7-code-quality-a
```

Both local and remote source SHA must equal:

`38a5f605f2014304a2d1db0193bab029207831d3`

Then:

```powershell
git checkout -b gemini/v7-code-quality-a2
```

============================================================
CONFIRMED CURRENT DEFECTS
============================================================

## 1. Typed navigation is only partial

Good:
- `NavigationSection` exists.
- `INavigationService` exists.
- feature ViewModels no longer depend on MainViewModel for typed navigation.

Still wrong:
- `MainWindow.xaml` still has ten copy/pasted RadioButtons.
- `CommandParameter` values are still strings such as `"Dashboard"` and `"Absences"`.
- `MainViewModel.NavigateCommand` still parses strings with `Enum.TryParse`.
- requested `NavigationItemViewModel` does not exist.
- `NavigationService` comments claim it owns ViewModel lifecycle although MainViewModel actually creates/activates VMs.

Required:
- create `NavigationItemViewModel`;
- MainViewModel exposes typed navigation items;
- MainWindow uses one ItemsControl/ListBox + one DataTemplate;
- CommandParameter is `NavigationSection`;
- remove all string route parsing/fallbacks;
- correct misleading comments.

## 2. ViewModelFactory became the new god object

Current factory has roughly twenty dependencies and knows the entire application service graph.

This directly violates Gate A's "no new god classes" rule.

Replace it with a small delegate registry:

```csharp
public sealed class ViewModelFactory : IViewModelFactory
{
    private readonly IReadOnlyDictionary<NavigationSection, Func<ViewModelBase>> _factories;
    public ViewModelBase Create(NavigationSection section) { ... }
}
```

Construct/register the delegates in `App.xaml.cs`, which is the real composition root.

DO NOT move the 20 dependencies into:
- AppContext
- ServiceBag
- Manager
- ServiceProvider
- global singleton
- service locator.

## 3. WPF isolation is incomplete

`PersonnelViewModel` still imports `Dynamologio.App.Views`, constructs `PersonEditorDialog`, and calls `ShowDialog()`.

Create:
`IPersonEditorDialogService`

The WPF implementation owns `PersonEditorDialog`.
PersonnelViewModel never constructs a Window.

Also remove dead `INotificationService` dependencies where unused.

## 4. Dynamologio VM/XAML binding regression

The ViewModel removed:
- TemplateStatusBgBrush
- TemplateStatusBorderBrush
- TemplateStatusBrush
- TemplateStatusText

but `DynamologioView.xaml` still binds to those old properties.

This is a runtime binding bug.

Required:
- use the existing reporting `TemplateVerificationStatus` enum directly;
- default must be `Unverified`, never `Verified`;
- keep `TemplateStatusMessage`;
- map visuals in XAML through DataTriggers for:
  - Verified
  - Missing
  - ShaMismatch
  - Unmapped
  - Unverified
- add binding regression test.

Do not create a second reduced enum.

============================================================
A6 — READ QUERY SERVICES ARE MISSING
============================================================

There is no query layer. Key ViewModels still inject IUnitOfWork and manually join entities.

Implement focused read/query services.

Minimum:

```csharp
public interface IStrengthQueryService
{
    UnitStrengthSnapshot GetCurrent(Guid? unitId = null);
    UnitStrengthSnapshot GetAt(DateTime timestamp, Guid? unitId = null);
    IReadOnlyList<OrganisationUnit> GetOrganisationUnits();
}
```

```csharp
public interface IAbsenceQueryService
{
    AbsenceScreenData GetScreenData(DateTime timestamp);
    IReadOnlyList<PersonnelLookupItem> GetPersonnelLookup();
    IReadOnlyList<StatusType> GetActiveStatusTypes();
}
```

```csharp
public interface IServiceRosterQueryService
{
    IReadOnlyList<ServiceRosterItem> GetForDate(DateTime date);
    IReadOnlyList<PersonnelLookupItem> GetPersonnelLookup();
    IReadOnlyList<ServiceType> GetActiveServiceTypes();
}
```

```csharp
public interface IPersonnelQueryService
{
    IReadOnlyList<PersonnelStatusSnapshot> GetPersonnel(DateTime timestamp);
    PersonnelDetails GetDetails(Guid personnelId);
    IReadOnlyList<Rank> GetRanks();
    IReadOnlyList<OrganisationUnit> GetUnits();
}
```

After this work these ViewModels MUST NOT depend on IUnitOfWork:
- DashboardViewModel
- DynamologioViewModel
- PersonnelViewModel
- AbsencesViewModel
- ServicesViewModel
- ReportsViewModel

No duplicate rank/unit/person/status dictionary joins may remain inside them.

============================================================
A7 — PERSONNEL PICKER IS STILL OBJECT/REFLECTION BASED
============================================================

Current control still has:
- `object SourceItem`
- `object SelectedItem`
- `IEnumerable`
- reflection over property names.

Replace it with a typed contract:

```csharp
public sealed class PersonnelLookupItem
{
    public Guid PersonnelId { get; set; }
    public string Rank { get; set; }
    public string FullName { get; set; }
    public string Unit { get; set; }
    public string MilitaryServiceNumber { get; set; }
    public string Specialty { get; set; }
    public string NormalizedSearchText { get; set; }
}
```

Picker:
```csharp
IEnumerable<PersonnelLookupItem> ItemsSource
PersonnelLookupItem SelectedItem
```

No reflection.
No arbitrary object conversion.
No `SourceItem`.

Use `ICollectionView` or typed filtering.

============================================================
A8 — WRITE-SIDE VALIDATION IS STILL NOT AUTHORITATIVE
============================================================

Current `DomainServices.cs` still writes almost anything supplied by callers.

### PersonnelService
Must itself enforce:
- required names;
- valid RankId;
- valid OrganisationUnitId if required;
- normalized ASM uniqueness;
- rank/category consistency;
- existing entity check on update/archive.

### AbsenceService
Must itself enforce:
- person exists;
- StatusType exists and is active;
- person lifecycle;
- valid half-open interval;
- overlap/exclusion conflicts;
- mandatory fields according to StatusType.

### DutyService
Must itself enforce:
- person exists;
- ServiceType exists and is active;
- lifecycle;
- valid start/end;
- overlapping assignment;
- absence conflict policy.

ViewModel validation remains only early user feedback.
The application/write service is authoritative.

============================================================
A9 — CURRENT ACTOR + CLOCK POLICY IS MISSING
============================================================

Create:

```csharp
public interface ICurrentActor
{
    string UserName { get; }
    string DisplayName { get; }
}
```

Add Windows implementation.

Extend clock:

```csharp
public interface IClock
{
    DateTime Now { get; }
    DateTime Today { get; }
    DateTime UtcNow { get; }
}
```

Policy:
- CreatedAt / ModifiedAt / audit timestamps = UTC
- domain day/effective dates = operational local date
- direct DateTime.Now/Today/UtcNow may appear only inside clock/platform adapters.

Mutation code must not use `Environment.UserName` directly.

============================================================
A10 — METADATA OWNERSHIP IS STILL WRONG
============================================================

Current EntityBase still defaults to:
- DateTime.Now
- "SYSTEM"

Current LiteDbRepository still overwrites CreatedAt/ModifiedAt.

Fix:
- entity defaults do not invent actor/time;
- write/application services populate metadata;
- repository only persists;
- update must preserve original CreatedAt/CreatedBy;
- ModifiedAt/ModifiedBy comes from clock/current actor.

============================================================
A11 — TRANSACTION RUNNER IS MISSING
============================================================

Create:

```csharp
public interface ITransactionRunner
{
    void Execute(Action action);
    T Execute<T>(Func<T> action);
}
```

LiteDB implementation owns:
Begin -> execute -> Commit
and Rollback on exception.

Use it in PersonnelService, AbsenceService and DutyService.

Audit remains inside the same transaction.

============================================================
A12 — NOT-FOUND STILL SILENTLY SUCCEEDS
============================================================

Current archive/cancel methods can commit without mutation when ID is missing.

Create explicit application exceptions/results.

Required:
- UpdatePerson missing -> fail
- ArchivePerson missing -> fail
- CancelAbsence missing -> fail
- CancelDuty missing -> fail

No silent success.

============================================================
ADDITIONAL REQUIRED CORRECTIONS
============================================================

### MainViewModel still queries IUnitOfWork
Replace repository access with `IDeploymentSettingsService` or shell configuration service.

### Hard-coded office identity remains
Remove `"1ο ΓΡΑΦΕΙΟ"` default.
Use neutral `"ΓΡΑΦΕΙΟ / ΤΜΗΜΑ"` or not-configured state.

### Screenshot tests still dirty the repository
Automated tests must NOT write PNGs into `docs/screenshots/v6`.

Write to TestResults/temp/artifact directory only.
Committing screenshots must be an explicit manual review action.

### Empty catches
Do not add or retain silent `catch { }` in modified Gate A paths.

============================================================
TEST SUITE REQUIRED FOR GATE A
============================================================

Do not keep adding Gate A tests to one giant `DynamologioTests.cs`.

Create separate test classes/files.

### NavigationTests.cs
1. typed NavigationSection creates expected VM
2. shell navigation uses enum-backed items
3. unsupported enum fails predictably
4. feature VM constructors do not depend on MainViewModel

### ViewModelArchitectureTests.cs
5. no MessageBox in ViewModels
6. no OpenFileDialog
7. no SaveFileDialog
8. no PrintDialog
9. no SolidColorBrush
10. no feature VM references `Dynamologio.App.Views`
11. migrated six feature VMs contain no IUnitOfWork dependency
12. no `_mainVM`
13. no `Navigate("`
14. SearchablePersonPicker contains no reflection fallback

### TemplateBindingTests.cs
15. DynamologioView bindings exist on VM
16. default template state = Unverified
17. Unmapped remains Unmapped

### AbsenceServiceTests.cs
18. invalid interval rejected directly
19. overlap rejected directly
20. missing person rejected
21. missing StatusType rejected
22. inactive StatusType rejected

### DutyServiceTests.cs
23. invalid interval rejected directly
24. overlapping duty rejected
25. missing person rejected
26. missing ServiceType rejected
27. absence conflict policy exercised

### PersonnelServiceTests.cs
28. duplicate normalized ASM rejected
29. invalid RankId rejected
30. inconsistent rank/category rejected
31. update nonexistent fails
32. archive nonexistent fails

### MetadataTests.cs
33. FixedClock controls timestamps
34. fake ICurrentActor controls actor fields
35. repository does not overwrite metadata

### PersonnelLookupTests.cs
36. rank filtering
37. name filtering
38. unit filtering
39. ASM filtering
40. specialty filtering
41. selection returns PersonnelLookupItem
42. no reflection path

### TransactionRunnerTests.cs
43. failure rolls back
44. success commits
45. audit failure rolls back mutation

Tests must exercise behavior, not only scan names.

============================================================
COMMIT PLAN
============================================================

Create exactly these focused commits:

1. `fix(navigation): complete typed data-driven shell navigation`
2. `refactor(factory): remove god factory and repository access from shell`
3. `refactor(read): add feature query services and read models`
4. `refactor(domain): enforce authoritative mutation validation`
5. `refactor(metadata): add current actor clock policy and transaction runner`
6. `refactor(picker): replace reflection picker with typed personnel lookup`
7. `test(gate-a): add architecture service metadata and lookup regression suites`

After EACH commit:

```powershell
dotnet build Dynamologio.sln
dotnet test Dynamologio.sln
git status
```

Fix failures before next commit.

============================================================
EVIDENCE
============================================================

Create:
`docs/V7-GATE-A-EVIDENCE.md`

Exact table:

| Requirement | Before | Exact New Code Path | Exact Test | Result |
|---|---|---|---|---|

Allowed results:
- PASS
- FAIL
- MANUAL CHECK REQUIRED
- BLOCKED

Also record:
- source SHA
- final branch SHA
- build result
- test result
- total tests
- GitHub CI = NONE unless actual remote checks exist

============================================================
REMOTE END STATE
============================================================

At end:

```powershell
git push -u origin gemini/v7-code-quality-a2
git ls-remote origin refs/heads/gemini/v7-code-quality-a2
git log --oneline 38a5f605f2014304a2d1db0193bab029207831d3..HEAD
git status
```

Remote SHA must match local HEAD.

STOP.

Do not create PR.
Do not merge.
Do not start Gate B.

============================================================
FINAL RESPONSE FORMAT
============================================================

Return only:

V7.2 GATE A CORRECTION STATUS

Source SHA:
Branch:
Remote SHA:

Commits:
1.
2.
3.
4.
5.
6.
7.

Navigation:
God factory removed:
MainViewModel repository access:
WPF dialog isolation:
Dynamologio binding regression:
Query services:
Typed picker:
Authoritative Personnel validation:
Authoritative Absence validation:
Authoritative Duty validation:
Clock:
Current actor:
Metadata:
Transaction runner:
Not-found behavior:

Tests:
Build:
GitHub CI:
Evidence file:

Known failures:
Manual checks:

STOPPED FOR SENIOR REVIEW

No emoji.
No GOAL_COMPLETE.
No production-ready claim.
No PR link.
