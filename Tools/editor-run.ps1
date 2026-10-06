# Drives the OPEN Unity editor from outside: focuses it so it recompiles, waits
# for the command queue to answer, then runs the given commands in order.
# Each command waits for its .done/.err file. Screenshot files land in Temp/.
#
# Usage: .\Tools\editor-run.ps1 build-scene play "wait:25" screenshot "wait:4" stop
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Commands)

$root = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $root "Temp\vaanam-commands"
New-Item -ItemType Directory -Force $dir | Out-Null

$sig = '[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd); [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);'
Add-Type -MemberDefinition $sig -Name WinFocus -Namespace VaanamTools -ErrorAction SilentlyContinue
$unity = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "Vaanam*" } | Select-Object -First 1
if (-not $unity) { Write-Error "No Unity editor window titled 'Vaanam*' is open."; exit 1 }
[VaanamTools.WinFocus]::ShowWindow($unity.MainWindowHandle, 9) | Out-Null
[VaanamTools.WinFocus]::SetForegroundWindow($unity.MainWindowHandle) | Out-Null

$n = 10
function Run-Command([string]$cmd, [int]$timeoutSec) {
    $script:n++
    $base = Join-Path $dir ("{0:D2}" -f $script:n)
    Set-Content -Path "$base.cmd" -Value $cmd -Encoding ascii
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path "$base.done") { $r = Get-Content "$base.done" -Raw; Write-Host ("[{0}] {1}" -f $cmd, $r.Trim()); return $true }
        if (Test-Path "$base.err") { $r = Get-Content "$base.err" -Raw; Write-Host ("[{0}] ERROR {1}" -f $cmd, $r.Trim()); return $false }
        Start-Sleep -Milliseconds 500
    }
    Write-Host "[$cmd] TIMEOUT after $timeoutSec s"
    return $false
}

# If sources are newer than the compiled assemblies, ask the editor to refresh
# (the command queue still runs on the old assembly, which is fine for that),
# then wait for the new DLLs to appear and the domain to reload.
# Each assembly is compared only with its own sources: Unity does not rebuild the
# editor DLL when only runtime scripts changed, and vice versa.
$allSources = Get-ChildItem (Join-Path $root "Assets\_Vaanam") -Recurse -Include *.cs, *.asmdef
$newestRuntime = ($allSources | Where-Object { $_.FullName -notmatch '\\Editor\\' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
$newestEditor = ($allSources | Where-Object { $_.FullName -match '\\Editor\\' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
$pairs = @(
    @{ Dll = (Join-Path $root "Library\ScriptAssemblies\Vaanam.dll"); Newest = $newestRuntime },
    @{ Dll = (Join-Path $root "Library\ScriptAssemblies\Vaanam.Editor.dll"); Newest = $newestEditor }
)
function Is-Stale {
    foreach ($p in $pairs) {
        if (-not (Test-Path $p.Dll)) { return $true }
        # The editor DLL is rebuilt whenever the runtime DLL is, so it must be at least as new as its own sources.
        if ((Get-Item $p.Dll).LastWriteTime -lt $p.Newest) { return $true }
    }
    return $false
}
if (Is-Stale) {
    if (-not (Run-Command "refresh" 180)) { exit 1 }
    $deadline = (Get-Date).AddSeconds(240)
    while ((Get-Date) -lt $deadline -and (Is-Stale)) { Start-Sleep -Seconds 2 }
    if (Is-Stale) { Write-Host "WARNING: assemblies still older than sources after refresh; check the Unity console for compile errors"; exit 1 }
    Start-Sleep -Seconds 4   # let the domain reload settle
}
if (-not (Run-Command "ping" 180)) { exit 1 }

foreach ($c in $Commands) {
    if ($c -like "wait:*") { $s = [int]($c.Substring(5)); Start-Sleep -Seconds $s; continue }
    $timeout = if ($c -eq "build-player") { 1500 } else { 300 }
    if (-not (Run-Command $c $timeout)) { if ($c -ne "stop") { Run-Command "stop" 60 | Out-Null }; exit 1 }
}
