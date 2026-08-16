# ADR-001: Target Framework Selection (.NET Framework 4.7.2)

## Status
Accepted

## Context
The application is required to operate seamlessly on legacy Windows 7 SP1 (both x86 and x64) as well as modern operating systems (Windows 10 and Windows 11 x64) in air-gapped / offline Greek military and administrative office environments. Modern .NET (e.g. .NET 8/9) has officially discontinued support for Windows 7, requires extra VC++ redistributable runtimes, and lacks native in-box availability on legacy machines.

## Decision
Target **.NET Framework 4.7.2** for all solution projects (`Dynamologio.Core`, `Dynamologio.Infrastructure`, `Dynamologio.ImportExport`, `Dynamologio.Reporting`, `Dynamologio.App`, and `Dynamologio.Tests`).

## Consequences
- Guarantees 100% native compatibility with Windows 7 SP1 (with .NET 4.7.2 pre-installed or chained in offline installer) and out-of-the-box support on Windows 10/11.
- Native WPF desktop UI with hardware rendering-tier fallback for low-spec GPUs.
- All adopted NuGet libraries must strictly support `net472` without requiring modern Windows 10-only WinRT APIs or WebViews.
