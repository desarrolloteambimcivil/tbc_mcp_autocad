# tbc-acad-mcp

Open-source (Apache-2.0) MCP gateway that lets Claude Code (and any MCP-capable client) drive Autodesk AutoCAD and Civil 3D 2022–2027. Fork of [bimwright/dwg-mcp](https://github.com/bimwright/dwg-mcp) maintained by Nour Rouas (`desarrolloteambimcivil/tbc_mcp_autocad`); see `NOTICE`. Sister product of [tbc-revit-mcp](https://github.com/desarrolloteambimcivil/tbc_mcp_revit); together they form the TBC-MCP suite.

Product name, author, repository, data folder, env-var prefix, pipe prefix and `_meta` key prefix live in `src/shared/Infrastructure/ProductIdentity.cs`. Use it instead of hardcoding names or paths.

## Architecture

```
MCP client → stdio → tbc-acad-mcp.exe (.NET 8, src/server) → TCP (2022–2024) or named pipe (2025–2027), NDJSON + token → plugin shell per AutoCAD year → AutoCAD .NET API
```

- **Gateway** (`src/server`, project `Bimwright.Dwg.Server.csproj`, AssemblyName `tbc-acad-mcp`): MCP server over stdio, no AutoCAD reference. Compiles a list of `src/shared` files explicitly (`<Compile Include>`); add new shared files it needs there.
- **Plugin** (`src/plugin-acadNN`, AssemblyName `TbcAcadMcp.Plugin.AcadNN`): `IExtensionApplication`; all shells compile `src/shared/**` and link `src/plugin-acad24/App.cs`. Work runs on AutoCAD's main thread via `Application.Idle`, then `Document.LockDocument()`.
- Discovery: `%LOCALAPPDATA%\NourRouas\tbc-acad-mcp\acad-<year>-<pid>.json` (+ `acad-<year>.json` alias), schema 3. Pipe name `TbcAcadMcp-<year>-<pid>`.
- Per-user data under `%LOCALAPPDATA%\NourRouas\tbc-acad-mcp\`: `dwgmcp.config.json` (gateway), `settings.json` (plugin), `captures\`, `outputs\`, `Fonts\`, `baked\`, `tbc-acad-mcp.log`.
- Bundle: `%APPDATA%\Autodesk\ApplicationPlugins\TbcAcadMcp.bundle` with `Platform="AutoCAD*"` so Civil 3D loads it.

## Build & test

```bash
# Gateway and one shell (only the .NET 8 SDK is needed for 2022–2026; 2027 needs .NET 10).
dotnet build src/server/Bimwright.Dwg.Server.csproj -c Debug
dotnet build src/plugin-acad25/Bimwright.Dwg.Plugin.Acad25.csproj -c Debug
dotnet test tests/Bimwright.Dwg.Tests          # main suite
dotnet test tests/Bimwright.Dwg.Rewriting.Tests
# tests/Bimwright.Dwg.Toast.Tests targets .NET 10 (CI only on machines with .NET 8 alone).
# tests/installer/*.ps1 are Pester 5 tests (CI).
```

Builds do not deploy. AutoCAD locks a loaded plugin DLL: close AutoCAD before replacing a deployed bundle. Shells compile against Autodesk's `AutoCAD.NET` NuGet reference packages (compile-time only); never commit or ship Autodesk DLLs.

## Conventions

- Tool names keep the `dwg_` prefix; wire command names are unprefixed. New toolset classes must be registered in `Program.ToolRegistration.cs`.
- Return DTOs only; never serialize AutoCAD objects. Entity references are AutoCAD hex handles.
- Writes require a verified target (`dwg_switch_target(target="pid:<n>", verify=true)`); changing drawings needs `rebind=true` plus `expected_document_id`.
- Never touch an upstream dwg-mcp install (`%LOCALAPPDATA%\Bimwright\dwg-mcp`, `Bimwright.Dwg.bundle`, `bimwright-dwg*`/`dwg-mcp` client entries): it is a separate product.
- Commit messages: Conventional Commits in English.

## Rebrand map

Changed: product/MCP name `tbc-acad-mcp`, gateway exe, plugin assembly names, bundle and setup ZIP names, data root, env vars `TBC_ACAD_MCP_*` (with `BIMWRIGHT_DWG_*` fallback), pipe prefix, `_meta` keys, Unicode style `TBC_Unicode`, layer `layer-tbc-acad-mcp`, UI wordmark and About.

Kept on purpose (smaller upstream merges): namespaces `Bimwright.Dwg.*`, csproj/sln/test project file names, `dwg_*` tool names, AutoCAD commands (`MCPSTART`, `MCPDISABLECODE`…), discovery file names, `dwgmcp.config.json`, thread and mutex names.
