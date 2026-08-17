# DYNAMOLOGIO V5 — FINAL TRUTH-GATE / PRODUCTION BLOCKER REMEDIATION

Repository: `Steliosgeox/DYNAMOLOGIO`
Source branch: `gemini/v4-blocker-remediation`
Reviewed head: `aeff9528e1fae4b2008a1968da63b03c3bd61976`

Create a NEW branch:
`gemini/v5-truth-gate`

DO NOT MERGE TO MAIN.
DO NOT WRITE GOAL_COMPLETE.
DO NOT USE "VERIFIED" UNLESS THE SPECIFIC TEST ACTUALLY PROVES THE CLAIM.
DO NOT GENERATE A MARKETING SUMMARY.

V4 is rejected because the repository still contains direct contradictions between implementation, tests, screenshots, and the V4 status report.

# P0 SECURITY

## SEC-001 — You removed one static secret and introduced another
File:
`src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs`

Current:
```csharp
private static string GetDefaultBackupPassphrase()
{
    return "DynamologioMachineBoundBackupKey2026";
}
```

This is a hard-coded backup encryption secret in a public repository.

The comment says "machine-bound". It is not machine-bound.

Required:
- delete it;
- no static fallback or default backup password anywhere;
- derive/store a backup key through a protected key-management design;
- if backups must be restorable on another machine, design a recovery/export key mechanism explicitly;
- never put the recovery secret in source.

Add repository scan test for known static credential patterns AND behavioral tests.

## SEC-002 — V4 legacy migration leaves a PLAINTEXT backup behind
`LiteDbContext.MigrateLegacyPlaintextDbIfNeeded()` creates:
`dynamologio.db.plaintext.bak`

On successful migration it is never deleted or securely protected.

Result:
you encrypt the live DB but leave the original plaintext personnel database next to it.

Required:
- never leave a plaintext legacy copy after successful migration;
- recovery copy must be encrypted/protected before the plaintext source is retired;
- define failure behavior;
- add test that successful migration leaves zero plaintext DB copies.

## SEC-003 — Migration test does not prove DPAPI failure closes
`AT_SEC_001_NoStaticFallbackSecret_KeyFailureFailsClosed` does not simulate DPAPI/key-store failure.
It simply creates an encrypted DB with an explicit password and tries a wrong password.

Rename the test or implement a testable key-provider abstraction:
`IKeyProtectionProvider`

Inject a failing provider and prove startup fails closed.

No test name may claim behavior it does not exercise.

## SEC-004 — Legacy migration is not atomic and validation is weak
The migration:
- copies source to `.plaintext.bak`;
- copies documents to staging;
- verifies only that staging can open / collection names can be read;
- uses `File.Copy(staging, live, true)`.

Required:
- validate collection counts and critical schema before switch;
- preserve/recreate required indexes;
- use a safe replacement strategy appropriate to Windows 7;
- recovery artifacts must not remain plaintext;
- add interruption/failure injection between each migration phase.

## SEC-005 — Production restore is likely broken for the encrypted production DB
Backup is created from the physical LiteDB file.

In production that physical DB is already LiteDB-password encrypted with the DPAPI-protected master key.

The backup then AES-encrypts those already-encrypted bytes.

During restore, after outer AES decryption, V4 validates staging with:
```csharp
new LiteDatabase($"Filename={stagingPath};Connection=direct")
```
WITHOUT the LiteDB database password.

Your tests use custom test databases that are intentionally created without the production DPAPI password, so they do not exercise this production case.

Required:
- test restore using an actual encrypted LiteDB source DB;
- staging validation must use the correct DB key;
- backup/lifecycle service must have a legitimate key provider, not know a hard-coded password;
- add production-equivalent restore test.

## SEC-006 — Disaster recovery to a replacement machine is undefined
The raw database is encrypted with a DPAPI LocalMachine-protected key.

If the PC/disk dies and only the backup survives, a replacement machine does not possess the original DPAPI key.

Outer AES backup encryption does not solve this because after decrypting it you still have the inner LiteDB-encrypted file.

Required:
Choose and document:
A. backups are same-machine-only; OR
B. portable disaster recovery is required.

If B:
- include a secure, explicitly recoverable key-wrapping design;
- recovery secret must be supplied/stored separately;
- test restore on a simulated different key provider/machine.

Do not claim disaster recovery until this works.

## SEC-007 — HMAC does not authenticate manifest.json
Current HMAC covers:
Magic || Version || Salt || IV || Ciphertext

