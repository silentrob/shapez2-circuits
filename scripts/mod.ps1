# Turns local mods on and off. The game loads every folder in its "mods" folder and has no switch of
# its own, so a disabled mod is moved to a sibling "mods-disabled" folder. Workshop mods are turned
# off by unsubscribing in Steam.
#   scripts\mod.ps1                 list local mods and whether each is on
#   scripts\mod.ps1 -Off Circuits   move it out of the game's mods folder
#   scripts\mod.ps1 -On Circuits    move it back
# Building a mod puts it back in "mods", so turn it off again after building if needed.
param([string]$On, [string]$Off)

$persistent = if ($env:SPZ2_PERSISTENT) { $env:SPZ2_PERSISTENT } else { "$env:USERPROFILE\AppData\LocalLow\tobspr Games\shapez 2" }
$enabled = Join-Path $persistent 'mods'
$disabled = Join-Path $persistent 'mods-disabled'

function Move-Mod([string]$Name, [string]$From, [string]$To) {
    $source = Join-Path $From $Name
    $target = Join-Path $To $Name
    if (-not (Test-Path $source)) { Write-Error "No mod '$Name' in $From"; exit 1 }
    if (Test-Path $target) { Write-Error "'$Name' already exists in $To; remove one copy first"; exit 1 }
    New-Item -ItemType Directory -Force $To | Out-Null
    Move-Item $source $target
}

if ($On) { Move-Mod $On $disabled $enabled; Write-Host "$On is on" }
elseif ($Off) { Move-Mod $Off $enabled $disabled; Write-Host "$Off is off" }
else {
    foreach ($dir in @(@{ Path = $enabled; State = 'on' }, @{ Path = $disabled; State = 'off' })) {
        if (Test-Path $dir.Path) {
            Get-ChildItem $dir.Path -Directory | ForEach-Object { "{0,-4} {1}" -f $dir.State, $_.Name }
        }
    }
}
