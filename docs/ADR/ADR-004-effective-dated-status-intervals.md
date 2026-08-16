# ADR-004: Effective-Dated Status Intervals and Query-Time Derivation

## Status
Accepted

## Context
Traditional CRUD systems use a mutable `bool IsPresent` or current status field in the personnel table. This causes severe failure modes:
1. Past daily reports cannot be accurately reconstructed.
2. Background OS services or scheduled tasks would be needed to flip presence when an absence expires, which fails completely if a machine is turned off over weekends or holidays.
3. Overlapping or conflicting absence dates are difficult to detect.

## Decision
Model all administrative presence/absence as immutable dated `StatusEvent` records using explicit half-open time intervals:
$$[StartAt, EndAtExclusive)$$
A person's current status and unit strength at any chosen timestamp $T$ are strictly calculated at query time:
- A person is active if $StrengthStartDate \le T < (StrengthEndDate \text{ or } \infty)$ and $IsArchived == \text{false}$.
- A person is absent if an active non-present `StatusEvent` covers $T$.
- Otherwise, the person is present by default.

## Consequences
- 100% deterministic and historical reconstruction for any past or future date without background timers.
- When an absence period expires, the status automatically reverts to Present on the exact return date without user intervention.
