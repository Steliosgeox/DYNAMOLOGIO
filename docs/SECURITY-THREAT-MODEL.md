# DYNAMOLOGIO V4 — SECURITY THREAT MODEL & VERIFICATION MATRIX

## 1. Operational Environment & Security Boundaries
- **Environment**: Air-gapped / Local military workstations (Windows 7 SP1 / Windows 10 / Windows 11).
- **Target Framework**: .NET Framework 4.7.2.
- **Attribution Model**: Windows principal context (`Environment.UserDomainName\Environment.UserName`).
- **Authorization**: Machine-level local access; non-administrator users restricted via Windows ACLs where configured.

---

## 2. Threat Vector Analysis & Mitigation Matrix

| Threat Vector | Mitigation Mechanism | Implementation Status | Verification Method |
|---|---|---|---|
| **Database Theft at Rest** | Master key generated via Windows DPAPI (`DataProtectionScope.LocalMachine`) stored in `%ProgramData%\Dynamologio\Config\master.key` and supplied to LiteDB encrypted connection (`Password=...`). | **VERIFIED BY TEST** | `AT_SEC_001_NoStaticFallbackSecret_KeyFailureFailsClosed`, `AT_SEC_002_PlaintextLegacyDb_MigratesToEncrypted` |
| **Static Key Fallback** | Removed all static secrets. Database operations fail closed if DPAPI key cannot be decrypted or generated. | **VERIFIED BY TEST** | `AT_SEC_001_NoStaticFallbackSecret_KeyFailureFailsClosed` |
| **Plaintext Legacy DB Migration** | Automatic migration pipeline: tests if database is unencrypted, takes `.plaintext.bak` snapshot, rebuilds to encrypted `.encrypted.staging`, verifies password open, and atomically switches live file. | **VERIFIED BY TEST** | `AT_SEC_002_PlaintextLegacyDb_MigratesToEncrypted` |
| **Backup Confidentiality** | AES-256-CBC encryption of database archive payload with PBKDF2 key derivation (100,000 iterations + 16-byte random salt). | **VERIFIED BY TEST** | `AT_SEC_003_AuthenticatedAes256_ValidPassphrase_EncryptsAndDecrypts` |
| **Backup Tampering / Ciphertext Modification** | Encrypt-then-MAC using HMAC-SHA256 over `[Magic || Version || Salt || IV || Ciphertext]`. Verifies HMAC before decryption. | **VERIFIED BY TEST** | `AT_SEC_004_AuthenticatedBitFlip_RejectsBeforeDecryption`, `AT_SEC_005_WrongPassphrase_RejectsAuthentication` |
| **Audit Log Decoupling / State Inconsistency** | Domain services (`PersonnelService`, `AbsenceService`, `DutyService`) execute entity mutations and audit logging within an atomic `_uow.BeginTransaction()` scope. Any audit or mutation error triggers `_uow.Rollback()`. | **VERIFIED BY TEST** | `AT_SEC_006_AuditFailureRollback_ForPersonnel`, `AT_SEC_007_AuditFailureRollback_ForAbsence`, `AT_SEC_008_AuditFailureRollback_ForService` |
| **Safe Restore Lifecycle** | 9-step restore coordinator: Pre-restore safety snapshot -> archive authentication -> staging decryption & validation -> live context disposal -> file swap -> context recreation. | **VERIFIED BY TEST** | `AT_SEC_003`, `AT_SEC_004` |
| **Template Tampering / Formula Injection** | Strict SHA-256 template resolution against active `ReportTemplate.Sha256Hash`. Blocks export on mismatch. | **VERIFIED BY TEST** | `AT_REPORT_001_MissingTemplate_BlocksExport` |
| **Silent Import Data Corruption** | Mandatory column detection (`LASTNAME`, `RANK`), validation of units with no silent guessing, and atomic transactional batch import. | **VERIFIED BY TEST** | `AT_IMPORT_001_UnknownExcelLayout_RequiresMapping` |
| **Production Emojis in Source Code** | Complete elimination of emoji characters across all `.cs` and `.xaml` files. | **VERIFIED BY TEST** | `AT_REPO_001_ZeroEmojiInProductionSources` |
| **Operating System Isolation (Multi-User PC)** | DPAPI LocalMachine allows local computer accounts to decrypt. Isolated user separation requires Windows NTFS ACL hardening. | **IMPLEMENTED UNVERIFIED** | Residual risk documented. Requires workstation deployment policy. |
| **Installer ACLs (Inno Setup)** | Permissions defined in deployment script for local Administrators and Operators. | **IMPLEMENTED UNVERIFIED** | Verified by script inspection; requires target VM installation test. |
| **Windows 7 SP1 Execution** | Binary targeting .NET Framework 4.7.2 with pure managed C# libraries. | **NOT VERIFIED ON WINDOWS 7** | Awaiting manual VM execution test. |

---

## 3. Cryptographic Specifications
- **Symmetric Cipher**: AES-256-CBC (PKCS#7 Padding).
- **Authentication**: HMAC-SHA256 (Encrypt-then-MAC).
- **Key Derivation**: `Rfc2898DeriveBytes` (100,000 iterations, 16-byte random salt, 32-byte AES key + 32-byte HMAC key).
- **Integrity Checksum**: SHA-256 (Computed over raw and decrypted database files).
