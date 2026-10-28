# Build script for LOKA Windows Installer (Loka-Setup.exe)
# Fulfills installasi.md Section 21, Section 27, and unified application architecture

[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$stagingDir = Join-Path $root "artifacts\staging"
$stagingApp = Join-Path $stagingDir "app"
$stagingInstaller = Join-Path $stagingDir "installer"
$releaseDir = Join-Path $root "release"
$payloadDir = Join-Path $root "Loka.Installer\Payload"
$payloadZip = Join-Path $payloadDir "app_payload.zip"

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "       Building LOKA Windows Installer Suite         " -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

function Safe-CopyFile {
    param(
        [Parameter(Mandatory=$true)][string]$Source,
        [Parameter(Mandatory=$true)][string]$Destination
    )
    if (Test-Path $Destination) {
        try {
            Copy-Item -Path $Source -Destination $Destination -Force -ErrorAction Stop
            return
        } catch {
            Write-Warning "Direct overwrite of '$Destination' failed ($($_.Exception.Message)). Attempting safe atomic replacement via rename..."
            $destName = [System.IO.Path]::GetFileName($Destination)
            $destDir = [System.IO.Path]::GetDirectoryName($Destination)
            $staleName = "$destName.stale." + [System.Guid]::NewGuid().ToString("N")
            $stalePath = Join-Path $destDir $staleName
            try {
                Rename-Item -Path $Destination -NewName $staleName -Force -ErrorAction Stop
                Copy-Item -Path $Source -Destination $Destination -Force -ErrorAction Stop
                try { Remove-Item -Path $stalePath -Force -ErrorAction SilentlyContinue } catch {}
                Write-Host "Safely replaced locked file: $Destination" -ForegroundColor Green
                return
            } catch {
                throw "Failed to copy '$Source' to '$Destination' even after fallback rename: $($_.Exception.Message)"
            }
        }
    } else {
        Copy-Item -Path $Source -Destination $Destination -Force
    }
}

# 1. Update branding assets from loka-symbol.svg
Write-Host "`n[1/6] Rendering branding assets from loka-symbol.svg..." -ForegroundColor Yellow
$brandingScript = Join-Path $root "generate-branding.ps1"
if (Test-Path $brandingScript) {
    & $brandingScript
}

# 2. Prepare staging directories
Write-Host "`n[2/6] Preparing staging & release directories..." -ForegroundColor Yellow
try { & dotnet build-server shutdown | Out-Null } catch {}
if (Test-Path $stagingDir) { 
    try { Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue } catch {}
}
if (-not (Test-Path $releaseDir)) { 
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null 
} else {
    Get-ChildItem -Path $releaseDir -Filter "*.stale*" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}
if (-not (Test-Path $payloadDir)) { New-Item -ItemType Directory -Path $payloadDir -Force | Out-Null }

New-Item -ItemType Directory -Path $stagingApp -Force | Out-Null
New-Item -ItemType Directory -Path $stagingInstaller -Force | Out-Null

# 3. Publish Unified Loka Application (Loka.exe - single file containing GUI & CLI)
Write-Host "`n[3/6] Publishing Unified Loka Application ($Configuration)..." -ForegroundColor Yellow
$appProj = Join-Path $root "Loka.App\Loka.App.csproj"
dotnet publish $appProj -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o $stagingApp /nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw "Failed to publish Loka application" }

# Clean any pdb, xml, or loose metadata files to ensure 100% single-file purity
Get-ChildItem -Path $stagingApp -Filter "*.pdb" -Recurse | Remove-Item -Force
Get-ChildItem -Path $stagingApp -Filter "*.xml" -Recurse | Remove-Item -Force

