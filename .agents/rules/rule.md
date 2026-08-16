---
trigger: model_decision
description: Responsibilities
---

from pathlib import Path

prompt = r"""# GEMINI MASTER BUILD PROMPT
## Project: ΔΥΝΑΜΟΛΟΓΙΟ
### Production-grade offline personnel strength management for legacy Windows environments

You are acting as a combined:

- Principal Software Architect
- Senior C# / WPF Engineer
- Legacy Windows Compatibility Engineer
- Database Architect
- Desktop UX Lead
- Excel/PDF Reverse-Engineering Engineer
- QA Lead
- Security Engineer
- Release Engineer

You are not building a demo, prototype, mockup, student project, or generic CRUD application.

You are building a production-grade Greek administrative desktop application called:

# ΔΥΝΑΜΟΛΟΓΙΟ

The deployed application must operate completely offline and must support legacy Greek military-office computers, including Windows 7 SP1, while also running correctly on Windows 10 and Windows 11.

The application exists to replace repeated manual Excel work with a reliable local personnel-strength management system.

The user will provide the official or currently-used Excel workbook(s) and representative PDFs after this prompt.

Those files are the primary source of truth for reporting structure and field semantics.

Do not invent military administrative procedures that cannot be established from:

1. the supplied files,
2. explicit user requirements,
3. configurable administrative settings.

If an administrative rule is unknown, make it configurable or mark it as an unresolved assumption.

---

# 0. NON-NEGOTIABLE MISSION

The daily workflow must become approximately:

PERSONNEL DATABASE
→ RECORD A CHANGE ONCE
→ STATUS CALCULATED AUTOMATICALLY
→ CURRENT STRENGTH CALCULATED AUTOMATICALLY
→ HISTORICAL STRENGTH RECONSTRUCTABLE
→ OFFICIAL REPORT GENERATED
→ PRINT / EXCEL / PDF

The operator should not manually recalculate totals or manually restore someone to "present" after an absence expires.

Core question answered by the system:

> Ποια είναι η δύναμη τώρα, ποιοι είναι παρόντες, ποιοι απουσιάζουν, για ποιο λόγο, μέχρι πότε, τι υπηρεσία έχουν και τι πρέπει να εμφανιστεί στο επίσημο Δυναμολόγιο;

The software must treat personnel state as time-dependent data, not merely a mutable Present/Absent boolean.

---

# 1. HARD PLATFORM REQUIREMENTS

Target platform:

- Windows 7 SP1 x86
- Windows 7 SP1 x64
- Windows 10 x64
- Windows 11 x64

Primary framework:

- C#
- WPF
- .NET Framework 4.7.2
- C# language features compatible with that toolchain

Do not switch to modern .NET unless you can prove Windows 7 compatibility with the exact deployment model.

Forbidden core runtime dependencies:

- Electron
- Chromium
- WebView2
- Node.js
- React
- Vue
- Angular
- MAUI
- Blazor Hybrid
- local HTTP server
- Docker
- mandatory Internet access
- cloud APIs
- cloud database
- online authentication
- browser rendering engine
- Office Interop as a required reporting dependency

The deployed executable must remain fully usable with the network disconnected.

Do not silently introduce a dependency that breaks Windows 7.

Before adopting any NuGet package, verify:

- exact supported target framework,
- x86/x64 behavior,
- native dependencies,
- Visual C++ runtime requirements,
- Windows API requirements,
- license suitability,
- offline deployment behavior.

Maintain a dependency compatibility table.

---

# 2. ARCHITECTURAL PRINCIPLE

Use a layered architecture.

Recommended solution:

```text
Dynamologio.sln

src/
  Dynamologio.App/
  Dynamologio.Core/
  Dynamologio.Infrastructure/
  Dynamologio.ImportExport/
  Dynamologio.Reporting/

tests/
  Dynamologio.Tests/