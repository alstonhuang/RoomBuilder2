<#
.SYNOPSIS
  Launches Unity with temp allocator leak callstacks enabled (-diag-temp-memory-leak-validation).

.DESCRIPTION
  Unity's warning:
    TLS Allocator ALLOC_TEMP_TLS ... has unfreed allocations ...
  suggests running the Editor with -diag-temp-memory-leak-validation to print callstacks.

  This script helps you:
  - launch the correct Unity.exe (Unity Hub install), and
  - write logs to a project-local file for easy sharing.

.PARAMETER UnityExe
  Full path to Unity.exe. If omitted, the script tries to locate it under Unity Hub installs.

.PARAMETER UnityVersion
  Unity version folder name under Unity Hub (e.g., 6000.0.62f1). Used only when UnityExe is omitted.

.PARAMETER ProjectPath
  Unity project root. Defaults to this script's parent folder.

.PARAMETER LogFile
  Output log file path. Defaults to <ProjectPath>\Temp\Editor_diag_temp_leak.log

.PARAMETER BatchMode
  Run Unity in batchmode (no UI). Useful with -RunEditModeTests.

.PARAMETER RunEditModeTests
  Runs EditMode tests via Unity CLI (-runTests -testPlatform editmode) and quits.

.PARAMETER TestResults
  Test result XML output path when -RunEditModeTests is used.

.EXAMPLE
  .\Tools\RunUnityWithTempLeakValidation.ps1

.EXAMPLE
  .\Tools\RunUnityWithTempLeakValidation.ps1 -RunEditModeTests -BatchMode
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$UnityExe,
    [string]$UnityVersion = "6000.0.62f1",
    [string]$ProjectPath,
    [string]$LogFile,
    [switch]$BatchMode,
    [switch]$RunEditModeTests,
    [string]$TestResults
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Quote-Arg([string]$value) {
    if ($null -eq $value) { return "" }
    if ($value -match '[\s"]') { return '"' + ($value -replace '"', '\"') + '"' }
    return $value
}

function Find-UnityExe([string]$version) {
    $hubRoot = "C:\Program Files\Unity\Hub\Editor"
    if (-not (Test-Path $hubRoot)) { return $null }

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($version)) {
        $candidates += Join-Path $hubRoot (Join-Path $version "Editor\Unity.exe")
    }

    $candidates += Get-ChildItem -Path $hubRoot -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName "Editor\Unity.exe" }

    foreach ($c in ($candidates | Select-Object -Unique)) {
        if (Test-Path $c) { return $c }
    }
    return $null
}

if ([string]::IsNullOrWhiteSpace($LogFile)) {
    # set after ProjectPath default is resolved
}

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ProjectPath = (Resolve-Path (Join-Path $scriptDir "..")).Path
}
$ProjectPath = (Resolve-Path $ProjectPath).Path

if ([string]::IsNullOrWhiteSpace($LogFile)) {
    $LogFile = Join-Path $ProjectPath "Temp\Editor_diag_temp_leak.log"
}
if (-not [System.IO.Path]::IsPathRooted($LogFile)) {
    $LogFile = Join-Path $ProjectPath $LogFile
}
$logDirForDisplay = Split-Path -Parent $LogFile
$logLeaf = Split-Path -Leaf $LogFile
$LogFile = Join-Path $logDirForDisplay $logLeaf

if (-not (Test-Path $ProjectPath)) {
    throw "ProjectPath not found: $ProjectPath"
}

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $UnityExe = Find-UnityExe -version $UnityVersion
}
if ([string]::IsNullOrWhiteSpace($UnityExe) -or -not (Test-Path $UnityExe)) {
    throw "Unity.exe not found. Pass -UnityExe `"C:\...\Unity.exe`" or install via Unity Hub under C:\Program Files\Unity\Hub\Editor\."
}

$logDir = Split-Path -Parent $LogFile
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir | Out-Null
}

if ([string]::IsNullOrWhiteSpace($TestResults) -and $RunEditModeTests) {
    $TestResults = Join-Path $ProjectPath "Temp\TestResults_editmode_diag.xml"
}

$args = @(
    "-projectPath", $ProjectPath,
    "-diag-temp-memory-leak-validation",
    "-logFile", $LogFile
)

if ($BatchMode) {
    $args += "-batchmode"
    $args += "-nographics"
}

if ($RunEditModeTests) {
    $args += @(
        "-runTests",
        "-testPlatform", "editmode",
        "-testResults", $TestResults,
        "-quit"
    )
}

$argLine = ($args | ForEach-Object { Quote-Arg $_ }) -join " "

Write-Host "Unity:     $UnityExe"
Write-Host "Project:   $ProjectPath"
Write-Host "LogFile:   $LogFile"
if ($RunEditModeTests) { Write-Host "TestResults: $TestResults" }
Write-Host ""
Write-Host "Command:"
Write-Host ("  " + (Quote-Arg $UnityExe) + " " + $argLine)
Write-Host ""

if ($PSCmdlet.ShouldProcess($UnityExe, "Start Unity with temp leak validation")) {
    Start-Process -FilePath $UnityExe -ArgumentList $argLine -WorkingDirectory $ProjectPath | Out-Null
    Write-Host "Launched. Reproduce the warning, then share the log:"
    Write-Host "  $LogFile"
}
