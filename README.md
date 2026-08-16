# ΔΥΝΑΜΟΛΟΓΙΟ (Dynamologio)

> **Production-grade offline personnel strength management for legacy and modern Windows environments**  
> Supporting **Windows 7 SP1 (x86/x64)**, **Windows 10 (x64)**, and **Windows 11 (x64)** on **.NET Framework 4.7.2**.

---

## 🏛️ Overview

**ΔΥΝΑΜΟΛΟΓΙΟ** is an air-gapped, offline administrative desktop application built with C# and WPF to manage military and administrative unit strength, dated presence/absence events, and operational duty rosters without manual Excel recalculations or fragile background services.

### Core Capabilities
- **Time-Dependent State Calculation**: Evaluates personnel presence at query time across half-open intervals $[StartAt, EndAtExclusive)$.
- **Automatic Return to Presence**: When an absence period expires, personnel automatically revert to `ΠΑΡΩΝ` on the exact return date.
- **Mathematical Invariant Verification**: $\text{Active Strength} = \text{Present} + \text{Absent}$.
- **Conflict Engine**: Real-time deterministic validation blocking overlapping leaves, inverted date ranges, and duplicate identities (ΑΣΜ).
- **Template-Preserving Official Excel Reporting**: Populates approved golden workbooks while retaining all original formulas (`=SUM(...)`), borders, styles, and page setups via NPOI.
- **Multi-Stage Excel Import Wizard**: Analyzes workbooks, matches columns/ranks, presents interactive change preview, and records `ImportBatch` provenance.
- **Embedded Persistence**: 100% managed local LiteDB 5.x storage (`%PROGRAMDATA%\Dynamologio\Data\dynamologio.db`).
- **Security & Backup**: Automated daily rotating backups with SHA-256 integrity verification, full audit trail, and technical diagnostic package export.

---

## 🏗️ Solution Structure

```text
Dynamologio.sln
├── src/
│   ├── Dynamologio.Core/             <-- Domain entities, interval math, status/strength engines
│   ├── Dynamologio.Infrastructure/   <-- LiteDB persistence, repositories, backups, audit logging
│   ├── Dynamologio.ImportExport/     <-- NPOI Excel analyzer, template writer, import pipeline
│   ├── Dynamologio.Reporting/        <-- Report view models, FlowDocument printing, Excel exporter
│   └── Dynamologio.App/              <-- WPF MVVM workstation shell (el-GR, virtualized DataGrids)
├── tests/
│   └── Dynamologio.Tests/            <-- Unit & integration test suite (13 passing tests)
├── docs/
│   ├── ADR/                          <-- Architecture Decision Records (ADR-001 to ADR-005)
│   ├── ASSUMPTIONS.md                <-- Explicit assumption register
│   ├── REQUIREMENTS.md               <-- Requirements traceability matrix
│   ├── DOMAIN.md                     <-- Mathematical & domain specifications
│   └── ACCEPTANCE-TESTS.md           <-- Acceptance criteria & scenarios
├── templates-reference/              <-- Verified golden Excel templates (SHA-256 hashed)
└── installer/                        <-- Inno Setup offline installer script (setup.iss)
```

---

## ⚙️ Building & Running Locally

### Prerequisites
- .NET Framework 4.7.2 Developer Pack / SDK
- .NET SDK (8.0+ or 9.0+) or Visual Studio 2019/2022

### Build Solution
```powershell
dotnet build Dynamologio.sln
```

### Run Automated Tests
```powershell
dotnet test Dynamologio.sln
```

### Run Desktop Application
```powershell
Start-Process "src\Dynamologio.App\bin\Debug\net472\Dynamologio.exe"
```

---

## 📦 Installer

Compile `installer\setup.iss` with **Inno Setup 6+** to produce the standalone offline installer `Dynamologio_Setup_v1.0.0_Offline.exe`.
