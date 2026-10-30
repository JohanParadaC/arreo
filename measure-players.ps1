# Measures a game with several players on this computer, using the Network Test Player
# (Arreo > Prototype > Build Network Test Player) and its bots (ProtoNetBot).
# Usage: .\measure-players.ps1                      (host + 4 clients, 20 cows, 60 s)
#        .\measure-players.ps1 -Herd 50 -Players 5 -Seconds 60
# The host and the first client render in 1280x720 windows; the other clients run headless so
# the two measured windows keep the GPU. Every copy waits for all players, measures, writes a
# report (frame times, where the frame goes, memory, bytes received) to -Out and quits.
# The host's upload is what the clients receive: add up their receivedBytesPerSecond.
param(
    [int]$Herd = 20,
    [int]$Players = 5,
    [int]$Seconds = 60,
    [string]$Out = (Join-Path $PSScriptRoot 'Logs\measure')
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'Build\NetworkTest\Arreo.exe'
if (-not (Test-Path $exe)) {
    throw "No Network Test Player at '$exe'. Build it first: Arreo > Prototype > Build Network Test Player."
}
New-Item -ItemType Directory -Force $Out | Out-Null
Get-ChildItem $Out -Filter *.txt | Remove-Item
$common = "-arreoMeasure $Seconds -arreoWaitPlayers $Players"
$window = '-screen-fullscreen 0 -screen-width 1280 -screen-height 720'

# The host saves into $Out, not over your real saved game.
$procs = @(Start-Process $exe -PassThru -ArgumentList "-arreoNet host -arreoHerd $Herd $common $window -arreoSaveDir `"$Out\saves`" -arreoMeasureOut `"$Out\host.txt`" -logFile `"$Out\host.log`"")
Start-Sleep -Seconds 6
for ($n = 1; $n -lt $Players; $n++) {
    $render = if ($n -eq 1) { $window } else { '-batchmode -nographics' }
    $procs += Start-Process $exe -PassThru -ArgumentList "$render -arreoNet client $common -arreoMeasureOut `"$Out\client$n.txt`" -logFile `"$Out\client$n.log`""
    Start-Sleep -Seconds 2
}

$deadline = (Get-Date).AddSeconds($Seconds + 150)
foreach ($p in $procs) {
    $left = [int]($deadline - (Get-Date)).TotalMilliseconds
    if ($left -lt 1 -or -not $p.WaitForExit($left)) {
        if (-not $p.HasExited) { $p.Kill() }
    }
}

$upload = 0
foreach ($report in Get-ChildItem $Out -Filter *.txt | Sort-Object Name) {
    Write-Host "===== $($report.BaseName)" -ForegroundColor Cyan
    $text = Get-Content $report.FullName
    $text | Where-Object { $_ -match '^(role|players|fps|time per|memory|bandwidth|console)' } | ForEach-Object { Write-Host $_ }
    if ($report.BaseName -like 'client*') {
        $line = $text | Where-Object { $_ -match 'receivedBytesPerSecond=(\d+)' } | Select-Object -First 1
        if ($line -match 'receivedBytesPerSecond=(\d+)') { $upload += [int]$Matches[1] }
    }
}
Write-Host ("Host upload (what the clients receive, without UDP headers): {0:N1} KB/s" -f ($upload / 1024)) -ForegroundColor Green