`manifest.json` is outside that authenticated payload.

Required:
- authenticate canonical manifest metadata too, OR move authenticated metadata into the protected envelope;
- reject manifest substitution/modification;
- add manifest-tamper test.

## SEC-008 — Static backup passphrase defeats HMAC authenticity
Because V4's backup secret is public source code, anybody with the backup and repository can derive the HMAC key and recompute a valid tag.

Until SEC-001 is fixed, the "authenticated backup" claim is invalid against a malicious attacker.

## SEC-009 — PBKDF2 hash algorithm is still implicit
Current:
```csharp
new Rfc2898DeriveBytes(passphrase, salt, 100000)
```

The legacy overload does not explicitly select SHA-256.

V4 specifically required explicit hash-algorithm selection if supported by the exact target.

Required:
- explicitly select supported PRF on net472;
- document exact primitive;
- add deterministic known-answer test for KDF parameters;
- store KDF version/iteration/hash identifier in the authenticated format.

## SEC-010 — Restore lifecycle still does NOT replace the active application DB context
`BackupService.RestoreBackup()` still has no ownership of `LiteDbContext`.
`SettingsViewModel` still calls Restore then refreshes the same MainViewModel.
`App.xaml.cs` still creates the DB/UoW once.

There is no database lifecycle coordinator.

Required:
Implement:
`IDatabaseLifecycleCoordinator`

It must:
- block new writes;
- dispose current repositories/UoW/context;
- validate staging with the real key;
- switch;
- recreate context/UoW/services/ViewModels;
- rebind the window;
- rollback on any stage failure.

Add integration test with actual context recreation.

## SEC-011 — Automatic backup consistency is still not proven
Backup reads/copies the physical DB while LiteDB is open and being used.

Required:
Use a database-consistent backup/snapshot strategy proven for the exact LiteDB version.
Test a write occurring around backup creation and validate restored DB integrity.

## SEC-012 — "Auto-backup timestamp tracking" claim is false
`GetLastAutoBackupStatus(out lastAttempt, out lastSuccess)` initializes both to null and never parses/sets them.

Required:
Persist structured backup health, not scrape free-text logs:
- LastAttemptUtc
- LastSuccessUtc
- LastFailureCode
- LastFailureMessageSafe
- BackupHealth

Expose it in UI.

## SEC-013 — Installer ACL claim remains false
V4 did not modify `installer/setup.iss`.
There are still no explicit ACL/Permissions entries for ProgramData.

Threat model says installer ACLs exist.

Required:
Either implement and verify them or mark NOT IMPLEMENTED.
Never lie in the threat model.

## SEC-014 — Authorization remains NOT IMPLEMENTED
Attribution is not authorization.

Implement or explicitly block release on:
- operator access
- admin access
- read-only behavior
- startup authorization
- write authorization.

If Windows groups are chosen, actually check WindowsPrincipal membership.

## SEC-015 — Release signing remains absent
Branch commit is unsigned and installer has no Authenticode signing pipeline.

Keep signing = NOT IMPLEMENTED until actual release signing is configured and verified.

# P0 REPORTING

## REP-001 — Template can be reported VERIFIED with NO expected SHA
Current `CheckTemplateStatus()`:
- finds active template if present;
- expectedSha may remain empty;
- if file exists and expectedSha is empty, returns `Verified`.

This is invalid.

Required:
Verified requires ALL:
- active ReportTemplate exists;
- expected SHA non-empty;
- file exists;
- actual SHA matches;
- mapping exists;
- mandatory mapping validated;
- template version supported.

Otherwise return Missing/Unverified/ShaMismatch/Unmapped.

## REP-002 — CellMappingJson is still unused
`ReportTemplate.CellMappingJson` exists but `NpoiTemplateWriter` still writes hardcoded cells:
- B1/B2
- summary rows 4/5/6
- second sheet starting row 4
etc.

Required:
Create typed mapping model and remove hard-coded official cell coordinates from production writer.

Golden fixture may have a mapping profile, but production logic cannot assume those cells.

## REP-003 — CustomTemplatePath is effectively ignored by template verification
`ReportGenerationRequest.CustomTemplatePath` exists.
`CheckTemplateStatus()` does not accept/use the request.

Fix or remove the misleading field.

## REP-004 — Four Excel report types are still the same workbook
All four `ReportType` branches call:
`GenerateDynamologioWorkbook(...)`

