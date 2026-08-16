# Acceptance Test Cases: ΔΥΝΑΜΟΛΟΓΙΟ (docs/ACCEPTANCE-TESTS.md)

## Test Scenarios

### ATC-001: Active Leave Automatic Calculation
- **Given**: Officer "Λοχίας Παπαδόπουλος Ιωάννης" with Regular Leave from `16/08/2026 00:00` to `21/08/2026 00:00`.
- **When**: Strength snapshot is calculated for `18/08/2026 10:00`.
- **Then**: Status is `Absent` (Κανονική Άδεια), Return Date is `21/08/2026`, and Absent count increases by 1.

### ATC-002: Automatic Return to Presence on Expiration Date
- **Given**: Officer with leave ending `21/08/2026 00:00`.
- **When**: Strength snapshot is calculated for `21/08/2026 08:00`.
- **Then**: Status is automatically `Present`, Present count increases by 1, and no manual return action is required.

### ATC-003: Future Leave Does Not Affect Today
- **Given**: Today is `16/08/2026`. Leave is recorded for `20/08/2026` to `25/08/2026`.
- **When**: Snapshot is calculated for `16/08/2026`.
- **Then**: Person is `Present` today. When evaluated for `22/08/2026`, person is `Absent`.

### ATC-004: Personnel Lifecycle Boundaries
- **Given**: Soldier enrolled in strength on `01/05/2026` and discharged/transferred out on `01/09/2026 00:00`.
- **When**: Historical snapshot evaluated at `31/08/2026 23:59` vs `01/09/2026 00:01`.
- **Then**: Included in active strength on 31/08, completely excluded from active strength on 01/09.

### ATC-005: Conflict Engine Blocks Overlapping Absences
- **Given**: Person has leave `16/08/2026` to `21/08/2026`.
- **When**: Operator tries to save an overlapping sick leave `18/08/2026` to `23/08/2026`.
- **Then**: Conflict Engine flags an `Error` and blocks persistence.

### ATC-006: Backup Manifest & Verification
- **Given**: Active database with personnel and status records.
- **When**: Backup is generated and subsequent restore is triggered.
- **Then**: SHA-256 manifest is verified, current database is backed up before overwrite, and all records match exactly.
