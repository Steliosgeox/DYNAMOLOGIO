# ADR-003: Excel Engine Selection (NPOI)

## Status
Accepted

## Context
The application must generate and parse both legacy binary workbooks (`.xls` - Excel 97-2003) and modern XML workbooks (`.xlsx` - Excel 2007+) without requiring Microsoft Office or Excel to be installed on client workstations. COM Interop (`Microsoft.Office.Interop.Excel`) is strictly forbidden due to reliability, licensing, performance, and offline air-gap constraints.

## Decision
Adopt **NPOI** (pure managed port of Apache POI) supporting both HSSF (`.xls`) and XSSF (`.xlsx`) formats.

## Consequences
- Zero dependency on Microsoft Office or local COM automation.
- Deep access to formula engines, cell styling, print setups, page scaling, row/column dimensions, headers/footers, and merged cell geometries.
- Full compatibility with .NET Framework 4.7.2.
