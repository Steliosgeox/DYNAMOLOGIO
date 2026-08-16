# Assumptions Register: ΔΥΝΑΜΟΛΟΓΙΟ (docs/ASSUMPTIONS.md)

| Assumption ID | Domain Area | Assumption Statement | Status | Impact if Changed |
|---|---|---|---|---|
| **ASM-001** | Mathematical Intervals | Effective dates operate on half-open intervals $[StartAt, EndAtExclusive)$ where expiration automatically reverts status to `Present` at $00:00:00$ on return date. | **CONFIRMED** | Core architecture invariant. |
| **ASM-002** | Ranks Hierarchy | Default seed data contains 18 military ranks and 1 civilian employee rank. | **CONFIGURABLE** | Ranks are stored in database and fully editable via catalog settings. |
| **ASM-003** | Organisation Units | Default units seed provides 5 company structures (ΛΔ, 1ος ΛΤ, 2ος ΛΤ, 3ος ΛΤ, ΛΥΠ). | **CONFIGURABLE** | Units and companies are customizable per battalion/installation. |
| **ASM-004** | Absence Types | 9 standard absence types are configured (ΚΑ, ΑΑ, ΤΑ, ΦΑ, ΦΠ, ΝΟΣ, ΑΠΟΣΠ, ΦΥΛ, ΕΚΤΟΣ). | **CONFIGURABLE** | Codes, names, and mutual exclusion groups can be configured in database. |
| **ASM-005** | Air-Gapped Environment | Application runs 100% offline with zero cloud telemetry, external fonts, or online dependencies. | **CONFIRMED** | Security & reliability compliance requirement. |
| **ASM-006** | Official Reporting | Official military Excel output requires a verified template workbook matching SHA-256 hash. If no verified template is registered, official export halts. | **CONFIRMED** | Prevents writing to incorrect/unverified cell locations. |
