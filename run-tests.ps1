# Runs the Unity tests locally in batchmode, the same EditMode suite CI runs.
# Usage: .\run-tests.ps1                 (EditMode)
#        .\run-tests.ps1 -Platform PlayMode
# Close the Unity Editor first: batchmode can't open a project that is already open.
param(
    [ValidateSet('EditMode', 'PlayMode')]
    [string]$Platform = 'EditMode',
    [string]$UnityPath
)

$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot

if (-not $UnityPath) {
    $versionLine = Select-String -Path (Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion:\s*(\S+)'
    $version = $versionLine.Matches[0].Groups[1].Value
    $UnityPath = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $UnityPath)) {
    throw "Unity not found at '$UnityPath'. Pass -UnityPath."
}

$platformName = $Platform.ToLower()
$results = Join-Path $projectPath "results-$platformName.xml"
$log = Join-Path $projectPath "Logs\tests-$platformName.log"
if (Test-Path $results) { Remove-Item $results }

Write-Host "Running $Platform tests with $UnityPath ..."
$unityArgs = @(
    '-batchmode', '-nographics',
    '-projectPath', "`"$projectPath`"",
    '-runTests', '-testPlatform', $Platform,
    '-testResults', "`"$results`"",
    '-logFile', "`"$log`""
)
$process = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow

if (-not (Test-Path $results)) {
    Write-Host "No results file. Check the log: $log" -ForegroundColor Red
    exit 1
}

[xml]$xml = Get-Content $results
$run = $xml.'test-run'
foreach ($case in $xml.SelectNodes('//test-case[@result="Failed"]')) {
    Write-Host "FAILED: $($case.fullname)" -ForegroundColor Red
    Write-Host "  $($case.failure.message.InnerText)"
}
$color = if ($run.result -eq 'Passed') { 'Green' } else { 'Red' }
Write-Host "$Platform`: $($run.passed)/$($run.total) passed, $($run.failed) failed, $($run.skipped) skipped" -ForegroundColor $color
exit $process.ExitCode
