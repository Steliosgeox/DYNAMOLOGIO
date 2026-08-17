# ΔΥΝΑΜΟΛΟΓΙΟ — V5 EVIDENCE MATRIX

This matrix maps every single architectural requirement and security/domain claim to its exact production code path, test proof, and verified status.

---

## 1. Security & Cryptography

| Claim / Requirement | Production Code Path | Exact Automated Test | Status | Evidence / Notes |
| :--- | :--- | :--- | :--- | :--- |
| **No Static Secrets in Codebase** | `src/Dynamologio.Infrastructure/Security/IKeyProtectionProvider.cs`<br>`src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs` | `AT_SEC_009_NoStaticFallbackSecretsInCodebase` | **VERIFIED** | All hardcoded strings (`DynamologioFallbackLocalKey2026#`, `DynamologioMachineBoundBackupKey2026`) completely removed. Backup machine key derived dynamically via `HMACSHA256(masterKey, "DynamologioMachineBackupDerivedKeyV5")`. |
| **Key Provider DPAPI Failure Fails Closed** | `src/Dynamologio.Infrastructure/Security/IKeyProtectionProvider.cs`<br>`src/Dynamologio.Infrastructure/LiteDb/LiteDbContext.cs` | `AT_SEC_001_InjectedKeyProviderFailure_FailsClosed` | **VERIFIED** | `IKeyProtectionProvider` abstraction injected. Injecting `FailingKeyProvider` throws `CryptographicException` and prevents database opening. |
| **Plaintext-to-Encrypted Migration Zero Residue** | `src/Dynamologio.Infrastructure/LiteDb/LiteDbContext.cs` (`MigrateLegacyPlaintextDbIfNeeded`) | `AT_SEC_002_PlaintextLegacyDb_MigratesToEncrypted_LeavesNoPlaintextResidue` | **VERIFIED** | Migration verifies staging DB with master password, verifies collection document counts match, zero-overwrites temporary files with `RNGCryptoServiceProvider`, and deletes all plaintext backups. |
| **Authenticated Backup Envelope (`DYNBK3`)** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs` (`BackupService`) | `AT_SEC_003_AuthenticatedAes256_ValidPassphrase_EncryptsAndDecrypts`<br>`AT_SEC_004_AuthenticatedBitFlip_RejectsBeforeDecryption`<br>`AT_SEC_005_WrongPassphrase_RejectsAuthentication` | **VERIFIED** | Format: `[DYNBK3: 6B][Version: 2B][Salt: 16B][IV: 16B][HMAC-SHA256: 32B][Ciphertext(manifest.json + DB)]`. Modifying 1 byte in payload or metadata rejects before decryption. |
| **Production Encrypted LiteDB Restore** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs` (`DatabaseLifecycleCoordinator`) | `AT_SEC_006_ProductionEncryptedLiteDb_BackupAndRestore_Succeeds` | **VERIFIED** | Validates decrypted staging database using `new LiteDatabase($"Filename={stagingPath};Password={masterPassword};Connection=direct")`. |
| **Database Lifecycle Coordinator Reopens Context** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs` (`DatabaseLifecycleCoordinator`) | `AT_SEC_007_LifecycleCoordinator_RestoresAndRecreatesContext` | **VERIFIED** | Performs 9-step atomic restore: pre-restore safety copy, validation with master password, context closure, file swap, context reopen, and executing UI reload callback `onContextRecreated()`. |
| **Structured Backup Health Info** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs` (`BackupService.GetBackupHealth`) | `AT_SEC_008_AutoBackupHealth_PersistsStructuredTimestamps` | **VERIFIED** | Structured JSON persisted to `Config/backup_health.json` with `LastAttemptUtc`, `LastSuccessUtc`, `LastFailureCode`, `LastFailureMessageSafe`, `StatusSummary`. |

---

## 2. Transactional Integrity & Import

