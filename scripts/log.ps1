# Shows mod-loader lines from the game's Player.log.
#   scripts\log.ps1            mod-related lines from the last session
#   scripts\log.ps1 -All       the whole log
#   scripts\log.ps1 -Follow    keep watching while the game runs
param([switch]$All, [switch]$Follow)

$persistent = if ($env:SPZ2_PERSISTENT) { $env:SPZ2_PERSISTENT } else { "$env:USERPROFILE\AppData\LocalLow\tobspr Games\shapez 2" }
$log = Join-Path $persistent 'Player.log'
if (-not (Test-Path $log)) { Write-Error "No Player.log at $log"; exit 1 }

# The loader prefixes each mod's log lines with "Name[version]"; also keep load and failure lines.
$pattern = '^\S+\[\d+\.\d+\.\d+[^\]]*\] |^Loading .*\.dll|^Skipping loading|IMod|exception|error'
$noise = 'GameAnalytics|^ERROR: Shader '
if ($Follow) {
    Get-Content $log -Wait -Tail 0 | Where-Object { $All -or ($_ -match $pattern -and $_ -notmatch $noise) }
} elseif ($All) {
    Get-Content $log
} else {
    Select-String -Path $log -Pattern $pattern | ForEach-Object { $_.Line } | Where-Object { $_ -notmatch $noise }
}