Only the title differs.

This is NOT four distinct report implementations.

Either:
- implement actual renderers per report type, or
- expose only DailyDynamologio.

## REP-005 — Printable ServiceRoster is wrong
`GeneratePrintableDocument()` chooses:
- Absent => AbsentPersonnel
- everything else => PresentPersonnel

Therefore ServiceRoster does not render service assignments at all.

Fix and test actual service rows/type/time/location.

## REP-006 — Existing tests do not prove SHA enforcement
`AT_REPORT_001_MissingTemplate_BlocksExport` only tests missing-template behavior.

Add:
- empty expected SHA => NOT verified
- wrong SHA => blocked
- missing mapping => blocked
- mapping targets wrong sheet/cell => blocked
- modified template after registration => blocked
- correct template+mapping => export succeeds without altering protected/static regions

# P0 IMPORT

## IMP-001 — There is still no interactive import wizard
`ImportExportViewModel` remains:
Browse -> Analyze -> Commit

It does not expose:
- sheet choice
- header-row choice
- column mapping UI
- rank/unit resolution UI
- step state machine.

The service accepting a `Dictionary<string,int>` is not an interactive wizard if no UI constructs it.

Implement the actual user workflow.

## IMP-002 — Default actor "OPERATOR" remains in import API
Current:
```csharp
CommitImport(..., string operatorUsername = "OPERATOR")
```

Remove generic production actor.
Use current authenticated identity/access context.

## IMP-003 — Import mutations are not audited atomically
Commit writes Personnel and ImportBatch in a transaction, but does not create structured AuditEvents for imported mutations/batch.

Implement provenance + audit in same transaction.

## IMP-004 — Name fallback can update wrong person
If ASM is absent/unmatched, importer matches by LastName + FirstName and may update the first matching record.

Duplicate names are normal.

Required:
- ambiguous name match => resolution required;
- never auto-update by non-unique name;
- add duplicate-name test.

## IMP-005 — StrengthStartDate is silently set to DateTime.Today
New imported personnel receive:
`StrengthStartDate = DateTime.Today`

This may corrupt historical strength.

Required:
- import/mapping supports effective start date where available;
- if unavailable, require explicit import-effective date from operator;
- use IClock, not DateTime.Today.

## IMP-006 — No actual mid-import failure injection test
Add a repository/service hook that throws after N writes and prove:
- Personnel unchanged
- ImportBatch absent
- Audits absent
after rollback.

# P1 UI / EVIDENCE

## UI-001 — "20 screenshots verify 1024 layout" is false
The screenshot test renders each `UserControl` directly at 1024x768.

It does NOT render `MainWindow`.

Therefore it bypasses:
- 220px sidebar
- top header
- status bar
- actual ContentControl width.

A 1024-wide child view is not the same as a 1024-wide application window whose content area is roughly 804px.

Required:
Render/show the complete MainWindow at actual outer window size.

## UI-002 — Screenshot test does not inspect visual correctness
It passes if PNG encoding succeeds.
It has no assertions for:
- clipping
- overlapping controls
- blank rendering
- horizontal overflow
- hidden actions
- active nav
- unreadable text.

Screenshots are evidence artifacts, not automated visual verification.

Rename status to `SCREENSHOT CAPTURED`, not `VERIFIED BY TEST`.

## UI-003 — Active-nav test does not test active navigation
`AT_UI_001_AllPrimaryViewsInstantiateOnSTA` only constructs Views and checks NotNull.

It does not click navigation or inspect RadioButton state/indicator.

Add a real navigation behavior test or mark manual verification.

## UI-004 — DatePicker still has no custom ControlTemplate
V4 `DesignSystem.xaml` implements a custom ComboBox template but DatePicker is still only property setters.

Stop claiming both have custom templates.

## UI-005 — SearchablePersonPicker claim overstates search dimensions
Code filters:
- LastName
- FirstName
- MilitaryServiceNumber
- Specialty

It does NOT search rank or unit as V4 claimed.

Display only shows FullName + ASM.

Use a view model item with:
- Rank
- Name
- Unit/section
- ASM
and search all promised dimensions.

## UI-006 — Absences is still fixed 380px editor + table
The page still uses:
`Width="380"` for editor.
The application shell still uses a fixed 220px sidebar.

Rework adaptive layout based on actual MainWindow content width.

