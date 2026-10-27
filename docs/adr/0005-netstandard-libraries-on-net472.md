# 0005. Logic in netstandard2.0 libraries; only the host is .NET Framework 4.7.2

- Status: superseded by 0006 (everything targets .NET 10 and runs on the Mac)
- Date: 2026-10-26

## Context
The backend must run on .NET Framework 4.7.2 (Windows, IIS/OWIN). But .NET Framework code cannot run on macOS or Linux,
and the Copilot cloud agent runs on Linux: an agent that cannot run the tests cannot verify its own work, so it guesses.

## Decision
- `Homebase.Domain`, `Homebase.Application`, `Homebase.Data` and `Homebase.Devices` target **netstandard2.0** (consumable by net472 today, modern .NET later).
- `Homebase.Api` and `Homebase.Api.Host` target **net472**. They stay thin: map DTOs, call the domain.
- Test projects multi-target `net472;net10.0` where possible; `-f net10.0` runs anywhere.
- SDK-style projects build net472 on any OS via `Microsoft.NETFramework.ReferenceAssemblies`.
- C# stays at 7.3 (the net472 default) so code compiles identically in Visual Studio and in CI.

## Consequences
- Good: ~all business logic is verifiable by agents on Linux; migration to .NET 8+ becomes a host replacement.
- Bad: net472-only API tests need a Windows CI job; some modern C# features are unavailable.
- Enforced by: `backend/Directory.Build.props`, CI `windows` job, `.github/instructions/backend-dotnet.instructions.md`.
