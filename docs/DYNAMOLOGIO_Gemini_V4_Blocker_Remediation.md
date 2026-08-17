# DYNAMOLOGIO V4 — BLOCKER-ONLY REMEDIATION DIRECTIVE

Repository: `Steliosgeox/DYNAMOLOGIO`
Source branch: `gemini/security-ux-v3`
Reviewed commit: `3b1ee03a350af5ba9d7717aefff024032d6418d5`

Do not merge to main. Create `gemini/v4-blocker-remediation` from V3. Do not claim completion. Do not write GOAL_COMPLETE.

## P0 SECURITY

1. REMOVE THE HARDCODED FALLBACK KEY.
`LiteDbContext.cs` currently returns `DynamologioFallbackLocalKey2026#` if DPAPI fails. This is a release blocker and contradicts the threat model. Fail closed. Never use a static fallback secret. Add a test proving DPAPI/key failure cannot open/create the DB with a fallback.

2. DEFINE DPAPI SCOPE HONESTLY.
`DataProtectionScope.LocalMachine` is machine-scoped, not user isolation. If untrusted local users exist, either use CurrentUser where operationally possible or enforce strict ACLs and document residual risk.

3. IMPLEMENT LEGACY UNENCRYPTED-DB MIGRATION.
V3 switched the normal LiteDB connection to Password=... but has no demonstrated upgrade path for an existing plaintext `dynamologio.db`. Add detection, verified safety copy, staging rebuild/migration, validation, atomic switch, rollback, wrong-key fail-closed behavior, and tests.

4. NORMAL UI/AUTO BACKUPS ARE STILL UNENCRYPTED.
`SettingsViewModel` calls `_backupService.CreateBackup(targetDir)` with no passphrase. `PerformDailyAutoBackup()` also calls `CreateBackup(backupsDir)` with no passphrase. Fix the production path so normal backups are encrypted according to a defined key/passphrase policy. Test the actual UI/service policy.

5. BACKUP AUTHENTICATION IS NOT IMPLEMENTED.
Threat model claims AES-256-CBC + HMAC-SHA256; code has no HMAC. Implement a standard authenticated construction compatible with .NET Framework 4.7.2, or use a vetted compatible format. Do not rely on unkeyed SHA-256 for authenticity. Authenticate header/version/salt/IV/ciphertext before decryption.

6. PBKDF2 CLAIM DOES NOT MATCH CODE.
Threat model says 100,000 iterations and HMAC-SHA256. Code uses `new Rfc2898DeriveBytes(passphrase, salt, 50000)`, which uses the legacy default hash. Use an explicit SHA-256-capable API on the exact target, benchmark legacy hardware, version KDF parameters, and make docs match code.

7. `VerifyBackup()` DOES NOT VERIFY ENCRYPTED PAYLOADS.
It only validates SHA-256 when `!manifest.IsEncrypted`. Create a structured verification result and require authentication/decryption validation where appropriate.

8. RESTORE LIFECYCLE IS STILL UNSAFE.
Current BackupService copies over the live DB without owning/disposal/recreation of the active LiteDB context. Introduce an application-level database lifecycle coordinator. Required: maintenance mode, close context, verify/authenticate package, staging DB, open/validate staging, schema check, replace live DB, recreate context/UoW/services/ViewModels, rollback on failure.

9. AUDIT ATOMICITY CLAIM IS FALSE.
`PersonEditorViewModel.Save()` writes Personnel. `PersonnelViewModel` audits only after dialog success. Move audited mutations to transaction-aware application services. Mutation + audit must commit together. Add failure-injection tests for Personnel, Absence and Service.

10. `Environment.UserName` IS ATTRIBUTION, NOT AUTHORIZATION.
Implement an explicit access-control model. If Windows groups are used, enforce operator/admin/read-only permissions. Audit a domain-qualified identity where possible. Do not claim RBAC without enforcement.

11. THREAT MODEL CLAIMS INSTALLER ACLS THAT DO NOT EXIST.
`installer/setup.iss` has no explicit hardened ProgramData ACL implementation. Add and test ACLs or delete the claim.

12. AUTO-BACKUP FAILURES ARE STILL SILENT.
Persist/show last attempt, last successful backup and degraded status. Log failures. No blanket silent catches.

13. UNKNOWN DISPATCHER EXCEPTIONS STILL CONTINUE.
`App.xaml.cs` still sets `args.Handled = true` for unknown dispatcher exceptions. Unknown integrity-threatening errors should log, inform and safely terminate rather than blindly continue.

## P0 FUNCTIONAL

14. REPORT TEMPLATE CONTRACT IS STILL NOT WIRED.
V3 did not modify `ReportGeneratorService.cs` or `NpoiTemplateWriter.cs`. Implement actual active `ReportTemplate` resolution, expected-vs-actual SHA-256, typed mapping validation, required mappings, output validation and audit metadata.

15. DYNAMOLOGIO HARDCODES `Πρότυπο: Επαληθευμένο`.
Bind template state to real verification: Missing / Unverified / Verified / Changed / MappingIncomplete. Never show green Verified without evidence.

