# SAMYR TECH WAY — macOS / VS Code / Claude Code Quick Start

> **The solution is already scaffolded and implemented.** Skip the scaffold and `migrations add` steps below: the `InitialCreate` migration already exists. Follow [README.md](../README.md) instead.

## Prerequisites

1. Install Visual Studio Code, Claude Code extension / CLI, Git, .NET 10 SDK (Apple Silicon macOS build), and optionally GitHub CLI.
2. Open the macOS Terminal and verify:

```bash
dotnet --version
git --version
code --version
```

## Scaffold the project locally

```bash
mkdir -p ~/Projects/samyr-tech-way-spinwin
cd ~/Projects/samyr-tech-way-spinwin
dotnet new sln -n SamyrTechWay.SpinWin
dotnet new mvc -n SamyrTechWay.SpinWin -o src/SamyrTechWay.SpinWin -f net10.0
dotnet sln add src/SamyrTechWay.SpinWin/SamyrTechWay.SpinWin.csproj
dotnet new xunit -n SamyrTechWay.SpinWin.Tests -o tests/SamyrTechWay.SpinWin.Tests -f net10.0
dotnet sln add tests/SamyrTechWay.SpinWin.Tests/SamyrTechWay.SpinWin.Tests.csproj
dotnet add tests/SamyrTechWay.SpinWin.Tests/SamyrTechWay.SpinWin.Tests.csproj reference src/SamyrTechWay.SpinWin/SamyrTechWay.SpinWin.csproj
```

Extract this package zip's contents into the solution root; it includes `CLAUDE.md`, docs and the original logo in its target folder. **Do not replace the MVC source files when merging**; the ZIP includes only a single asset in `src`.

```bash
code .
dotnet build
```

Open the Claude Code panel in VS Code (or execute `claude` from a terminal in the root). Send:

> Read `CLAUDE.md` and the COMPLETE `docs/MASTER_BUILD_PROMPT.md`. Implement SAMYR TECH WAY Spin & Win in this existing ASP.NET Core MVC solution end-to-end, including backend, MSSQL/EF Core migrations, secure admin + customer login, transactional spin engine and Try Again, prize editor/credits/admin panel, premium brand UI with GSAP + sounds, Share Your Win PNG, and tests. Follow the phases and build/test before stopping. Do not just propose a design. Update `docs/IMPLEMENTATION_PLAN.md` as you work.

## Database on macOS

Your Apple Silicon Mac does not ship with SQL Server LocalDB. Easiest for this project: create your free MonsterASP.NET MSSQL database early, enable temporary **Remote access**, then use the **Remote** connection string locally. After deploying, your app on Monster should use the **Local** (internal) connection string via a hosting environment variable. Keep both out of Git.

When the Claude implementation has configured the connection string key and EF Core CLI tool, typical commands (confirm paths and names with Claude) are:

```bash
dotnet user-secrets init --project src/SamyrTechWay.SpinWin
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' '<REMOTE_SQL_CONNECTION_STRING_FROM_MONSTER>' --project src/SamyrTechWay.SpinWin
dotnet ef migrations add InitialCreate --project src/SamyrTechWay.SpinWin --startup-project src/SamyrTechWay.SpinWin
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project src/SamyrTechWay.SpinWin --startup-project src/SamyrTechWay.SpinWin
dotnet run --project src/SamyrTechWay.SpinWin
```

`<...>` above is a placeholder; never paste secrets into a screenshot or Git commit. Only add a migration if the project doesn't already include one; Claude should create it.

## Final verification

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet publish src/SamyrTechWay.SpinWin/SamyrTechWay.SpinWin.csproj -c Release -o ./publish
```

Use `docs/MONSTERASP_DEPLOY.md` to go live.
