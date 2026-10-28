# ==============================================================================
# LOKA - Windows Native Installer Script
# Fulfills Sections 5, 6, 14 of Fitur.md
# ==============================================================================
[CmdletBinding()]
param (
    [string]$InstallDir = "C:\Program Files\Loka",
    [switch]$CreateDesktopShortcut = $true,
    [switch]$LaunchAfterInstall = $false
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   LOKA - Local Development Environment Manager Setup     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Administrator Elevation Check
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warning "Administrator privileges required to install to '$InstallDir' and register in Windows."
    Write-Host "Restarting script with elevated privileges..." -ForegroundColor Yellow
    Start-Process powershell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"") -Verb RunAs
    exit
}

# 2. Build & Publish Release Binaries
$projectRoot = Split-Path -Parent $PSScriptRoot
Write-Host "[1/5] Compiling and publishing LOKA Release build..." -ForegroundColor Green

$env:PATH = "C:\Users\LENOVO\.dotnet;$env:PATH"
$publishDir = Join-Path $projectRoot "dist\Loka.App"
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force -ErrorAction SilentlyContinue
}

$buildProcess = Start-Process -FilePath "dotnet" -ArgumentList "publish `"$projectRoot\Loka.App\Loka.App.csproj`" -c Release -o `"$publishDir`" --nologo" -NoNewWindow -Wait -PassThru
if ($buildProcess.ExitCode -ne 0) {
    Write-Error "Failed to publish Loka.App. Please ensure 'dotnet build' succeeds."
    exit 1
}

# 3. Install Files to Target Directory
Write-Host "[2/5] Copying binaries to '$InstallDir'..." -ForegroundColor Green
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Copy-Item -Path "$publishDir\*" -Destination $InstallDir -Recurse -Force

# Copy Logo & Icon
$resourcesDir = Join-Path $InstallDir "Resources"
if (-not (Test-Path $resourcesDir)) {
    New-Item -ItemType Directory -Path $resourcesDir -Force | Out-Null
}
if (Test-Path "$projectRoot\logo.png") {
    Copy-Item -Path "$projectRoot\logo.png" -Destination "$resourcesDir\logo.png" -Force
}

# Copy Uninstaller script to target
Copy-Item -Path "$PSScriptRoot\Uninstall-Loka.ps1" -Destination "$InstallDir\Uninstall-Loka.ps1" -Force

# 4. Create Windows Shortcuts
Write-Host "[3/5] Creating Start Menu & Desktop shortcuts..." -ForegroundColor Green
$wshShell = New-Object -ComObject WScript.Shell
$targetExe = Join-Path $InstallDir "Loka.App.exe"

# Start Menu Shortcut
$startMenuPath = [Environment]::GetFolderPath('CommonPrograms')
$startShortcutPath = Join-Path $startMenuPath "Loka.lnk"
$startShortcut = $wshShell.CreateShortcut($startShortcutPath)
$startShortcut.TargetPath = $targetExe
$startShortcut.WorkingDirectory = $InstallDir
$startShortcut.Description = "Loka Local Development Environment Manager"
if (Test-Path "$resourcesDir\logo.png") {
    $startShortcut.IconLocation = "$targetExe,0"
}
$startShortcut.Save()
Write-Host "  -> Created Start Menu shortcut: $startShortcutPath" -ForegroundColor DarkGray

# Desktop Shortcut
if ($CreateDesktopShortcut) {
    $desktopPath = [Environment]::GetFolderPath('CommonDesktopDirectory')
    $desktopShortcutPath = Join-Path $desktopPath "Loka.lnk"
    $desktopShortcut = $wshShell.CreateShortcut($desktopShortcutPath)
    $desktopShortcut.TargetPath = $targetExe
    $desktopShortcut.WorkingDirectory = $InstallDir
    $desktopShortcut.Description = "Loka Local Development Environment Manager"
    $desktopShortcut.IconLocation = "$targetExe,0"
    $desktopShortcut.Save()
    Write-Host "  -> Created Desktop shortcut: $desktopShortcutPath" -ForegroundColor DarkGray
}

# 5. Register in Windows Add/Remove Programs (Registry)
Write-Host "[4/5] Registering in Windows Add/Remove Programs..." -ForegroundColor Green
$uninstallRegPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Loka"
if (-not (Test-Path $uninstallRegPath)) {
    New-Item -Path $uninstallRegPath -Force | Out-Null
}

$uninstallCmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$InstallDir\Uninstall-Loka.ps1`""

Set-ItemProperty -Path $uninstallRegPath -Name "DisplayName" -Value "Loka - Windows Local Development Environment"
Set-ItemProperty -Path $uninstallRegPath -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $uninstallRegPath -Name "Publisher" -Value "Loka Team"
Set-ItemProperty -Path $uninstallRegPath -Name "DisplayIcon" -Value "$targetExe,0"
Set-ItemProperty -Path $uninstallRegPath -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $uninstallRegPath -Name "UninstallString" -Value $uninstallCmd
Set-ItemProperty -Path $uninstallRegPath -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallRegPath -Name "NoRepair" -Value 1 -Type DWord

Write-Host "[5/5] Installation completed successfully!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Loka has been installed to: $InstallDir" -ForegroundColor White
Write-Host "Default environment root:   C:\Loka" -ForegroundColor White
Write-Host ""

if ($LaunchAfterInstall) {
    Write-Host "Launching Loka..." -ForegroundColor Green
    Start-Process -FilePath $targetExe
}
