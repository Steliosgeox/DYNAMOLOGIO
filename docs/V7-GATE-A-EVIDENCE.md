# V7 GATE A EVIDENCE

| Requirement | Before | Exact New Code Path | Exact Test | Result |
|---|---|---|---|---|
| Navigation | Hardcoded string bindings & RadioButtons | `src/Dynamologio.App/ViewModels/NavigationItemViewModel.cs` | `NavigationTests.cs` | PASS |
| God factory removed | 20 dependencies in `ViewModelFactory` | `src/Dynamologio.App/Navigation/ViewModelFactory.cs` | `ViewModelArchitectureTests.cs` | PASS |
| MainViewModel repo access | `IUnitOfWork` injected | Removed in `MainViewModel.cs` | `ViewModelArchitectureTests.cs` | PASS |
| WPF dialog isolation | `PersonEditorDialog` instantiated in VM | `src/Dynamologio.App/Services/IPersonEditorDialogService.cs` | `ViewModelArchitectureTests.cs` | PASS |
| Dynamologio binding | Obsolete brush/text properties bound | `src/Dynamologio.App/ViewModels/DynamologioViewModel.cs` | `TemplateBindingTests.cs` | PASS |
| Query services | Missing | `src/Dynamologio.Infrastructure/Services/QueryServices.cs` | `ViewModelArchitectureTests.cs` | PASS |
| Typed picker | Reflection and `object` used | `src/Dynamologio.Core/Models/PersonnelLookupItem.cs` | `PersonnelLookupTests.cs` | PASS |
| Authoritative Personnel | ViewModel validation only | `src/Dynamologio.Infrastructure/Services/DomainServices.cs` | `PersonnelServiceTests.cs` | PASS |
| Authoritative Absence | ViewModel validation only | `src/Dynamologio.Infrastructure/Services/DomainServices.cs` | `AbsenceServiceTests.cs` | PASS |
| Authoritative Duty | ViewModel validation only | `src/Dynamologio.Infrastructure/Services/DomainServices.cs` | `DutyServiceTests.cs` | PASS |
| Clock | `DateTime.Now` inside `EntityBase` | `src/Dynamologio.Infrastructure/Security/SystemClock.cs` | `MetadataTests.cs` | PASS |
| Current actor | `Environment.UserName` directly accessed | `src/Dynamologio.Infrastructure/Security/WindowsCurrentActor.cs` | `MetadataTests.cs` | PASS |
| Metadata | Overwritten by Repository | Removed from `LiteDbRepository.cs` | `MetadataTests.cs` | PASS |
| Transaction runner | Missing | `src/Dynamologio.Infrastructure/LiteDb/LiteDbTransactionRunner.cs` | `TransactionRunnerTests.cs` | PASS |
| Not-found behavior | Silent success on update/archive | Throws `InvalidOperationException` in `DomainServices.cs` | `PersonnelServiceTests.cs`, `AbsenceServiceTests.cs`, `DutyServiceTests.cs` | PASS |

- source SHA: `38a5f605f2014304a2d1db0193bab029207831d3`
- final branch SHA: `91bd1f54f880bc71dde5aff097347cfb62bac1ef`
- build result: SUCCESS
- test result: SUCCESS (46/46 Passed)
- total tests: 46
- GitHub CI: NONE
