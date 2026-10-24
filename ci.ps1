# Runs this mod's console commands (CITest, CIEditor, ...) in a running Raft and prints only this run's results, so a
# test loop costs a few lines instead of reading Player.log. Needs a dev build of the mod (pack.ps1 -Install, without
# -Release): DevTests.Init logs "[CITEST] Command file: <path>" and starts PollCommandFile, which every second reads
# Mods\DynamicIslands\dev_commands.txt, deletes it and runs each line as a console command; the commands log
# "[CITEST] ..." lines ("PASS: ...", "FAIL: ...") to Raft's Player.log.
# Usage: powershell -ExecutionPolicy Bypass -File ci.ps1 -Command "<cmd>[;<cmd>...]" [-Until <regex>] [-Timeout 300]
#            [-Quiet 20] [-Full] [-RaftDir <path>] [-LogPath <path>] [-Player 2 -LogPath <the sandboxed Raft's Player.log>]
#   The lines of one run all start in the same frame, side by side: a command that needs an earlier one finished goes in
#   its own run (CIEditor, then CIDemo).
#   The run ends at a [CITEST] line after the last command's "> " echo that matches -Until (e.g. '^Editor ready:' for
#   CIEditor), without -Until at "[CITEST] IDLE (all tests done)" (every test coroutine has ended), else after -Quiet seconds without a new [CITEST] line (with -Until: only once a FAIL line came). A single
#   command that is unknown or throws ends it at once. Raft exiting ends it too.
#   A command file the mod does not read within 60 s (-Timeout while the mod has not logged its command file yet) is
#   deleted again, so a later Raft start does not run it.
#   Exit: 0 no FAIL, 1 FAIL, 2 Raft, the log, a dev build, the command file or this run's lines not found, 3 timeout.
param(
    [string[]]$Command,
    [ValidateRange(1, 2)][int]$Player = 1,
    [int]$Timeout = 300,
    [int]$Quiet = 20,
    [string]$Until,
    [switch]$Full,
    [string]$RaftDir = "D:\Games\SteamLibrary\steamapps\common\Raft",
    [string]$LogPath = (Join-Path $env:USERPROFILE "AppData\LocalLow\Redbeet Interactive\Raft\Player.log")
)