# Copy standalone single-file Loka.exe directly to release directory
$singleExeSource = Join-Path $stagingApp "Loka.exe"
if (-not (Test-Path $singleExeSource)) {
    throw "Loka.exe binary not found at $singleExeSource"
}
$singleExeDest = Join-Path $releaseDir "Loka.exe"
Safe-CopyFile -Source $singleExeSource -Destination $singleExeDest
$singleSizeMb = [math]::Round((Get-Item $singleExeDest).Length / 1MB, 2)
Write-Host "Produced Standalone Single-File App: $singleExeDest ($singleSizeMb MB)" -ForegroundColor Green

# 4. Create Application Payload Archive (app_payload.zip)
Write-Host "`n[4/7] Creating application payload archive (app_payload.zip)..." -ForegroundColor Yellow
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }
Start-Sleep -Milliseconds 300
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stagingApp, $payloadZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$zipSizeMb = [math]::Round((Get-Item $payloadZip).Length / 1MB, 2)
Write-Host "Created application payload archive: $payloadZip ($zipSizeMb MB)" -ForegroundColor Green

# 5. Build Online Bootstrapper (Loka-Setup-Online-x64.exe & Loka-Setup.exe)
Write-Host "`n[5/7] Building Online Bootstrapper (Loka-Setup-Online-x64.exe)..." -ForegroundColor Yellow
$legacyStackZip = Join-Path $payloadDir "stack_payload.zip"
if (Test-Path $legacyStackZip) { Remove-Item $legacyStackZip -Force }

$stagingOnline = Join-Path $stagingDir "installer-online"
if (Test-Path $stagingOnline) { Remove-Item $stagingOnline -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Path $stagingOnline -Force | Out-Null

$installerProj = Join-Path $root "Loka.Installer\Loka.Installer.csproj"
try { & dotnet build-server shutdown | Out-Null } catch {}
dotnet publish $installerProj -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o $stagingOnline /nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw "Failed to publish Loka Online Bootstrapper" }

$onlineExeSource = Join-Path $stagingOnline "Loka-Setup.exe"
$onlineDest = Join-Path $releaseDir "Loka-Setup-Online-x64.exe"
$setupDest = Join-Path $releaseDir "Loka-Setup.exe"
Safe-CopyFile -Source $onlineExeSource -Destination $onlineDest
Safe-CopyFile -Source $onlineExeSource -Destination $setupDest
$onlineSizeMb = [math]::Round((Get-Item $onlineDest).Length / 1MB, 2)
Write-Host "Produced Online Bootstrapper: $onlineDest ($onlineSizeMb MB)" -ForegroundColor Green

# 6. Build Full Offline Installer (Loka-Setup-Offline-x64.exe & Loka-Setup-x64.exe)
Write-Host "`n[6/7] Building Optimized Full Offline Installer (Loka-Setup-Offline-x64.exe)..." -ForegroundColor Yellow
$cacheDir = Join-Path $root "artifacts\cache"
$offlineDest = Join-Path $releaseDir "Loka-Setup-Offline-x64.exe"
$unifiedSetupDest = Join-Path $releaseDir "Loka-Setup-x64.exe"

