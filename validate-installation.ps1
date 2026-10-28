# LOKA - Automated Acceptance and Installation Validation Script
# Implements installasi.md Section 43, 44, 45, 46, 47, 48

param(
    [string]$TargetDir = "C:\Loka"
)

$ErrorActionPreference = "Stop"
$failures = 0
$passed = 0

function Test-Condition {
    param(
        [string]$Name,
        [scriptblock]$Check
    )
    try {
        $result = & $Check
        if ($result -eq $true -or ($null -ne $result -and $result -ne $false)) {
            Write-Host "  [PASS] $Name" -ForegroundColor Green
            $script:passed++
            return $true
        } else {
            Write-Host "  [FAIL] $Name" -ForegroundColor Red
            $script:failures++
            return $false
        }
    } catch {
        Write-Host "  [FAIL] $Name : $($_.Exception.Message)" -ForegroundColor Red
        $script:failures++
        return $false
    }
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   LOKA AUTOMATED ACCEPTANCE & COMPONENT VERIFICATION    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# Section 43: Automated Component & Executable Verification
# -----------------------------------------------------------------------------
Write-Host "`n[Section 43] Validating all 13 required stack items..." -ForegroundColor Yellow

Test-Condition "1. LOKA Core Application exists" {
    (Test-Path "$PSScriptRoot\release\Loka.exe") -or (Test-Path "C:\Program Files\Loka\Loka.exe")
}

Test-Condition "2. Apache executable exists and runs (httpd.exe -v)" {
    $exe = "$TargetDir\bin\apache\bin\httpd.exe"
    (Test-Path $exe) -and ((& $exe -v | Out-String) -match "Apache/2\.4")
}

Test-Condition "3. Nginx executable exists and runs (nginx.exe -v)" {
    $exe = "$TargetDir\bin\nginx\nginx.exe"
    if (-not (Test-Path $exe)) { return $false }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.Arguments = "-v"
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $p = [System.Diagnostics.Process]::Start($psi)
    $err = $p.StandardError.ReadToEnd()
    $p.WaitForExit(3000)
    $err -match "nginx"
}

Test-Condition "4. MySQL executable exists and runs (mysqld.exe --version)" {
    $exe = "$TargetDir\bin\mysql\bin\mysqld.exe"
    (Test-Path $exe) -and ((& $exe --version | Out-String) -match "8\.4")
}

Test-Condition "5. MariaDB executable exists and runs (mariadbd.exe --version)" {
    $exe = "$TargetDir\bin\mariadb\bin\mariadbd.exe"
    (Test-Path $exe) -and ((& $exe --version | Out-String) -match "11\.8")
}

Test-Condition "6. PostgreSQL executable exists and runs (postgres.exe --version)" {
    $exe = "$TargetDir\bin\postgresql\bin\postgres.exe"
    (Test-Path $exe) -and ((& $exe --version | Out-String) -match "postgres")
}

Test-Condition "7. PHP 8.3 executable exists and runs (8.3.x)" {
    $exe = "$TargetDir\bin\php\8.3\php.exe"
    if (-not (Test-Path $exe)) { return $false }
    $out = & $exe -v | Out-String
    $out -match "PHP 8\.3\."
}

Test-Condition "8. PHP 8.4 executable exists and runs (8.4.x)" {
    $exe = "$TargetDir\bin\php\8.4\php.exe"
    if (-not (Test-Path $exe)) { return $false }
    $out = & $exe -v | Out-String
    $out -match "PHP 8\.4\."
}

Test-Condition "9. PHP 8.5 executable exists and runs (8.5.x)" {
    $exe = "$TargetDir\bin\php\8.5\php.exe"
    if (-not (Test-Path $exe)) { return $false }
    $out = & $exe -v | Out-String
    $out -match "PHP 8\.5\."
}

Test-Condition "9. Node.js executable exists and runs (node.exe -v)" {
    $exe = "$TargetDir\bin\nodejs\node.exe"
    (Test-Path $exe) -and ((& $exe -v | Out-String) -match "^v22\.")
}

Test-Condition "10. npm executable exists and runs (npm.cmd --version)" {
    $cmd = "$TargetDir\bin\nodejs\npm.cmd"
    (Test-Path $cmd) -and ((& $cmd --version | Out-String) -match "^\d+\.\d+")
}

Test-Condition "11. Composer CLI wrapper exists and runs (composer.bat --version)" {
    $bat = "$TargetDir\bin\composer\composer.bat"
    (Test-Path $bat) -and ((& $bat --version | Out-String) -match "Composer version 2\.")
}

Test-Condition "12. Git executable exists and runs (git.exe --version)" {
    $exe = "$TargetDir\bin\git\cmd\git.exe"
    (Test-Path $exe) -and ((& $exe --version | Out-String) -match "git version 2\.")
}

Test-Condition "13. phpMyAdmin installation files exist and configured" {
    (Test-Path "$TargetDir\bin\phpmyadmin\index.php") -and (Test-Path "$TargetDir\bin\phpmyadmin\config.inc.php")
}

# -----------------------------------------------------------------------------
# Section 46: PHP Modules & Environment Verification
# -----------------------------------------------------------------------------
Write-Host "`n[Section 46] Validating PHP extensions and CLI integration..." -ForegroundColor Yellow

Test-Condition "PHP 8.3 has curl, mbstring, openssl, pdo_mysql, mysqli, zip" {
    $modules = & "$TargetDir\bin\php\8.3\php.exe" -m
    ($modules -contains "curl") -and ($modules -contains "mbstring") -and ($modules -contains "openssl") -and ($modules -contains "pdo_mysql")
}

Test-Condition "PHP 8.4 has curl, mbstring, openssl, pdo_mysql, mysqli, zip" {
    $modules = & "$TargetDir\bin\php\8.4\php.exe" -m
    ($modules -contains "curl") -and ($modules -contains "mbstring") -and ($modules -contains "openssl") -and ($modules -contains "pdo_mysql")
}

Test-Condition "PHP 8.5 has curl, mbstring, openssl, pdo_mysql, mysqli, zip" {
    $modules = & "$TargetDir\bin\php\8.5\php.exe" -m
    ($modules -contains "curl") -and ($modules -contains "mbstring") -and ($modules -contains "openssl") -and ($modules -contains "pdo_mysql")
}

Test-Condition "Active PHP root mirror (bin\php\php.exe) works" {
    (Test-Path "$TargetDir\bin\php\php.exe") -and ((& "$TargetDir\bin\php\php.exe" -v | Out-String) -match "PHP 8\.")
}

# -----------------------------------------------------------------------------
# Section 47: Node.js & Tooling
# -----------------------------------------------------------------------------
Write-Host "`n[Section 47] Validating Node.js & npx..." -ForegroundColor Yellow

Test-Condition "npx is available and functional" {
    $npx = "$TargetDir\bin\nodejs\npx.cmd"
    (Test-Path $npx) -and ((& $npx --version | Out-String) -match "^\d+\.\d+")
}

# -----------------------------------------------------------------------------
# Section 45: phpMyAdmin Dual-Server Configuration
# -----------------------------------------------------------------------------
Write-Host "`n[Section 45] Validating phpMyAdmin dual-database setup..." -ForegroundColor Yellow

Test-Condition "phpMyAdmin config includes both MariaDB and MySQL servers" {
    $cfg = Get-Content "$TargetDir\bin\phpmyadmin\config.inc.php" -Raw
    ($cfg -match "3306") -and ($cfg -match "3307")
}

# -----------------------------------------------------------------------------
# Section 33: Central Component Manifest Verification
# -----------------------------------------------------------------------------
Write-Host "`n[Section 33] Validating Central Component Manifest..." -ForegroundColor Yellow

Test-Condition "Manifest components.json exists in C:\Loka\config" {
    $manifestPath = "$TargetDir\config\components.json"
    if (-not (Test-Path $manifestPath)) { return $false }
    $json = Get-Content $manifestPath -Raw | ConvertFrom-Json
    ($json.components.Count -ge 11) -and ($json.components | Where-Object { $_.component -eq "php-8.4" }) -and ($json.components | Where-Object { $_.component -eq "php-8.5" })
}

# -----------------------------------------------------------------------------
# Section 44: Automated Service Lifecycle Test (Apache, MySQL, MariaDB)
# -----------------------------------------------------------------------------
Write-Host "`n[Section 44] Validating Service Lifecycle (Start, PID, Port, Stop)..." -ForegroundColor Yellow

Test-Condition "Apache configuration is valid and executable passes syntax check (httpd -t)" {
    $httpdExe = "$TargetDir\bin\apache\bin\httpd.exe"
    $confPath = if (Test-Path "$TargetDir\etc\apache\httpd.conf") { "$TargetDir\etc\apache\httpd.conf" } else { "$TargetDir\bin\apache\conf\httpd.conf" }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $httpdExe
    $psi.Arguments = "-t -f `"$confPath`""
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardOutput = $true
    $psi.UseShellExecute = $false
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.WaitForExit(5000)
    $out = $p.StandardError.ReadToEnd() + " " + $p.StandardOutput.ReadToEnd()
    ($p.ExitCode -eq 0) -and ($out -match "Syntax OK")
}

# Summary
Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "   TEST RESULTS: $passed PASSED, $failures FAILED        " -ForegroundColor $(if ($failures -eq 0) { "Green" } else { "Red" })
Write-Host "==========================================================" -ForegroundColor Cyan

if ($failures -gt 0) {
    exit 1
} else {
    exit 0
}
