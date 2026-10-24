# Packs the DynamicIslands\ folder into DynamicIslands.rmod (an .rmod is a plain zip; RML compiles the .cs files in game).
# Replaces build.bat, which only works when the repo folder is literally named "DynamicIslands".
# Usage: powershell -ExecutionPolicy Bypass -File pack.ps1 [-Install] [-Release] [-RaftDir <path>]
#   -Release leaves out DevTests*.cs (the CI* dev/test console commands) and DevTests*.island (their sample island)
param(
    [switch]$Install,
    [switch]$Release,
    [string]$RaftDir = "D:\Games\SteamLibrary\steamapps\common\Raft"
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$src = Join-Path $PSScriptRoot "DynamicIslands"
$out = Join-Path $PSScriptRoot "DynamicIslands.rmod"

$excludeDirs  = @("bin", "obj", ".vs", "Properties")
# (stray files never ship: crash dumps of Git Bash and other tools, logs, editor backups - a bash.exe.stackdump was in 3.0)
$excludeFiles = @("*.csproj", "*.csproj.user", "*.rmod", "*.meta", "*.old", "*.veryold", "*,veryold", "*.pdb",
    "*.stackdump", "*.log", "*.tmp", "*.bak", "*.orig", "*.rej", "*~", "*.swp", "Thumbs.db", "desktop.ini", ".DS_Store")
if ($Release) { $excludeFiles += "DevTests*.cs", "DevTests*.island" }

# Only files git tracks go in (a file someone or something left in the folder doesn't); without git, every file the
# patterns allow. Skipped untracked files are named.
$tracked = $null
try {
    $list = & git -C $PSScriptRoot -c core.quotepath=off ls-files -- DynamicIslands 2>$null
    if ($LASTEXITCODE -eq 0 -and $list) {
        $tracked = @{}
        foreach ($p in $list) { if ($p) { $tracked[$p.Trim('"').Substring("DynamicIslands/".Length).Replace('/', '\').ToLowerInvariant()] = $true } }
    }
} catch { $tracked = $null }
if ($null -eq $tracked) { Write-Warning "git isn't available: packing every file the patterns allow (check the list for stray files)" }

$skipped = New-Object System.Collections.Generic.List[string]
$files = @(Get-ChildItem $src -Recurse -File -Force | Where-Object {
    $rel = $_.FullName.Substring($src.Length + 1)
    $top = $rel.Split('\')[0]
    if ($excludeDirs -contains $top) { return $false }
    foreach ($pattern in $excludeFiles) { if ($_.Name -like $pattern) { return $false } }
    if ($null -ne $tracked -and -not $tracked.ContainsKey($rel.ToLowerInvariant())) { $skipped.Add($rel); return $false }
    return $true
})
if ($skipped.Count -gt 0) { Write-Warning ("Not packed (not in git): " + ($skipped -join ", ")) }

if (Test-Path $out) { Remove-Item $out }
$zip = [System.IO.Compression.ZipFile]::Open($out, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($f in $files) {
        $entry = $f.FullName.Substring($src.Length + 1).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal)
    }
    # The guide as a PDF, for the Guide buttons in the game (HelpLinks.OpenGuide writes it out of the .rmod to open it)
    $pdf = Join-Path $PSScriptRoot "docs\Custom-Islands-Guide.pdf"
    if (Test-Path $pdf) { [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $pdf, "Custom-Islands-Guide.pdf", [System.IO.Compression.CompressionLevel]::Optimal) }
    else { Write-Warning "docs\Custom-Islands-Guide.pdf is missing: the Guide buttons will open the guide online" }
} finally { $zip.Dispose() }

$version = (Get-Content (Join-Path $src "modinfo.json") -Raw | ConvertFrom-Json).version
Write-Host ("Packed {0} files into {1} ({2:N0} KB), version {3}{4}" -f $files.Count, $out, ((Get-Item $out).Length / 1KB), $version, $(if ($Release) { ", release (no dev commands)" } else { "" })) -ForegroundColor Green

# The build's stamp beside the .rmod, never in it (IslandNetwork.Build hashes every file of the .rmod): the commit and worktree
# it was packed from and the .rmod's md5 - _ProjectDocs\tools\buildcheck.ps1 says which commit is installed
$stamp = Join-Path $PSScriptRoot "DynamicIslands.build.txt"
$commit = "$(& git -C $PSScriptRoot rev-parse HEAD 2>$null)".Trim()
$dirty = @(& git -C $PSScriptRoot status --porcelain -uno -- DynamicIslands 2>$null | Where-Object { $_ }).Count
Set-Content -LiteralPath $stamp -Encoding ASCII -Value @("commit $(if ($commit) { $commit } else { 'unknown (no git)' })",
    "uncommitted $dirty", "from $PSScriptRoot", "packed $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))", "release $([bool]$Release)",
    "md5 $((Get-FileHash -LiteralPath $out -Algorithm MD5).Hash)")

if ($Install) {
    $mods = Join-Path $RaftDir "mods"
    if (-not (Test-Path $mods)) { throw "Raft mods folder not found: $mods (install RML and start Raft once)" }
    Copy-Item $out $mods -Force
    Copy-Item $stamp $mods -Force
    Write-Host "Installed to $mods\DynamicIslands.rmod" -ForegroundColor Green
}