16. REPORT TYPES ARE STILL COSMETIC.
Current ReportGeneratorService still uses the same Dynamologio Excel path and generic printable document regardless of ReportType. Implement every exposed report type or remove unsupported types.

17. IMPORT “INTERACTIVE 5-STAGE PIPELINE” IS FALSE.
V3 did not modify `ExcelImportPipeline.cs`. Current code still uses fallback column indexes, first sheet, no interactive mapping, and blank unit can resolve to first unit. Build a real File -> Detect -> Map -> Resolve -> Preview -> Commit flow. No fallback indexes in production mode.

18. ADD MISSING FAILURE-INJECTION TESTS.
Required: import row-N failure rolls back all, audit failure rolls back mutation, restore failure preserves current DB, legacy DB migration failure preserves legacy DB, wrong template hash blocks export, missing mapping blocks export.

## P1 UX

19. COMBOBOX/DATEPICKER “CUSTOM CONTROLTEMPLATES” CLAIM IS FALSE.
DesignSystem only sets properties for ComboBox and DatePicker. Either implement real templates or stop claiming custom templates.

20. ACTIVE CYAN NAV INDICATOR NEVER BECOMES VISIBLE.
MainWindow creates `activeIndicator` with `Visibility=Collapsed`, but active DataTriggers only change button Background/Foreground. Fix the selected navigation architecture and reduce repeated inline style duplication.

21. SIDEBAR IS STILL FIXED 220PX.
Implement adaptive/collapsible behavior for 1024px width and prove it with screenshots.

22. ABSENCES “SEARCHABLE PERSON PICKER” IS STILL A NORMAL COMBOBOX.
A XAML comment is not functionality. Implement type-to-filter search with rank/name/unit/ASM and keyboard selection.

23. ABSENCES FORM IS STILL FIXED 380PX.
Reconsider the page composition for 1024 width. Do not just change colors.

24. EMPTY STATES ARE STILL NOT IMPLEMENTED IN ABSENCE TABS.
Build a reusable EmptyState and apply it to all major grids.

25. DYNAMOLOGIO IS STILL KPI BOXES + CARD + TABS.
Improve hierarchy, reduce box soup, surface operational state, and use actual template status. Do not self-score.

26. THERE ARE NO COMMITTED SCREENSHOT ARTIFACTS.
Create sanitized real-app screenshots in `docs/screenshots/v4/` for Dashboard, Dynamologio, Personnel, Absences, Services, Reports, Import, Validation, History and Settings at 1366x768 and 1024x768.

27. WINDOWS 7 IS NOT VERIFIED.
Binary targeting is not a VM test. Keep status `NOT VERIFIED ON WINDOWS 7` until actual Win7 execution evidence exists.

## DOCUMENTATION

Rewrite `SECURITY-THREAT-MODEL.md` to match the implementation exactly. Remove false claims about:
- no hardcoded keys
- HMAC-SHA256
- 100k PBKDF2 iterations
- installer ACLs
- atomic audit
- machine-name/UTC audit if not implemented

Use only:
- NOT IMPLEMENTED
- IMPLEMENTED UNVERIFIED
- VERIFIED BY TEST
- VERIFIED MANUALLY
- BLOCKED

## REQUIRED TESTS BEFORE NEXT REVIEW

Security:
- no static fallback secret
- plaintext legacy DB -> encrypted migration
- migration interruption rollback
- encrypted DB reopen
- automatic backup encrypted
- exported backup encrypted
- wrong passphrase rejected
- authenticated bit-flip rejection
- manifest replacement rejection
- ciphertext replacement rejection
- truncated backup rejection
- restore staging failure rollback
- context recreated after restore
- audit failure rollback for personnel
- audit failure rollback for absence
- audit failure rollback for service
- unauthorized write blocked if authorization implemented

Reporting/import:
- missing template blocked
- wrong SHA blocked
- missing mapping blocked
- each exposed report type distinct
- unknown Excel layout requires mapping
- blank unit never silently maps to first unit
- import failure rolls back all

UI:
- production emoji scan
- all primary Views instantiate on STA
- no critical WPF binding errors
- selected navigation verification

## GIT

Create `gemini/v4-blocker-remediation`.
Do not modify main.
Use logical commits, not one giant “everything fixed” commit.
Push branch only.
Do not merge.

## FINAL RESPONSE

Return only:

V4 BLOCKER STATUS

Branch:
Commits:

P0 Security
Static fallback key:
Existing DB migration:
Backup encryption default:
Backup authentication:
PBKDF2:
Restore lifecycle:
Audit atomicity:
Authorization:
ACLs:
Auto-backup health:
Fatal exception policy:

P0 Functional
Template verification:
Report types:
Import mapping:
Rollback tests:

P1 UX
Active navigation:
Person picker:
Empty states:
1024 layout:
Screenshots:
Win7 VM:

Evidence
Build:
Tests:
Screenshot artifact paths:
Known blockers:

No congratulations. No emoji. No production-ready. No GOAL_COMPLETE.
