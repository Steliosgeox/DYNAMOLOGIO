# Acceptance Test Suite: ΔΥΝΑΜΟΛΟΓΙΟ (docs/ACCEPTANCE-TESTS.md)

## Test Execution Summary
- **Total Executed**: 18
- **Passed**: 18
- **Failed**: 0
- **Skipped**: 0
- **Duration**: ~20s

---

## Acceptance Test Scenarios & Results

### 1. Lifecycle & Historical Snapshots
- **AT-LIFECYCLE-001**: Active personnel within `[StrengthStartDate, StrengthEndDate)` are classified as `IsInActiveStrength = true` and `EffectiveStatus = Present`. **[PASSED]**
- **AT-LIFECYCLE-002**: Query date prior to `StrengthStartDate` evaluates to `IsInActiveStrength = false` and `EffectiveStatus = ExcludedFromStrength`. **[PASSED]**
- **AT-LIFECYCLE-003**: Query date on or after `StrengthEndDate` evaluates to `IsInActiveStrength = false` and `EffectiveStatus = ExcludedFromStrength`. **[PASSED]**
- **AT-LIFECYCLE-004**: Historical query prior to departure date preserves active status even if the person is subsequently archived (`IsArchived = true`). **[PASSED]**

### 2. Status Derivation & Interval Mathematics
- **AT-ABS-001**: Status interval math strictly respects half-open intervals $[StartAt, EndAtExclusive)$ across boundary timestamps. **[PASSED]**
- **AT-ABS-002**: Automatic return to `Present` upon expiration occurs at exactly $00:00:00$ on the return date. **[PASSED]**
- **AT-ABS-003**: 1-day absence $[16/08, 17/08)$ correctly evaluates to active absence on $16/08$ and returns to `Present` on $17/08$. **[PASSED]**
- **AT-ABS-004**: Cancelled status events (`IsCancelled = true`) are ignored by the status engine. **[PASSED]**
- **AT-ABS-005**: Returning today count accurately aggregates all scheduled leaves ending on the query date. **[PASSED]**
- **AT-ABS-006**: Civilian personnel category does not silently fall through into conscripts count. **[PASSED]**

### 3. Conflict Detection Engine
- **AT-CONFLICT-001**: Inverted date range ($Return \le Start$) returns `INVALID_DATE_RANGE` error without silent mutation. **[PASSED]**
- **AT-CONFLICT-002**: Duplicate military service number (ΑΣΜ) is detected and blocked with `DUPLICATE_ASM` error. **[PASSED]**
- **AT-CONFLICT-003**: Service duty assignment outside active strength interval returns `SERVICE_OUTSIDE_STRENGTH` error. **[PASSED]**
- **AT-CONFLICT-004**: Overlapping service assignments for the same person return `OVERLAPPING_SERVICE` error. **[PASSED]**

### 4. Audit, Reporting & Backup
- **AT-AUDIT-001**: Audit service logs structured records containing actor, entity, action, and JSON payloads. **[PASSED]**
- **AT-DIAG-001**: Technical diagnostic package service exports encrypted/compressed ZIP diagnostic bundle. **[PASSED]**
- **AT-REPORT-001**: Printable document generator creates compliant `FlowDocument` with unit title and strength tables. **[PASSED]**
- **AT-BACKUP-001**: Backup service creates SHA-256 verified ZIP archives and restores safely with pre-restore backup. **[PASSED]**
