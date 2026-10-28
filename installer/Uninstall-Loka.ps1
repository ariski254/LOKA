# ==============================================================================
# LOKA - Windows Native Uninstaller Script
# Fulfills Sections 15 and 56 of Fitur.md (No unexpected data loss)
# ==============================================================================
[CmdletBinding()]
param ()

$ErrorActionPreference = "Stop"

# 1. Administrator Elevation Check
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warning "Administrator privileges required to uninstall LOKA."
    Start-Process powershell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"") -Verb RunAs
    exit
}

Write-Host "==========================================================" -ForegroundColor Yellow
Write-Host "                  UNINSTALL LOKA                          " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "What would you like to remove?" -ForegroundColor White
Write-Host ""
Write-Host "  [1] Application only (Default & Recommended)" -ForegroundColor Cyan
Write-Host "      - Removes Loka binaries from Program Files"
Write-Host "      - Keeps your projects, databases, runtimes, and configs intact"
Write-Host ""
Write-Host "  [2] Application + Configuration" -ForegroundColor Yellow
Write-Host "      - Removes Loka binaries and user configuration (%AppData%\Loka)"
Write-Host "      - Keeps your development projects, databases, and runtimes"
Write-Host ""
Write-Host "  [3] Application + Entire Environment (DANGEROUS)" -ForegroundColor Red
Write-Host "      - Removes binaries, configuration, and the entire C:\Loka environment"
Write-Host "      - WARNING: All projects, databases, and certificates will be deleted!"
Write-Host ""

$choice = Read-Host "Select option [1, 2, or 3] (Default is 1)"
if ([string]::IsNullOrWhiteSpace($choice)) {
    $choice = "1"
}

# Close any running Loka and stack processes
$processNames = @("Loka", "Loka.App", "nginx", "httpd", "mariadbd", "mysqld", "postgres", "php-cgi")
foreach ($name in $processNames) {
    Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Milliseconds 500

$installDir = "C:\Program Files\Loka"
$envDir = "C:\Loka"
$appDataDir = [System.IO.Path]::Combine($env:APPDATA, "Loka")

# Option 3 Safety Confirmation per Section 15 & 55
if ($choice -eq "3") {
    Write-Host ""
    Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
    Write-Host "                         WARNING                          " -ForegroundColor Red
    Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
    Write-Host "This will permanently remove:" -ForegroundColor White
    Write-Host "  - Projects (in $envDir\projects)" -ForegroundColor Red
    Write-Host "  - Databases (MySQL / MariaDB data)" -ForegroundColor Red
    Write-Host "  - Runtimes (PHP, Node, Apache, etc.)" -ForegroundColor Red
    Write-Host "  - Configuration files" -ForegroundColor Red
    Write-Host "  - Logs" -ForegroundColor Red
    Write-Host "  - SSL certificates" -ForegroundColor Red
    Write-Host ""
    
    $confirm = Read-Host "Are you absolutely sure you want to delete your entire environment? Type 'DELETE' to confirm"
    if ($confirm -ne "DELETE") {
        Write-Host "Environment deletion aborted by user. Falling back to Application only removal." -ForegroundColor Yellow
        $choice = "1"
    }
}

Write-Host ""
Write-Host "Proceeding with uninstallation..." -ForegroundColor Cyan

# Remove Shortcuts
$startShortcut = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) "Loka.lnk"
if (Test-Path $startShortcut) {
    Remove-Item -Path $startShortcut -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Removed Start Menu shortcut." -ForegroundColor DarkGray
}

$desktopShortcut = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) "Loka.lnk"
if (Test-Path $desktopShortcut) {
    Remove-Item -Path $desktopShortcut -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Removed Desktop shortcut." -ForegroundColor DarkGray
}

# Remove Registry Entry
$uninstallRegPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Loka"
if (Test-Path $uninstallRegPath) {
    Remove-Item -Path $uninstallRegPath -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Removed Windows Add/Remove Programs entry." -ForegroundColor DarkGray
}

# Remove Application Binaries & Directory
if (Test-Path $installDir) {
    Get-ChildItem -Path $installDir -Exclude "Uninstall-Loka.ps1" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c ping 127.0.0.1 -n 3 > NUL & if exist `"$installDir`" rd /s /q `"$installDir`"" -WindowStyle Hidden
    Write-Host "  -> Removed application binaries and folder: $installDir." -ForegroundColor DarkGray
}

# Handle Options 2 and 3
if ($choice -eq "2" -or $choice -eq "3") {
    if (Test-Path $appDataDir) {
        Remove-Item -Path $appDataDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  -> Removed configuration directory: $appDataDir" -ForegroundColor DarkGray
    }
}

if ($choice -eq "3") {
    if (Test-Path $envDir) {
        Remove-Item -Path $envDir -Recurse -Force -ErrorAction SilentlyContinue
        Start-Process -FilePath "cmd.exe" -ArgumentList "/c ping 127.0.0.1 -n 3 > NUL & if exist `"$envDir`" rd /s /q `"$envDir`"" -WindowStyle Hidden
        Write-Host "  -> Removed environment directory and folder: $envDir" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "Loka has been successfully uninstalled from your computer." -ForegroundColor Green
if ($choice -ne "3") {
    Write-Host "Your development environment and projects in '$envDir' have been safely preserved." -ForegroundColor Cyan
}
Write-Host ""