| Claim / Requirement | Production Code Path | Exact Automated Test | Status | Evidence / Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Audit Failure Rollback (Personnel)** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs` (`PersonnelService`) | `AT_TX_001_AuditFailureRollback_ForPersonnel` | **VERIFIED** | Wrapped in `using (var tx = _uow.BeginTransaction())`. Injecting audit failure aborts transaction; 0 records inserted. |
| **Audit Failure Rollback (Absence)** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs` (`AbsenceService`) | `AT_TX_002_AuditFailureRollback_ForAbsence` | **VERIFIED** | Wrapped in `using (var tx = _uow.BeginTransaction())`. Injected audit failure leaves 0 `StatusEvents`. |
| **Audit Failure Rollback (Duty)** | `src/Dynamologio.Infrastructure/Services/InfrastructureServices.cs` (`DutyService`) | `AT_TX_003_AuditFailureRollback_ForService` | **VERIFIED** | Wrapped in `using (var tx = _uow.BeginTransaction())`. Injected audit failure leaves 0 `ServiceAssignments`. |
| **Unknown Excel Layout Blocks Import** | `src/Dynamologio.ImportExport/Excel/Import/ExcelImportPipeline.cs` | `AT_IMPORT_001_UnknownExcelLayout_RequiresMapping` | **VERIFIED** | `ExcelImportService.AnalyzeAndPreviewImport()` checks mandatory column requirements and marks rows `ErrorInvalid` with `CanCommit = false`. |
| **Duplicate Name Ambiguity Requires Resolution** | `src/Dynamologio.ImportExport/Excel/Import/ExcelImportPipeline.cs` | `AT_IMPORT_002_DuplicateNameAmbiguity_RequiresAsmResolution` | **VERIFIED** | If duplicate records exist matching `LastName + FirstName` without ASM, importer rejects row with explicit ambiguity error and prevents commit. |

---

## 3. Reporting & Template Verification

| Claim / Requirement | Production Code Path | Exact Automated Test | Status | Evidence / Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Strict Template Verification Contract** | `src/Dynamologio.Reporting/Services/ReportGeneratorService.cs` (`CheckTemplateStatus`) | `AT_REPORT_001_NoTemplateOrBlankSha_ReturnsUnverified` | **VERIFIED** | Returns `Verified` **only if** active `ReportTemplate` exists, `Sha256Hash` is non-empty, file exists, actual file hash matches expected hash, and `CellMappingJson` parses validly. Returns `Unverified` if template or expected hash is missing. |
| **Distinct Report Generation (All 4 Types)** | `src/Dynamologio.ImportExport/Excel/NpoiTemplateWriter.cs`<br>`src/Dynamologio.Reporting/Services/ReportGeneratorService.cs` | `AT_REPORT_002_DistinctReportGeneration_AllFourTypes` | **VERIFIED** | 4 distinct generators implemented in `NpoiTemplateWriter`: `GenerateDynamologioWorkbook`, `GenerateAbsentWorkbook`, `GeneratePresentWorkbook`, `GenerateServiceWorkbook`. 4 distinct `FlowDocument` visual models rendered. |

---

## 4. UI & Full Application Window Evidence

| Claim / Requirement | Production Code Path | Exact Automated Test | Status | Evidence / Notes |
| :--- | :--- | :--- | :--- | :--- |
| **5-Dimensional Person Search** | `src/Dynamologio.App/Controls/SearchablePersonPicker.xaml`<br>`src/Dynamologio.App/Controls/SearchablePersonPicker.xaml.cs` | `AT_UI_001_SearchablePersonPicker_SearchesAllFiveDimensions` | **VERIFIED** | Searches across Rank, FullName, Unit, ASM, and Specialty. Item template displays Rank (Bold Accent), FullName, Unit, and ASM. |
| **Full MainWindow STA Visual Capture (1366x768 & 1024x768)** | `src/Dynamologio.App/Views/MainWindow.xaml`<br>`src/Dynamologio.App/Styles/DesignSystem.xaml` | `AT_UI_002_FullMainWindow_STA_Rendering_1366x768_and_1024x768` | **VERIFIED** | Renders complete **`MainWindow`** (220px nav rail, headers, status bar, and active view content) with realistic military demo data (32 soldiers, 4 ranks, 3 companies, active absences, duties) and saves 20 high-fidelity PNGs to `docs/screenshots/v5/`. |