## UI-007 — Empty-state claim is false for Absences
All three tabs still contain bare DataGrids with no empty overlay/presenter.

Implement real empty states across every major table.

## UI-008 — Hardcoded deployment identity remains
`MainViewModel` still defaults:
- 123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ
- 1ο ΓΡΑΦΕΙΟ

`ReportGenerationRequest` and NpoiTemplateWriter still default similar fake deployment identity.

Use neutral unset/first-run configuration.

## UI-009 — Default organisation/status/service seeds remain deployment-specific
SchemaMigrationRunner still injects infantry company structure and service/status assumptions.

Separate:
- system-required catalog schema
- demo fixture
- deployment configuration.

Do not silently configure a real workstation with invented unit structure.

## UI-010 — Screenshots must use sanitized realistic demo data
A blank database screenshot does not demonstrate dense operational usability.

Provide sanitized fixture dataset with:
- ranks
- 30+ personnel
- active/future/past absences
- services
- conflicts
- report state
and capture both normal and empty states.

# TEST HONESTY RULES

A test name must describe exactly what it tests.

Examples currently rejected:
- "NoStaticFallbackSecret_KeyFailureFailsClosed" when no DPAPI failure is simulated.
- "AllPrimaryViewsInstantiate" used as evidence for active navigation.
- screenshot file generation used as evidence for layout correctness.
- MissingTemplate test used as evidence for strict SHA enforcement.
- crypto roundtrip used as evidence for production backup policy.

Create `docs/V5-EVIDENCE-MATRIX.md`:

| Claim | Code path | Automated test | Manual evidence | Status |

Status only:
- NOT IMPLEMENTED
- IMPLEMENTED / UNVERIFIED
- VERIFIED AUTOMATED
- VERIFIED MANUAL
- BLOCKED

No claim may cite an unrelated test.

# MINIMUM REQUIRED TESTS

Security:
1. injected DPAPI/key-provider failure fails closed
2. successful migration leaves no plaintext copy
3. migration preserves all collection counts
4. migration interruption at each phase recovers
5. production encrypted LiteDB backup+restore succeeds
6. replacement-machine recovery scenario according to chosen policy
7. default automatic backup uses non-static protected secret
8. default manual backup uses protected/explicit secret
9. manifest tamper rejected
10. ciphertext tamper rejected
11. HMAC recomputation impossible without protected secret in threat model
12. wrong passphrase rejected
13. truncated envelope rejected
14. backup while concurrent DB write restores consistently
15. restore recreates context and services
16. restore failure leaves original DB operational
17. auto-backup health timestamps persist
18. unauthorized write denied if authorization is implemented

Audit/import:
19. personnel audit failure rollback
20. absence audit failure rollback
21. duty audit failure rollback
22. import failure after N rows rollback
23. import creates batch audit/provenance
24. duplicate-name ambiguity requires resolution
25. import effective start-date handling

Reporting:
26. no ReportTemplate => unverified/blocked
27. blank SHA => unverified/blocked
28. wrong SHA => blocked
29. missing mapping => blocked
30. wrong mapping => blocked
31. valid template+mapping succeeds
32. each exposed report type has distinct correct data
33. ServiceRoster includes service records, times and locations

UI:
34. MainWindow rendered at actual 1366x768
35. MainWindow rendered at actual 1024x768
36. navigation selection actually changes checked item
37. no critical WPF binding errors
38. SearchablePersonPicker searches promised fields
39. empty states visible when collection empty

# GIT

Create:
`gemini/v5-truth-gate`

Use multiple focused commits.
Push branch only.
Do not merge.

# FINAL RESPONSE FORMAT

Return:

V5 EVIDENCE STATUS

Security
DB key management:
Legacy migration:
Plaintext residue:
Backup key management:
Backup authentication:
Production restore:
Cross-machine recovery:
Backup consistency:
ACLs:
Authorization:
Auto-backup health:

Reporting
Template registration:
SHA:
Mapping:
Daily Dynamologio:
Absent report:
Present report:
Service roster:

Import
Wizard:
Column mapping:
Value resolution:
Identity ambiguity:
Audit/provenance:
Atomic rollback:

UI Evidence
Full MainWindow 1366:
Full MainWindow 1024:
Active navigation:
Search picker:
Empty states:
Realistic-data screenshots:
Win7 VM:

Verification
Build:
Automated tests:
Manual evidence:
Known blockers:

No GOAL_COMPLETE.
No "production-ready".
No unrelated test citations.
