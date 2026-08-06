# AGENTS.md

## Start Here

Before doing anything, read `docs/ai-handoff.md` (start here), `docs/roadmap.md`
(what's done / next), and `docs/architecture.md` (dependency rules). This tells you
where work left off without scanning the whole repo. Check `git status --short` too:
work may be intentionally uncommitted, so preserve unrelated user changes.

Also update `docs/ai-handoff.md` and `docs/roadmap.md` when you move a milestone so
the next agent can resume instantly.

## Build & Test

Run from the repository root with the .NET 9 SDK:

### Build
```powershell
dotnet build ProjectSeshat.sln
```

### Test
```powershell
dotnet test ProjectSeshat.sln
```

### Run
```powershell
dotnet run --project src/ProjectSeshat.App
```

## EF Core migrations

Schema changes go through EF Core migrations (not `EnsureCreated`). To add one:

```powershell
dotnet ef migrations add <Name> --project src/ProjectSeshat.Data
```

## Lint & Typecheck

### Lint
```powershell
# Add your linter command here
```

### Typecheck
```powershell
# Add your typechecker command here
```

## Notes

1. Ensure you have .NET SDK installed (`dotnet --version` should be 9.x).
2. Tests require running the build first.
3. Use `--configuration Release` for production builds.
4. Package versions are managed centrally in `Directory.Packages.props`; add versions there, not in `.csproj`.
