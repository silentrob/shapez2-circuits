# Creates a new mod project (project, entry class, manifest, translations) and adds it to the solution.
#   scripts\new-mod.ps1 MyMod             plain code mod
#   scripts\new-mod.ps1 MyMod -Shifter    depends on ShapezShifter (Flow/Hijack/Kit APIs)
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')][string]$Name,
    [switch]$Shifter
)

$root = Split-Path $PSScriptRoot -Parent

# Windows PowerShell 5.1 writes a BOM with -Encoding utf8; the game parses these files, so avoid it.
function Write-Utf8([string]$Path) { process { [IO.File]::WriteAllText($Path, $_, (New-Object Text.UTF8Encoding $false)) } }

$dir = Join-Path $root $Name
if (Test-Path $dir) { Write-Error "$dir already exists"; exit 1 }
New-Item -ItemType Directory $dir | Out-Null

$shifterProp = if ($Shifter) { "`n        <UseShapezShifter>true</UseShapezShifter>" } else { '' }
@"
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <RootNamespace>$Name</RootNamespace>$shifterProp
    </PropertyGroup>
</Project>
"@ | Write-Utf8 (Join-Path $dir "$Name.csproj")

@"
using JetBrains.Annotations;
using ILogger = Core.Logging.ILogger;

namespace $Name
{
    [UsedImplicitly]
    public class ${Name}Mod : IMod
    {
        private readonly ILogger Logger;

        public ${Name}Mod(ILogger logger)
        {
            Logger = logger;
            Logger.Info?.Log("[$Name] Loaded");
        }

        public void Dispose() { }
    }
}
"@ | Write-Utf8 (Join-Path $dir "${Name}Mod.cs")

$deps = if ($Shifter) {
@"
[
    {
      "ModId": "steam:3542611357",
      "ModTitle": "Shapez Shifter",
      "Version": "^1.2.0"
    }
  ]
"@.Trim()
} else { '[]' }

@"
{
  "Version": "0.1.0",
  "Title": "$Name",
  "Description": "",
  "Author": "Rob Ellis",
  "SavedModVersionCompabilityRangeWithSelf": "<0.2.0",
  "GameVersionSupportRange": "*",
  "AffectsSaveGames": $(if ($Shifter) { 'true' } else { 'false' }),
  "DisablesAchievements": $(if ($Shifter) { 'true' } else { 'false' }),
  "Conflicts": [],
  "Assemblies": [
    "$Name.dll"
  ],
  "Dependencies": $deps
}
"@ | Write-Utf8 (Join-Path $dir 'manifest.json')

@"
{
  "en-US": {
  }
}
"@ | Write-Utf8 (Join-Path $dir 'translations.json')

dotnet sln (Join-Path $root 'shapez2-mods.sln') add (Join-Path $dir "$Name.csproj")
Write-Host "Created $dir. Build with: dotnet build $Name"
