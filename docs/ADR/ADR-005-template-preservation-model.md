# ADR-005: Template Preservation Model for Official Reports

## Status
Accepted

## Context
Official administrative and military Excel workbooks contain intricate layouts, specific fonts, border styles, cell shading, headers/footers, formulas, page breaks, print setups, and margins required by administrative regulations. Generating workbooks from scratch frequently introduces subtle visual discrepancies.

## Decision
Implement a **Template Preservation Engine** (`IExcelTemplateWriter`):
1. An approved golden template is registered with a unique ID, metadata, and cryptographic SHA-256 hash.
2. When generating a report, the engine creates a byte-for-byte in-memory copy of the template.
3. The engine populates only explicitly mapped data cells and table ranges.
4. All unmapped static titles, logos, signatures, formulas, print regions, and page setup rules remain untouched.
5. If a template's hash is modified without updating the mapping profile, the system halts export and prompts the operator for verification.

## Consequences
- 100% visual fidelity with official military/administrative expectations.
- Complete separation between data calculation in `Dynamologio.Core` and document layout in `Dynamologio.ImportExport` / `Dynamologio.Reporting`.
