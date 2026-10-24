# Compile-checks the mod with Visual Studio's MSBuild and prints a compact error summary.
# RML compiles the .cs files itself in game; this is only to catch errors before launching Raft.
# Usage: powershell -ExecutionPolicy Bypass -File compile.ps1 [-Full] [-Release]
#   -Release compiles without DevTests*.cs, as pack.ps1 -Release ships the mod: no mod code may use the dev commands
param([switch]$Full, [switch]$Release)

$ms = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $ms) { Write-Error "MSBuild not found (install Visual Studio with .NET desktop workload)"; exit 1 }

$proj = Join-Path $PSScriptRoot "DynamicIslands\DynamicIslands.csproj"
if ($Release) {
    # (a copy of the project without the dev commands, next to it so its paths hold; removed after)
    $rel = Join-Path $PSScriptRoot "DynamicIslands\DynamicIslands.release.csproj"
    (Get-Content $proj) | Where-Object { $_ -notmatch 'Compile Include="DevTests' } | Set-Content $rel -Encoding UTF8
    $proj = $rel
}
$log  = Join-Path $PSScriptRoot "DynamicIslands\obj\compile.log"
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null

& $ms $proj /t:Rebuild /p:Configuration=Debug /nologo /v:q "/flp:logfile=$log;verbosity=normal" | Out-Null
$ok = $LASTEXITCODE -eq 0
if ($Release) { Remove-Item $proj -ErrorAction SilentlyContinue }

$errors = Select-String -Path $log -Pattern ': error ' |
    ForEach-Object { ($_.Line.Trim() -replace '\s*\[[^\]]*\]$', '') -replace '^.*\\DynamicIslands\\', '' } |
    Sort-Object -Unique

if ($ok) { Write-Host "BUILD OK" -ForegroundColor Green; exit 0 }

Write-Host "BUILD FAILED: $($errors.Count) errors" -ForegroundColor Red
$errors | ForEach-Object { ($_ -split '\(')[0] } | Group-Object | Sort-Object Count -Descending |
    ForEach-Object { "{0,5}  {1}" -f $_.Count, $_.Name }
if ($Full) { $errors } else { $errors | Select-Object -First 40 }
exit 1