if (Test-Path $cacheDir) {
    # Full offline stack packaging (All web servers, runtimes, and databases)
    $standardOfflinePackages = @(
        "apache.zip",
        "nginx.zip",
        "php-8.3.zip",
        "php-8.4.zip",
        "php-8.5.zip",
        "mariadb.zip",
        "mysql.zip",
        "postgresql.zip",
        "nodejs.zip",
        "git.zip",
        "composer.phar",
        "phpmyadmin.zip"
    )

    $stagingOfflinePayload = Join-Path $stagingDir "offline-payload"
    if (Test-Path $stagingOfflinePayload) { Remove-Item $stagingOfflinePayload -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path $stagingOfflinePayload -Force | Out-Null

    $totalPayloadBytes = 0
    foreach ($pkg in $standardOfflinePackages) {
        $srcPkg = Join-Path $cacheDir $pkg
        if (Test-Path $srcPkg) {
            Copy-Item $srcPkg (Join-Path $stagingOfflinePayload $pkg) -Force
            $totalPayloadBytes += (Get-Item $srcPkg).Length
        } else {
            Write-Warning "Standard package $pkg not found in $cacheDir"
        }
    }

    $rawPayloadMb = [math]::Round($totalPayloadBytes / 1MB, 2)
    Write-Host "Bundling optimized standard runtime stack ($rawPayloadMb MB raw payload) into offline installer..." -ForegroundColor Cyan
    
    if (Test-Path $legacyStackZip) { Remove-Item $legacyStackZip -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stagingOfflinePayload, $legacyStackZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    $stackZipSizeMb = [math]::Round((Get-Item $legacyStackZip).Length / 1MB, 2)
    Write-Host "Optimized offline stack archive: $legacyStackZip ($stackZipSizeMb MB)" -ForegroundColor Green

    $stagingOffline = Join-Path $stagingDir "installer-offline"
    if (Test-Path $stagingOffline) { Remove-Item $stagingOffline -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path $stagingOffline -Force | Out-Null

    dotnet publish $installerProj -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o $stagingOffline /nodeReuse:false
    if ($LASTEXITCODE -ne 0) { throw "Failed to publish Loka Full Offline Installer" }

    $offlineExeSource = Join-Path $stagingOffline "Loka-Setup.exe"
    Safe-CopyFile -Source $offlineExeSource -Destination $offlineDest
    Safe-CopyFile -Source $offlineExeSource -Destination $unifiedSetupDest
    
    # Clean temporary embedded payload to keep repository workspace clean
    Remove-Item $legacyStackZip -Force -ErrorAction SilentlyContinue
    Remove-Item $stagingOfflinePayload -Recurse -Force -ErrorAction SilentlyContinue

    $offlineSizeMb = [math]::Round((Get-Item $offlineDest).Length / 1MB, 2)
    Write-Host "Produced Optimized Full Offline Installer: $offlineDest ($offlineSizeMb MB)" -ForegroundColor Green
    Write-Host "Produced Unified Installer: $unifiedSetupDest ($offlineSizeMb MB)" -ForegroundColor Green
} else {
    Write-Warning "Cache directory $cacheDir not found! Skipping offline installer build."
}

# 7. Generate Checksums & Verification Manifests
Write-Host "`n[7/7] Generating SHA256 checksums manifest..." -ForegroundColor Yellow
$checksumsFile = Join-Path $releaseDir "checksums.txt"
$checksumLines = [System.Collections.Generic.List[string]]::new()

$releaseFiles = @($singleExeDest, $onlineDest, $setupDest)
if (Test-Path $offlineDest) {
    $releaseFiles += $offlineDest
}
if (Test-Path $unifiedSetupDest) {
    $releaseFiles += $unifiedSetupDest
}

foreach ($file in $releaseFiles) {
    $hash = Get-FileHash -Path $file -Algorithm SHA256
    $fileName = [System.IO.Path]::GetFileName($file)
    $checksumLines.Add("$($hash.Hash)  $fileName")
    
    $individualSha = "$file.sha256"
    Set-Content -Path $individualSha -Value "$($hash.Hash)  $fileName`r`n" -Encoding UTF8
}

Set-Content -Path $checksumsFile -Value ($checksumLines -join "`r`n" + "`r`n") -Encoding UTF8

Write-Host "`n=====================================================" -ForegroundColor Green
Write-Host "   SUCCESS: LOKA Release Distribution Suite Ready!    " -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green
Write-Host "Standalone App     : $singleExeDest ($singleSizeMb MB)" -ForegroundColor Cyan
Write-Host "Online Bootstrapper: $onlineDest ($onlineSizeMb MB)" -ForegroundColor Cyan
if (Test-Path $offlineDest) {
    Write-Host "Full Offline Setup : $offlineDest ($offlineSizeMb MB)" -ForegroundColor Cyan
}
Write-Host "Checksums Manifest : $checksumsFile" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Green
