# ADR-002: Embedded Local Persistence (LiteDB 5.x)

## Status
Accepted

## Context
The application must run strictly standalone and offline without requiring administrative setup of external database servers (like SQL Server, PostgreSQL, or Oracle) or complex multi-architecture native C++ wrappers (such as SQLite with native DLL packaging issues across x86/x64).

## Decision
Adopt **LiteDB 5.x** as the primary embedded document database for V1 local storage, wrapped behind generic repository and Unit of Work abstractions.

## Consequences
- 100% managed C# code with zero external native DLL dependencies for database engine.
- BSON document storage allowing flexible document modeling, indexed fields, and simple atomic single-file storage (`dynamologio.db`).
- Standard repository abstraction (`IPersonnelRepository`, `IStatusEventRepository`, etc.) ensures the domain and application layers remain completely unaware of LiteDB, making potential future migration to multi-user client/server databases trivial.
