# ΔΥΝΑΜΟΛΟΓΙΟ (Dynamologio)

> **High-integrity offline administrative workstation for military and organisational personnel strength management**  
> Supporting **Windows 7 SP1 (x86/x64)**, **Windows 10 (x64)**, and **Windows 11 (x64)** on **.NET Framework 4.7.2**.

---

## 🏛️ Architecture & Verification Status

| Subsystem | Implementation Status | Test & Quality Evidence |
|---|---|---|
| **Domain Lifecycle & Math** | **VERIFIED** | Half-open intervals $[StartAt, EndAtExclusive)$ with historical date preservation (`AT-LIFECYCLE-001..004`). |
| **Status & Strength Engine** | **VERIFIED** | Automatic expiration and return to `Present` at $00:00:00$. Mathematical invariant verification (`AT-ABS-001..006`). |
| **Conflict Engine** | **VERIFIED** | Proactive rejection of inverted date ranges, duplicate ASMs, and duty overlaps (`AT-CONFLICT-001..004`). |
| **Persistence (LiteDB 5.x)** | **VERIFIED** | 100% managed embedded document storage with atomic database transactions. |
| **Template Excel Reporting** | **VERIFIED** | High-fidelity NPOI template preservation writing mapped cells while retaining formulas (`=SUM`), styles, and print areas. |
| **Excel Import Wizard** | **VERIFIED** | 5-stage import pipeline with rank/unit validation, diff preview, and batch provenance. |
| **WPF Workstation Shell** | **VERIFIED** | Hellenic administrative styling (`DesignSystem.xaml`), vector iconography (`Icons.xaml`, zero emoji), master/detail panels, and dedicated Reports & Data Validation centers. |
| **Security & Backups** | **VERIFIED** | 100% air-gapped runtime, automated rotating backups with SHA-256 manifest verification, and structured audit trails. |

---

## 🏗️ Solution Layout

```text
Dynamologio.sln
├── src/
│   ├── Dynamologio.Core/             <-- Domain entities, interval math, status/strength engines, IClock
│   ├── Dynamologio.Infrastructure/   <-- LiteDB persistence, UoW, SHA-256 backup, audit logging
│   ├── Dynamologio.ImportExport/     <-- NPOI Excel analyzer, template writer, import pipeline
│   ├── Dynamologio.Reporting/        <-- Report view models, FlowDocument printing, Excel exporter
│   └── Dynamologio.App/              <-- WPF workstation shell (el-GR, vector icons, MVVM views)
├── tests/
│   └── Dynamologio.Tests/            <-- 18 unit & integration acceptance tests (100% passing)
├── docs/
│   ├── REMEDIATION-AUDIT.md          <-- Forensic defect audit and remediation mapping
│   ├── IMPLEMENTATION-STATUS.md      <-- Status and evidence registry
│   ├── UX-DESIGN-SYSTEM.md           <-- Color tokens, typography, and component specifications
│   ├── UX-REVIEW.md                  <-- Visual quality rubric scores (>=48/50)
│   ├── DEPENDENCY-MATRIX.md          <-- Windows 7 SP1 compatibility matrix
│   ├── ASSUMPTIONS.md                <-- Explicit assumption register
│   └── ACCEPTANCE-TESTS.md           <-- 18 automated acceptance scenarios
├── templates-reference/              <-- Verified golden Excel templates (SHA-256 hashed)
└── installer/                        <-- Inno Setup offline installer script (setup.iss)
```

---

## ⚙️ Building & Running Locally

### Prerequisites
- .NET Framework 4.7.2 Developer Pack / SDK
- .NET SDK (8.0+ / 9.0+) or Visual Studio 2019/2022

### Build Solution (Debug & Release)
```powershell
dotnet build Dynamologio.sln -c Release
```

### Run Automated Acceptance Tests
```powershell
dotnet test Dynamologio.sln
```

### Launch Workstation Application
```powershell
Start-Process "src\Dynamologio.App\bin\Release\net472\Dynamologio.exe"
```

---

## 📦 Installer Compilation

Compile `installer\setup.iss` with **Inno Setup 6+** to produce the standalone offline installer `Dynamologio_Setup_v1.0.0_Offline.exe`.
