# Security Threat Model: ΔΥΝΑΜΟΛΟΓΙΟ (docs/SECURITY-THREAT-MODEL.md)

## 1. System Scope & Environment
- **Platform**: Air-gapped workstation running Windows 7 SP1 (x86/x64), Windows 10 (x64), or Windows 11 (x64) on .NET Framework 4.7.2.
- **Data Classified**: Military / organisational personnel strength records, full names, military service numbers (ΑΣΜ), ranks, leaves, and operational duty assignments.
- **Physical Boundary**: Administrative office workstation. Zero external cloud connectivity, zero telemetry.

---

## 2. Threat Analysis & Mitigations

### Threat 1: Stolen Workstation Hard Drive / Cold Boot Storage Extraction (Data at Rest)
- **Risk**: An unauthorized party extracts the physical HDD/SSD or copies the raw `%PROGRAMDATA%\Dynamologio\Data\dynamologio.db` file.
- **Mitigation**:
  1. Operating system-level full disk encryption (BitLocker) is the primary line of defense.
  2. Application-level database key encryption via **Windows DPAPI** (`ProtectedData.Protect(key, optionalEntropy, DataProtectionScope.LocalMachine)`). LiteDB initializes with this machine-bound master key: `Filename=...;Password={dpapiMasterKey}`.
  3. No plaintext passwords or cryptographic keys are ever hard-coded in source code or committed to repository.

### Threat 2: Unauthorized Local User on Shared Machine (Privilege Escalation)
- **Risk**: A standard non-administrative Windows user on the same machine accesses the database directory directly.
- **Mitigation**:
  1. Inno Setup installer applies strict NTFS ACLs restricting `%PROGRAMDATA%\Dynamologio` to `Administrators` and the designated `DynamologioUsers` local group.
  2. Database file access is scoped via DPAPI.

### Threat 3: Stolen / Intercepted Backup Archive (Data in Transit)
- **Risk**: A backup ZIP file exported to a USB flash drive is lost or stolen.
- **Mitigation**:
  1. Backups intended for external transport are encrypted using **AES-256-CBC** with HMAC-SHA256 authenticated header or password-derived key via **PBKDF2** (`Rfc2898DeriveBytes` with 100,000 iterations and cryptographic salt).
  2. Raw plaintext `.db` files are never written to unprotected removable media.

### Threat 4: Backup Tampering & Corrupt State Restore (Integrity Violation)
- **Risk**: An attacker or corrupted USB drive alters the backup ZIP content, causing the application to restore corrupted or malicious data.
- **Mitigation**:
  1. The backup manifest computes an exact **SHA-256 integrity checksum** of the database payload.
  2. Before restoring, the application extracts to a secure temporary sandbox and verifies the SHA-256 checksum against the manifest.
  3. Prior to overwriting the active database, the application takes an automatic **pre-restore safety backup** (`.bak`). If restore verification or database startup fails, the previous active database is restored automatically.

### Threat 5: Audit Log Repudiation & Modification
- **Risk**: An operator performs an unauthorized mutation and either deletes the audit record or the audit write fails while the business mutation succeeds.
- **Mitigation**:
  1. All domain mutations and their corresponding `AuditEvent` participate in the same atomic database transaction. If audit recording fails, the business mutation is rolled back.
  2. Audit records capture the authenticated **Windows Identity** (`Environment.UserName`), machine name, and UTC timestamp, preventing anonymous `"OPERATOR"` claims.

### Threat 6: Accidental Disclosure via Public Git Repository
- **Risk**: Real military personnel names, official documents, or unencrypted test backups are accidentally committed to the repository.
- **Mitigation**:
  1. Defensive `.gitignore` rules prevent staging `*.db`, `*.bak`, `*.log`, `*.xlsx`, `*.xls`, `*.pdf`, `*.zip`, `Backups/`, `Diagnostics/`, `Imports/`, and `Exports/`.
  2. All automated tests utilize synthesized, sanitized in-memory fixtures.

---

## 3. Cryptographic Primitives (.NET 4.7.2 & Windows 7 SP1 Compliant)
- **Key Storage**: Windows DPAPI (`System.Security.Cryptography.ProtectedData`).
- **Integrity Checksums**: SHA-256 (`System.Security.Cryptography.SHA256Managed` / `SHA256.Create()`).
- **Archive Encryption**: AES-256 with PBKDF2 key derivation (`Rfc2898DeriveBytes`).
