param(
    [string]$CacheDir = "$PSScriptRoot\cache",
    [string]$StagingDir = "$PSScriptRoot\staging\stack",
    [string]$OutputZip = "$PSScriptRoot\..\Loka.Installer\Payload\stack_payload.zip"
)

$ErrorActionPreference = "Stop"

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "     Preparing LOKA Stack Runtime Payload (C:\Loka)  " -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

# 1. Ensure cache has all components
$downloadScript = Join-Path $PSScriptRoot "download-stack.ps1"
if (Test-Path $downloadScript) {
    & $downloadScript -CacheDir $CacheDir
}

# 2. Reset staging directory
if (Test-Path $StagingDir) {
    Remove-Item $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem

# Helper to extract zip and strip common single top-level directory
function Extract-ArchiveStripped([string]$ZipPath, [string]$DestDir) {
    if (-not (Test-Path $DestDir)) { New-Item -ItemType Directory -Path $DestDir -Force | Out-Null }
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    
    # Determine if there is a single common top-level directory
    $topLevels = @()
    foreach ($entry in $archive.Entries) {
        $clean = $entry.FullName.Replace('\', '/').Trim('/')
        if ($clean) {
            $firstPart = $clean.Split('/')[0]
            if ($firstPart -and $topLevels -notcontains $firstPart) {
                $topLevels += $firstPart
            }
        }
    }

    $rootPrefix = if ($topLevels.Count -eq 1) { $topLevels[0] + "/" } else { "" }

    foreach ($entry in $archive.Entries) {
        $norm = $entry.FullName.Replace('\', '/')
        if ($rootPrefix -and $norm.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $norm = $norm.Substring($rootPrefix.Length)
        }
        if (-not $norm) { continue }

        $destFile = Join-Path $DestDir ($norm.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        if ([string]::IsNullOrEmpty($entry.Name)) {
            if (-not (Test-Path $destFile)) { New-Item -ItemType Directory -Path $destFile -Force | Out-Null }
        } else {
            $parent = Split-Path -Parent $destFile
            if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destFile, $true)
        }
    }
    $archive.Dispose()
}

# 3. Extract Components
# 3.1 Nginx
Write-Host "`n[1/5] Extracting Nginx..." -ForegroundColor Yellow
$nginxZip = Join-Path $CacheDir "nginx.zip"
$nginxDest = Join-Path $StagingDir "bin\nginx"
Extract-ArchiveStripped $nginxZip $nginxDest
Write-Host "  -> Nginx installed at $nginxDest" -ForegroundColor DarkGray

# 3.2 PHP 8.3
Write-Host "`n[2/5] Extracting PHP 8.3..." -ForegroundColor Yellow
$phpZip = Join-Path $CacheDir "php-8.3.zip"
$php83Dest = Join-Path $StagingDir "bin\php\8.3"
$phpBaseDest = Join-Path $StagingDir "bin\php"
Extract-ArchiveStripped $phpZip $php83Dest

# Mirror to bin\php
if (-not (Test-Path $phpBaseDest)) { New-Item -ItemType Directory -Path $phpBaseDest -Force | Out-Null }
Get-ChildItem -Path $php83Dest -Filter "*.exe" | Copy-Item -Destination $phpBaseDest -Force
Get-ChildItem -Path $php83Dest -Filter "*.dll" | Copy-Item -Destination $phpBaseDest -Force

# Generate php.ini
$phpIniContent = @'
[PHP]
engine = On
short_open_tag = Off
precision = 14
output_buffering = 4096
zlib.output_compression = Off
implicit_flush = Off
serialize_precision = -1
zend.enable_gc = On
zend.exception_ignore_args = On
zend.exception_string_param_max_len = 0

max_execution_time = 300
max_input_time = 60
memory_limit = 512M
error_reporting = E_ALL & ~E_DEPRECATED & ~E_STRICT
display_errors = On
display_startup_errors = On
log_errors = On
log_errors_max_len = 1024
ignore_repeated_errors = Off
ignore_repeated_source = Off
report_memleaks = On

variables_order = "GPCS"
request_order = "GP"
register_argc_argv = Off
auto_globals_jit = On
post_max_size = 128M

default_mimetype = "text/html"
default_charset = "UTF-8"

enable_dl = Off
file_uploads = On
upload_max_filesize = 128M
max_file_uploads = 20

allow_url_fopen = On
allow_url_include = Off
default_socket_timeout = 60

extension_dir = "ext"

extension=curl
extension=fileinfo
extension=mbstring
extension=openssl
extension=pdo_mysql
extension=mysqli
extension=zip

[Date]
date.timezone = UTC

[Session]
session.save_handler = files
session.use_strict_mode = 0
session.use_cookies = 1
session.use_only_cookies = 1
session.name = PHPSESSID
session.auto_start = 0
session.cookie_lifetime = 0
session.cookie_path = /
session.serialize_handler = php
session.gc_probability = 1
session.gc_divisor = 1000
session.gc_maxlifetime = 1440
session.cache_limiter = nocache
session.cache_expire = 180
session.use_trans_sid = 0
session.sid_length = 26
session.trans_sid_tags = "a=href,area=href,frame=src,form="
session.sid_bits_per_character = 5
'@
Set-Content -Path (Join-Path $php83Dest "php.ini") -Value $phpIniContent -Encoding UTF8
Set-Content -Path (Join-Path $phpBaseDest "php.ini") -Value $phpIniContent -Encoding UTF8
Write-Host "  -> PHP 8.3 & php.ini configured" -ForegroundColor DarkGray

# 3.3 Composer
Write-Host "`n[3/5] Setting up Composer..." -ForegroundColor Yellow
$composerDest = Join-Path $StagingDir "bin\composer"
if (-not (Test-Path $composerDest)) { New-Item -ItemType Directory -Path $composerDest -Force | Out-Null }
Copy-Item (Join-Path $CacheDir "composer.phar") (Join-Path $composerDest "composer.phar") -Force
$composerBat = "@echo off`r`n`"%~dp0..\php\8.3\php.exe`" `"%~dp0composer.phar`" %*`r`n"
Set-Content -Path (Join-Path $composerDest "composer.bat") -Value $composerBat -Encoding ASCII
Write-Host "  -> Composer wrapper configured" -ForegroundColor DarkGray

# 3.4 phpMyAdmin
Write-Host "`n[4/5] Extracting phpMyAdmin..." -ForegroundColor Yellow
$pmaZip = Join-Path $CacheDir "phpmyadmin.zip"
$pmaDest = Join-Path $StagingDir "bin\phpmyadmin"
Extract-ArchiveStripped $pmaZip $pmaDest

$pmaConfig = @'
<?php
$cfg['blowfish_secret'] = 'loka_development_secret_32_chars_12345';
$i = 1;
$cfg['Servers'][$i]['auth_type'] = 'config';
$cfg['Servers'][$i]['host'] = '127.0.0.1';
$cfg['Servers'][$i]['port'] = '3307';
$cfg['Servers'][$i]['user'] = 'root';
$cfg['Servers'][$i]['password'] = '';
$cfg['Servers'][$i]['AllowNoPassword'] = true;
$cfg['UploadDir'] = '';
$cfg['SaveDir'] = '';
'@
Set-Content -Path (Join-Path $pmaDest "config.inc.php") -Value $pmaConfig -Encoding UTF8
Write-Host "  -> phpMyAdmin configured for MariaDB (3307)" -ForegroundColor DarkGray

# 3.5 MariaDB
Write-Host "`n[5/5] Extracting MariaDB..." -ForegroundColor Yellow
$mariaZip = if (Test-Path (Join-Path $CacheDir "mariadb.zip")) { Join-Path $CacheDir "mariadb.zip" } elseif (Test-Path (Join-Path $CacheDir "mariadb-11.8.zip")) { Join-Path $CacheDir "mariadb-11.8.zip" } else { Join-Path $CacheDir "mariadb-11.4.zip" }
$mariaDest = Join-Path $StagingDir "bin\mariadb"
Extract-ArchiveStripped $mariaZip $mariaDest

# Initialize data directory
$mariaDataDir = Join-Path $StagingDir "data\mariadb"
if (-not (Test-Path $mariaDataDir)) { New-Item -ItemType Directory -Path $mariaDataDir -Force | Out-Null }

$installDbExe = Join-Path $mariaDest "bin\mysql_install_db.exe"
if (-not (Test-Path $installDbExe)) {
    $installDbExe = Join-Path $mariaDest "bin\mariadb-install-db.exe"
}

if (Test-Path $installDbExe) {
    Write-Host "  -> Initializing MariaDB system tables..." -ForegroundColor DarkGray
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $installDbExe
    $psi.Arguments = "--datadir=`"$mariaDataDir`" --default-user --port=3307"
    $psi.WorkingDirectory = (Split-Path -Parent $installDbExe)
    $psi.CreateNoWindow = $true
    $psi.UseShellExecute = $false
    $proc = [System.Diagnostics.Process]::Start($psi)
    $proc.WaitForExit()
}

# my.ini
$etcMaria = Join-Path $StagingDir "etc\mariadb"
if (-not (Test-Path $etcMaria)) { New-Item -ItemType Directory -Path $etcMaria -Force | Out-Null }
$myIni = @"
[mysqld]
port = 3307
basedir = "C:/Loka/bin/mariadb"
datadir = "C:/Loka/data/mariadb"
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
default_storage_engine = InnoDB
max_allowed_packet = 64M
bind-address = 127.0.0.1
sql_mode = "NO_ENGINE_SUBSTITUTION"

[client]
port = 3307
default-character-set = utf8mb4
"@
Set-Content -Path (Join-Path $etcMaria "my.ini") -Value $myIni -Encoding UTF8
Write-Host "  -> MariaDB configuration & system tables ready" -ForegroundColor DarkGray

# 3.6 Apache HTTP Server
Write-Host "`n[6/7] Extracting Apache 2.4..." -ForegroundColor Yellow
$apacheZip = Join-Path $CacheDir "apache.zip"
$apacheDest = Join-Path $StagingDir "bin\apache"
Extract-ArchiveStripped $apacheZip $apacheDest

$apacheConfPath = Join-Path $apacheDest "conf\httpd.conf"
if (Test-Path $apacheConfPath) {
    $c = Get-Content $apacheConfPath -Raw
    $c = $c -replace 'C:/Apache24-64', 'C:/Loka/bin/apache'
    $c = $c -replace 'Define SRVROOT ".*"', 'Define SRVROOT "C:/Loka/bin/apache"'
    $c = $c -replace 'ServerRoot ".*"', 'ServerRoot "C:/Loka/bin/apache"'
    $c = $c -replace 'Listen 80\b', 'Listen 8080'
    $c = $c -replace '#LoadModule rewrite_module modules/mod_rewrite.so', 'LoadModule rewrite_module modules/mod_rewrite.so'
    $c = $c -replace '#LoadModule proxy_module modules/mod_proxy.so', 'LoadModule proxy_module modules/mod_proxy.so'
    $c = $c -replace '#LoadModule proxy_fcgi_module modules/mod_proxy_fcgi.so', 'LoadModule proxy_fcgi_module modules/mod_proxy_fcgi.so'
    $c = $c -replace '#LoadModule vhost_alias_module modules/mod_vhost_alias.so', 'LoadModule vhost_alias_module modules/mod_vhost_alias.so'
    $c = $c -replace '#LoadModule alias_module modules/mod_alias.so', 'LoadModule alias_module modules/mod_alias.so'
    $c = $c -replace 'DocumentRoot "C:/Loka/bin/apache/htdocs"', 'DocumentRoot "C:/Loka/projects"'
    $c = $c -replace '<Directory "C:/Loka/bin/apache/htdocs">', '<Directory "C:/Loka/projects">'
    if ($c -notmatch 'Loka Default VirtualHost') {
        $defaultVhost = @"

# Loka Default VirtualHost (Catch-all for localhost & 127.0.0.1)
<VirtualHost *:8080>
    ServerName localhost
    ServerAlias 127.0.0.1
    DocumentRoot "C:/Loka/projects"

    <Directory "C:/Loka/projects">
        Options Indexes FollowSymLinks MultiViews
        AllowOverride All
        Require all granted
        DirectoryIndex index.php index.html index.htm
        <IfModule mod_rewrite.c>
            RewriteEngine On
        </IfModule>
        <IfModule mod_proxy_fcgi.c>
            ProxyFCGISetEnvIf "true" SCRIPT_FILENAME "%{reqenv:DOCUMENT_ROOT}%{reqenv:SCRIPT_NAME}"
            <FilesMatch \.php$>
                SetHandler "proxy:fcgi://127.0.0.1:9000/"
            </FilesMatch>
        </IfModule>
    </Directory>

    Alias /phpmyadmin "C:/Loka/bin/phpmyadmin"
    <Directory "C:/Loka/bin/phpmyadmin">
        Options Indexes FollowSymLinks MultiViews
        AllowOverride All
        Require all granted
        DirectoryIndex index.php index.html index.htm
        <IfModule mod_proxy_fcgi.c>
            ProxyFCGISetEnvIf "reqenv('SCRIPT_NAME') =~ m#^/phpmyadmin/(.*)$#" SCRIPT_FILENAME "C:/Loka/bin/phpmyadmin/$1"
            <FilesMatch \.php$>
                SetHandler "proxy:fcgi://127.0.0.1:9000/"
            </FilesMatch>
        </IfModule>
    </Directory>

    ErrorLog "C:/Loka/logs/apache_default_error.log"
    CustomLog "C:/Loka/logs/apache_default_access.log" common
</VirtualHost>
"@
        $c += "`r`n`r`n" + $defaultVhost
    }
    if ($c -notmatch 'Loka Projects Sites') {
        $c += "`r`n`r`n# Loka Projects Sites`r`nIncludeOptional `"C:/Loka/config/apache/sites-enabled/*.conf`"`r`nIncludeOptional `"C:/Loka/etc/apache/sites-enabled/*.conf`"`r`n"
    }
    Set-Content -Path $apacheConfPath -Value $c -Encoding UTF8

    $etcApache = Join-Path $StagingDir "etc\apache"
    if (-not (Test-Path $etcApache)) { New-Item -ItemType Directory -Path $etcApache -Force | Out-Null }
    Copy-Item $apacheConfPath (Join-Path $etcApache "httpd.conf") -Force
}
Write-Host "  -> Apache 2.4 configured with mod_proxy_fcgi & sites-enabled" -ForegroundColor DarkGray

# 3.7 PostgreSQL
Write-Host "`n[7/7] Extracting PostgreSQL 16..." -ForegroundColor Yellow
$pgZip = Join-Path $CacheDir "postgresql.zip"
$pgDest = Join-Path $StagingDir "bin\postgresql"
if (-not (Test-Path $pgDest)) { New-Item -ItemType Directory -Path $pgDest -Force | Out-Null }

$archivePg = [System.IO.Compression.ZipFile]::OpenRead($pgZip)
$pgPrefix = "pgsql/"
foreach ($entry in $archivePg.Entries) {
    $norm = $entry.FullName.Replace('\', '/')
    if ($norm.StartsWith($pgPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $norm = $norm.Substring($pgPrefix.Length)
    }
    if (-not $norm) { continue }
    if ($norm.StartsWith("symbols/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $norm.StartsWith("doc/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $norm.StartsWith("include/", [System.StringComparison]::OrdinalIgnoreCase)) {
        continue
    }

    $destFile = Join-Path $pgDest ($norm.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    if ([string]::IsNullOrEmpty($entry.Name)) {
        if (-not (Test-Path $destFile)) { New-Item -ItemType Directory -Path $destFile -Force | Out-Null }
    } else {
        $parent = Split-Path -Parent $destFile
        if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destFile, $true)
    }
}
$archivePg.Dispose()

# Initialize PostgreSQL data cluster
$pgDataDir = Join-Path $StagingDir "data\postgresql"
if (-not (Test-Path $pgDataDir)) { New-Item -ItemType Directory -Path $pgDataDir -Force | Out-Null }

$initDbExe = Join-Path $pgDest "bin\initdb.exe"
if (Test-Path $initDbExe) {
    Write-Host "  -> Initializing PostgreSQL cluster..." -ForegroundColor DarkGray
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $initDbExe
    $psi.Arguments = "-D `"$pgDataDir`" -U postgres -A trust -E UTF8"
    $psi.WorkingDirectory = (Split-Path -Parent $initDbExe)
    $psi.CreateNoWindow = $true
    $psi.UseShellExecute = $false
    $proc = [System.Diagnostics.Process]::Start($psi)
    $proc.WaitForExit()
}
Write-Host "  -> PostgreSQL database cluster initialized" -ForegroundColor DarkGray

# 4. Create common environment directories
$commonDirs = @(
    "projects",
    "logs",
    "logs\apache",
    "logs\nginx",
    "logs\mariadb",
    "logs\postgresql",
    "tmp",
    "ssl",
    "backups",
    "config",
    "config\apache",
    "config\apache\sites-enabled",
    "config\nginx",
    "config\nginx\sites-enabled",
    "etc\apache\sites-enabled",
    "etc\nginx\sites-enabled",
    "etc\postgresql"
)
foreach ($d in $commonDirs) {
    $path = Join-Path $StagingDir $d
    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
}

# 5. Create default loka.json
$defaultConfig = @'
{
  "Paths": {
    "RootPath": "C:\\Loka",
    "BinPath": "C:\\Loka\\bin",
    "RuntimesPath": "C:\\Loka\\bin",
    "EtcPath": "C:\\Loka\\etc",
    "DataPath": "C:\\Loka\\data",
    "ProjectsPath": "C:\\Loka\\projects",
    "LogsPath": "C:\\Loka\\logs",
    "ConfigPath": "C:\\Loka\\config",
    "BackupsPath": "C:\\Loka\\backups",
    "TmpPath": "C:\\Loka\\tmp",
    "TempPath": "C:\\Loka\\tmp",
    "SslPath": "C:\\Loka\\ssl",
    "AdditionalRuntimeSearchPaths": [
      "C:\\laragon\\bin",
      "C:\\xampp",
      "C:\\Program Files\\nodejs",
      "C:\\Program Files\\Git\\cmd"
    ],
    "AllowExternalRuntimes": false
  },
  "Services": {
    "ApachePort": 8080,
    "ApacheSslPort": 8443,
    "NginxPort": 80,
    "MySqlPort": 3306,
    "MariaDbPort": 3307,
    "PostgresPort": 5432,
    "PreferredWebServer": "Nginx",
    "PreferredDatabase": "MariaDB",
    "AutoStartApache": false,
    "AutoStartMySql": false,
    "AutoStartNginx": false,
    "AutoStartMariaDb": false,
    "AutoStartPostgres": false
  },
  "General": {
    "StartWithWindows": false,
    "MinimizeToTray": true,
    "CloseToTray": true,
    "Theme": "Light",
    "Language": "English",
    "IsFirstRunCompleted": false
  },
  "Php": {
    "ActiveVersion": "8.3",
    "AvailableVersions": [ "8.3", "8.4", "8.5" ],
    "EnabledExtensions": [ "curl", "fileinfo", "mbstring", "openssl", "pdo_mysql", "mysqli", "zip" ]
  },
  "Database": {
    "RootUser": "root",
    "RootPassword": "",
    "Host": "127.0.0.1",
    "PhpMyAdminUrl": "http://localhost/phpmyadmin"
  }
}
'@
Set-Content -Path (Join-Path $StagingDir "config\loka.json") -Value $defaultConfig -Encoding UTF8

# 6. Compress staging stack into stack_payload.zip
Write-Host "`nCompressing stack payload to $OutputZip..." -ForegroundColor Yellow
$outDir = Split-Path -Parent $OutputZip
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
if (Test-Path $OutputZip) { Remove-Item $OutputZip -Force }

[System.IO.Compression.ZipFile]::CreateFromDirectory($StagingDir, $OutputZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$sizeMb = [math]::Round((Get-Item $OutputZip).Length / 1MB, 2)
Write-Host "STACK PAYLOAD CREATED: $OutputZip ($sizeMb MB)" -ForegroundColor Green
