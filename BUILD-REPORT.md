# Build validation — 2026-10-08

Repository: https://github.com/congha22/Smartphone-MyTube
Base commit: b96fe0fa2e69ecdd5b19380b50e31beb89d954c6.
Environment: Windows x64. Commands ran from this checkout unless noted.

## Tooling and dependency downloads

`dotnet --info` found preinstalled SDKs 8.0.425 and 10.0.400, and runtimes
8.0.31 and 10.0.11. Builds selected SDK 10.0.400 (no global.json).
No SDK, workloads, or system applications were installed. The newer SDK
can restore the reference packs for the mod's net6.0 target.

NuGet restore populated the user package cache with these project dependencies
(some may have already been cached):

- Pathoschild.Stardew.ModBuildConfig 4.4.0
- TextCopy 6.2.1
- Microsoft.Extensions.DependencyInjection.Abstractions 7.0.0
- CefGlue.Common 120.6099.215
- cef.redist.linux64 and cef.redist.osx64 120.1.8
- chromiumembeddedframework.runtime and chromiumembeddedframework.runtime.win-x64 120.1.8
- System.Runtime.CompilerServices.Unsafe 6.0.0
- System.Text.Encodings.Web 6.0.0
- System.Text.Json 6.0.1

## Commands and results

1. `dotnet --info` — success.
2. `git clone https://github.com/congha22/Smartphone-MyTube.git Smartphone-MyTube`
   from the parent workspace — sandbox network failed (127.0.0.1:9);
   repeated with approved network access, succeeded.
3. `dotnet build Smartphone-MyTube.csproj -c Release` and
   `dotnet build BrowserHost/SmartphoneMyTube.BrowserHost.csproj -c Release`
   — sandbox restores failed with NU1301, plus NU1900 vulnerability-data warnings.
4. Repeated both commands with approved network access — restores succeeded.
   BrowserHost failed with CS1061 (AppendSwitchWithValue) and CS0117 (UserDataPath).
   Mod failed in ModBuildConfig validation: game folder not found.
5. After fixes, `dotnet build BrowserHost/SmartphoneMyTube.BrowserHost.csproj -c Release --no-restore`
   — succeeded twice, zero warnings and zero errors.
6. `dotnet build Smartphone-MyTube.csproj -c Release --no-restore -p:EnableModDeploy=false -p:EnableModZip=false`
   — still blocked before C# compilation: game folder not found.
7. `git diff --check` — passed.

## Fixes

- MyTubeCefApp: use the available `AppendSwitch(string, string)` overload.
- Program: replace removed `UserDataPath` with `RootCachePath = dataDir`;
  `CachePath` remains a child of that root. Names were checked by reflection
  against the actual restored CefGlue assembly.
- Mod project: exclude BrowserHost source/resources/items from the root project's
  recursive default items. This prevents compiling helper code and nested generated
  assembly attributes into the net6.0 mod.
- No Smartphone API changes.

## Final status and next step

BrowserHost Release/net8.0 compilation passed. Output:
`BrowserHost/bin/Release/net8.0/SmartphoneMyTube.BrowserHost.dll`.
Compilation does not establish working CEF initialization, video/audio playback,
native packaging, or macOS/Linux runtime support; those were not tested.

Mod Release/net6.0 compilation remains unverified because the real game/SMAPI
reference assemblies are unavailable. Searches covered common Steam paths,
local user/project directories, Program Files directories, and D:, without finding
`Stardew Valley.dll` or `StardewModdingAPI.dll`. No substitute stubs were used.

With a real Stardew Valley installation containing SMAPI, run:

```powershell
dotnet build Smartphone-MyTube.csproj -c Release -p:GamePath="C:\actual\Stardew Valley" -p:EnableModDeploy=false -p:EnableModZip=false
```

Additional mod compile errors may become visible once those references exist.
The build fixes and this report are included together in version control.