$clock = [Diagnostics.Stopwatch]::StartNew()
$utf8 = New-Object Text.UTF8Encoding($false)
function Stop-Run([string]$msg) { Write-Host $msg -ForegroundColor Red; exit 2 }
function Test-Raft { [bool](Get-Process -Name Raft -ErrorAction SilentlyContinue) }
# (Raft keeps Player.log open for writing; $null while there is none, as when Raft starts again and renames it to
# Player-prev.log)
function Open-Log { try { New-Object IO.FileStream($LogPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete') } catch { $null } }
function Format-Line([string]$s) { if (-not $Full -and $s.Length -gt 300) { $s.Substring(0, 300) + " ..." } else { $s } }

# (-File passes "-Command a,b" as one string: ";" separates commands; blank and "#" lines are skipped by the mod too)
$cmds = @($Command | ForEach-Object { $_ -split ';' } | ForEach-Object { "$_".Trim() } | Where-Object { $_.Length -gt 0 -and -not $_.StartsWith('#') })
if ($cmds.Count -eq 0) { Stop-Run 'No command: -Command "CITest" (several: "CIEditor;CITest")' }
if ($Player -eq 2 -and -not $PSBoundParameters.ContainsKey('LogPath')) { Stop-Run "-Player 2 needs -LogPath <the sandboxed Raft's Player.log>" }
if (-not (Test-Raft)) { Stop-Run "Raft is not running" }
if (-not (Test-Path -LiteralPath $LogPath)) { Stop-Run "No Raft log at $LogPath" }

# What the log says: the command file the mod polls (DevTests.Init), and whether the loaded mod polls one at all
# (a release build logs "Mod Custom Islands has been loaded" without it; an unloaded mod stops polling)
$name = "dev_commands.txt"
if ($Player -eq 2) { $name = "dev_commands_2.txt" }
$fs = Open-Log
if (-not $fs) { Stop-Run "Cannot open $LogPath" }
try { $all = (New-Object IO.StreamReader($fs, $utf8)).ReadToEnd() } finally { $fs.Dispose() }
$cf = [regex]::Matches($all, '\[CITEST\] Command file: (.+?dev_commands(?:_2)?\.txt)')
$lastCf = -1
if ($cf.Count -gt 0) { $lastCf = $cf[$cf.Count - 1].Index }
$lastLoad = $all.LastIndexOf('[CUSTOM ISLANDS] Mod Custom Islands has been loaded', [StringComparison]::Ordinal)
$lastUnload = $all.LastIndexOf('[CUSTOM ISLANDS] Mod Custom Islands has been unloaded', [StringComparison]::Ordinal)
$all = $null
$polling = $lastCf -gt $lastUnload
if (-not $polling -and $lastLoad -gt $lastUnload) { Stop-Run "The mod is loaded but logged no '[CITEST] Command file:' line: not a dev build? (pack.ps1 -Install, without -Release)" }
$logFile = $null
if ($polling) { $logFile = $cf[$cf.Count - 1].Groups[1].Value }
if ($logFile -and (Split-Path $logFile -Leaf) -ne $name) { Stop-Run "$LogPath is the other player's log (its Raft polls $logFile)" }
# (the mod's own path unless -RaftDir is given; the log line is missing while the mod is still starting)
if ($logFile -and -not $PSBoundParameters.ContainsKey('RaftDir')) { $file = $logFile } else { $file = Join-Path $RaftDir "Mods\DynamicIslands\$name" }
if (-not (Test-Path -LiteralPath (Split-Path $file))) { Stop-Run "No folder $(Split-Path $file) (mod not loaded yet? -RaftDir is Raft's folder)" }

# Only the log written after this point is read
$fs = Open-Log
if (-not $fs) { Stop-Run "Cannot open $LogPath" }
$offset = $fs.Length; $fs.Dispose()
# (UTF-8 without BOM: the mod reads the file with File.ReadAllLines)
$text = ($cmds -join "`r`n") + "`r`n"
if (Test-Path -LiteralPath $file) { $text = "`r`n" + $text }   # (a file not read yet: its last line kept apart)
for ($try = 1; ; $try++) {
    # (the mod may have the file open for reading at this moment)
    try { [IO.File]::AppendAllText($file, $text, $utf8); break }
    catch { if ($try -ge 20) { Stop-Run ("Cannot write {0}: {1}" -f $file, $_.Exception.Message) }; Start-Sleep -Milliseconds 100 }
}

$readBy = $Timeout
if ($polling) { $readBy = [Math]::Min($Timeout, 60) }
while (Test-Path -LiteralPath $file) {
    if (-not (Test-Raft)) {
        Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
        Stop-Run "Raft exited before reading $file (file removed)"
    }
    if ($clock.Elapsed.TotalSeconds -ge $readBy) {
        Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
        $why = "mod not loaded, or not a dev build: pack.ps1 -Install without -Release?"
        if ($polling) { $why = "Raft busy or hung?" }
        if ($Player -eq 2) { $why += " Player 2: does the Sandboxie box open this file to the real folder (OpenFilePath)?" }
        Write-Host "TIMEOUT: Raft did not read $file in $readBy s, file removed ($why)" -ForegroundColor Red
        exit 3
    }
    Start-Sleep -Milliseconds 250
}

$fails = New-Object Collections.Generic.List[string]
$excs  = New-Object Collections.Generic.List[string]
$other = New-Object Collections.Generic.List[string]
$pass = 0
$last = $cmds[$cmds.Count - 1]
$echo = "> " + $last   # (PollCommandFile logs each line as "[CITEST] > <line>" before running it)
$need = @($cmds | Where-Object { $_ -ceq $last }).Count; $echoes = 0   # (the same command earlier in the run echoes too)
# (a command RunCommand can't find, or one that throws when started, logs one of these at once)
$ends = @(); if (-not $Until) { $ends = @("IDLE (all tests done)") }   # (DevTests.StartTest logs it when the last test coroutine ends; a load command ends before the world is in, so -Until wins)
if ($cmds.Count -eq 1) { $ends += @(("FAIL: no console command called " + ($last -split ' ')[0]), ("FAIL: command '" + $last + "': ")) }
$seen = $false; $done = $false; $timedOut = $false; $exited = $false; $stopAt = -1
$pending = $null; $pendingN = 0
$lastNew = $clock.Elapsed.TotalSeconds
$decoder = $utf8.GetDecoder(); $buf = New-Object byte[] 65536; $chars = New-Object char[] ($utf8.GetMaxCharCount(65536)); $partial = ""

while ($true) {
    $fs = Open-Log
    if ($fs) {
        try {
            if ($fs.Length -lt $offset) { $offset = 0; $partial = ""; $decoder.Reset() }   # (a new Player.log: Raft started again)
            $fs.Position = $offset
            while (($n = $fs.Read($buf, 0, $buf.Length)) -gt 0) {
                $offset += $n
                $partial += [string]::new($chars, 0, $decoder.GetChars($buf, 0, $n, $chars, 0))
            }
        } finally { $fs.Dispose() }
    }
    $lines = $partial -split "`n"
    $partial = $lines[$lines.Count - 1]
    for ($k = 0; $k -lt $lines.Count - 1; $k++) {
        $line = $lines[$k].TrimEnd("`r")
        $i = $line.IndexOf("[CITEST] ")
        if ($i -ge 0) {
            $t = $line.Substring($i + 9)
            $lastNew = $clock.Elapsed.TotalSeconds
            if ($t.StartsWith("FAIL:")) { $fails.Add($t) } else { $other.Add($t) }
            if ($t.StartsWith("PASS:")) { $pass++ }
            if ($t -ceq $echo) { if (++$echoes -ge $need) { $seen = $true } }
            elseif ($seen -and (($Until -and ($t -match $Until)) -or @($ends | Where-Object { $t.StartsWith($_) }).Count -gt 0)) { $done = $true }
            continue
        }
        # Unity exceptions: the first line, kept when it or a stack line under it mentions DynamicIslands
        if ($pending) {
            if ($line.Contains("DynamicIslands")) { $excs.Add("EXC " + $pending + " | " + $line.Trim()); $pending = $null }
            elseif ($line.StartsWith("(Filename:") -or ++$pendingN -gt 40) { $pending = $null }
        }
        if ($line -match '^[\w.]*Exception\b') {
            if ($line.Contains("DynamicIslands")) { $excs.Add("EXC " + $line) } else { $pending = $line; $pendingN = 0 }
        }
    }
    $now = $clock.Elapsed.TotalSeconds
    if ($stopAt -ge 0) { if ($now -ge $stopAt) { break } }
    elseif ($done) { $stopAt = $now + 1 }   # (one more read: lines of the same frame)
    elseif (-not (Test-Raft)) { $exited = $true; $stopAt = $now + 1 }
    elseif ($now - $lastNew -ge $Quiet -and (-not $Until -or $fails.Count -gt 0)) { break }
    elseif ($now -ge $Timeout) { $timedOut = $true; break }
    Start-Sleep -Milliseconds 500
}

$secs = [int]$clock.Elapsed.TotalSeconds
if (-not $seen) { Stop-Run "No '[CITEST] $echo' line in $LogPath after $secs s: not this Raft's log?" }
$fails | ForEach-Object { Format-Line $_ }
$rest = @($excs) + @($other)
if (-not $Full -and $rest.Count -gt 40) {
    "({0} lines left out, -Full shows all)" -f ($rest.Count - 40)
    $e = [Math]::Min($excs.Count, 40)
    $rest = @($excs | Select-Object -First $e) + @($other | Select-Object -Last (40 - $e))
}
$rest | ForEach-Object { Format-Line $_ }
if ($exited) { Write-Host "Raft exited" -ForegroundColor Yellow }
$summary = "PASS {0}, FAIL {1}, {2} s" -f $pass, $fails.Count, $secs
if ($timedOut) {
    Write-Host ("TIMEOUT after {0} s: {1}" -f $Timeout, $(if ($Until) { "no [CITEST] line matched -Until" } else { "[CITEST] lines kept coming (raise -Timeout)" })) -ForegroundColor Red
    Write-Host $summary -ForegroundColor Red; exit 3
}
if ($fails.Count -gt 0) { Write-Host $summary -ForegroundColor Red; exit 1 }
Write-Host $summary -ForegroundColor Green
exit 0
