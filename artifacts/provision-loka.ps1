param(
    [string]$TargetDir = "C:\Loka",
    [string]$CacheDir = "$PSScriptRoot\cache"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   Provisioning LOKA Development Stack into $TargetDir    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Ensure Directory Structure
$directories = @(
    "$TargetDir\bin",
    "$TargetDir\bin\php\8.4",
    "$TargetDir\bin\php\8.5",
    "$TargetDir\bin\nginx",
    "$TargetDir\bin\apache",
    "$TargetDir\bin\mariadb",
    "$TargetDir\bin\mysql",
    "$TargetDir\bin\postgresql",
    "$TargetDir\bin\nodejs",
    "$TargetDir\bin\git",
    "$TargetDir\bin\composer",
    "$TargetDir\bin\phpmyadmin",
    "$TargetDir\etc",
    "$TargetDir\etc\nginx",
    "$TargetDir\etc\apache",
    "$TargetDir\etc\mariadb",
    "$TargetDir\etc\mysql",
    "$TargetDir\data",
    "$TargetDir\data\mariadb",
    "$TargetDir\data\mysql",
    "$TargetDir\data\postgresql",
    "$TargetDir\projects",
    "$TargetDir\logs",
    "$TargetDir\logs\nginx",
    "$TargetDir\logs\apache",
    "$TargetDir\logs\php",
    "$TargetDir\logs\mariadb",
    "$TargetDir\logs\mysql",
    "$TargetDir\logs\postgresql",
    "$TargetDir\config",
    "$TargetDir\config\ssl",
    "$TargetDir\backups",
    "$TargetDir\tmp"
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

function Extract-ZipWithStrip {
    param(
        [string]$ZipFile,
        [string]$Destination,
        [string]$CustomPrefix = $null
    )

    Write-Host "Extracting $([System.IO.Path]::GetFileName($ZipFile)) -> $Destination..." -ForegroundColor Yellow
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipFile)

    try {
        $prefix = $null
        if ($CustomPrefix) {
            $prefix = $CustomPrefix.TrimEnd('/', '\') + "/"
        } else {
            # Auto detect single top-level folder
            $topDirs = @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) } | ForEach-Object {
                $clean = $_.FullName -replace '\\', '/'
                $idx = $clean.IndexOf('/')
                if ($idx -gt 0) { $clean.Substring(0, $idx) } else { $null }
            } | Where-Object { $_ } | Select-Object -Unique)

            if ($topDirs.Count -eq 1) {
                $prefix = $topDirs[0] + "/"
            }
        }

        foreach ($entry in $archive.Entries) {
            $rel = $entry.FullName -replace '\\', '/'
            if ($prefix) {
                if ($rel.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $rel = $rel.Substring($prefix.Length)
                } elseif ($CustomPrefix) {
                    continue
                }
            }

            if ([string]::IsNullOrWhiteSpace($rel)) { continue }

            # Skip docs, symbols, includes to minimize disk footprint
            if ($rel -match '^(doc|docs|manual|symbols|include)/' -or $rel.EndsWith('.pdb', [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $target = Join-Path $Destination ($rel -replace '/', '\')
            if ([string]::IsNullOrEmpty($entry.Name)) {
                if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }
            } else {
                $parent = [System.IO.Path]::GetDirectoryName($target)
                if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            }
        }
    } finally {
        $archive.Dispose()
    }
    Write-Host "Extracted successfully." -ForegroundColor Green
}

# 2. Extract Components
# Nginx
if (-not (Test-Path "$TargetDir\bin\nginx\nginx.exe")) {
    Extract-ZipWithStrip -ZipFile "$CacheDir\nginx.zip" -Destination "$TargetDir\bin\nginx"
}

# Apache
if (-not (Test-Path "$TargetDir\bin\apache\bin\httpd.exe")) {
    Extract-ZipWithStrip -ZipFile "$CacheDir\apache.zip" -Destination "$TargetDir\bin\apache" -CustomPrefix "Apache24"
}

# PHP 8.4
if (-not (Test-Path "$TargetDir\bin\php\8.4\php.exe")) {
    Extract-ZipWithStrip -ZipFile "$CacheDir\php-8.4.zip" -Destination "$TargetDir\bin\php\8.4"
}

# PHP 8.5
if (-not (Test-Path "$TargetDir\bin\php\8.5\php.exe")) {
    Extract-ZipWithStrip -ZipFile "$CacheDir\php-8.5.zip" -Destination "$TargetDir\bin\php\8.5"
}

# MariaDB
if (-not (Test-Path "$TargetDir\bin\mariadb\bin\mariadbd.exe")) {
    $mariaZip = if (Test-Path "$CacheDir\mariadb-11.8.zip") { "$CacheDir\mariadb-11.8.zip" } else { "$CacheDir\mariadb.zip" }
    Extract-ZipWithStrip -ZipFile $mariaZip -Destination "$TargetDir\bin\mariadb"
}

# MySQL
if (-not (Test-Path "$TargetDir\bin\mysql\bin\mysqld.exe")) {
    $mysqlZip = if (Test-Path "$CacheDir\mysql-8.4.zip") { "$CacheDir\mysql-8.4.zip" } else { "$CacheDir\mysql.zip" }
    Extract-ZipWithStrip -ZipFile $mysqlZip -Destination "$TargetDir\bin\mysql"
}

# PostgreSQL
if (-not (Test-Path "$TargetDir\bin\postgresql\bin\postgres.exe")) {
    $pgZip = if (Test-Path "$CacheDir\postgresql-17.zip") { "$CacheDir\postgresql-17.zip" } else { "$CacheDir\postgresql.zip" }
    Extract-ZipWithStrip -ZipFile $pgZip -Destination "$TargetDir\bin\postgresql" -CustomPrefix "pgsql"
}

# Node.js
if (-not (Test-Path "$TargetDir\bin\nodejs\node.exe")) {
    $nodeZip = if (Test-Path "$CacheDir\node-v22.zip") { "$CacheDir\node-v22.zip" } else { "$CacheDir\nodejs.zip" }
    Extract-ZipWithStrip -ZipFile $nodeZip -Destination "$TargetDir\bin\nodejs"
}

# Git
if (-not (Test-Path "$TargetDir\bin\git\cmd\git.exe")) {
    $gitZip = if (Test-Path "$CacheDir\mingit.zip") { "$CacheDir\mingit.zip" } else { "$CacheDir\git.zip" }
    Extract-ZipWithStrip -ZipFile $gitZip -Destination "$TargetDir\bin\git"
}

# Composer
if (-not (Test-Path "$TargetDir\bin\composer\composer.phar")) {
    Copy-Item "$CacheDir\composer.phar" "$TargetDir\bin\composer\composer.phar" -Force
}

# phpMyAdmin
if (-not (Test-Path "$TargetDir\bin\phpmyadmin\index.php")) {
    Extract-ZipWithStrip -ZipFile "$CacheDir\phpmyadmin.zip" -Destination "$TargetDir\bin\phpmyadmin"
}

# 3. Post-Extraction Configuration
Write-Host "`nConfiguring PHP 8.4, PHP 8.5, Composer, MariaDB, MySQL, and Web Servers..." -ForegroundColor Cyan

# PHP.ini template
$phpIniContent = @"
[PHP]
engine = On
short_open_tag = Off
precision = 14
output_buffering = 4096
zlib.output_compression = Off
implicit_flush = Off
serialize_precision = -1
zend.enable_gc = On
expose_php = Off
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
html_errors = On
variables_order = "GPCS"
request_order = "GP"
register_argc_argv = Off
auto_globals_jit = On
post_max_size = 128M
default_mimetype = "text/html"
default_charset = "UTF-8"
file_uploads = On
upload_max_filesize = 128M
max_file_uploads = 50
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
extension=pdo_pgsql
extension=pgsql
extension=pdo_sqlite
extension=sqlite3

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
"@

Set-Content -Path "$TargetDir\bin\php\8.4\php.ini" -Value $phpIniContent -Encoding ASCII
Set-Content -Path "$TargetDir\bin\php\8.5\php.ini" -Value $phpIniContent -Encoding ASCII

# Mirror PHP 8.4 to root bin\php
Get-ChildItem -Path "$TargetDir\bin\php\8.4" -Filter "*.exe" | Copy-Item -Destination "$TargetDir\bin\php" -Force
Get-ChildItem -Path "$TargetDir\bin\php\8.4" -Filter "*.dll" | Copy-Item -Destination "$TargetDir\bin\php" -Force
Copy-Item "$TargetDir\bin\php\8.4\php.ini" "$TargetDir\bin\php\php.ini" -Force
if (-not (Test-Path "$TargetDir\bin\php\ext")) { New-Item -ItemType Directory -Path "$TargetDir\bin\php\ext" -Force | Out-Null }
Copy-Item "$TargetDir\bin\php\8.4\ext\*" "$TargetDir\bin\php\ext\" -Recurse -Force

# Composer wrapper script
$composerBat = @"
@echo off
"%~dp0..\php\8.4\php.exe" "%~dp0composer.phar" %*
"@
Set-Content -Path "$TargetDir\bin\composer\composer.bat" -Value $composerBat -Encoding ASCII

# MariaDB my.ini
$mariaIni = @"
[mysqld]
port = 3307
basedir = "$TargetDir/bin/mariadb"
datadir = "$TargetDir/data/mariadb"
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
Set-Content -Path "$TargetDir\etc\mariadb\my.ini" -Value $mariaIni -Encoding ASCII

# MariaDB data initialization
if (-not (Test-Path "$TargetDir\data\mariadb\mysql")) {
    Write-Host "Initializing MariaDB system database..." -ForegroundColor Yellow
    $installDb = "$TargetDir\bin\mariadb\bin\mariadb-install-db.exe"
    if (-not (Test-Path $installDb)) { $installDb = "$TargetDir\bin\mariadb\bin\mysql_install_db.exe" }
    if (Test-Path $installDb) {
        & $installDb "--datadir=$TargetDir\data\mariadb" "--default-user" | Out-Null
    }
}

# MySQL my.ini
$mysqlIni = @"
[mysqld]
port = 3306
basedir = "$TargetDir/bin/mysql"
datadir = "$TargetDir/data/mysql"
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
default_storage_engine = InnoDB
max_allowed_packet = 64M
bind-address = 127.0.0.1
sql_mode = "STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION"

[client]
port = 3306
default-character-set = utf8mb4
"@
Set-Content -Path "$TargetDir\etc\mysql\my.ini" -Value $mysqlIni -Encoding ASCII

# MySQL data initialization
if (-not (Test-Path "$TargetDir\data\mysql\mysql") -and (Test-Path "$TargetDir\bin\mysql\bin\mysqld.exe")) {
    Write-Host "Initializing MySQL system database (insecure mode)..." -ForegroundColor Yellow
    & "$TargetDir\bin\mysql\bin\mysqld.exe" --initialize-insecure "--datadir=$TargetDir\data\mysql" "--basedir=$TargetDir\bin\mysql" | Out-Null
}

# PostgreSQL initialization
if (-not (Test-Path "$TargetDir\data\postgresql\PG_VERSION") -and (Test-Path "$TargetDir\bin\postgresql\bin\initdb.exe")) {
    Write-Host "Initializing PostgreSQL cluster..." -ForegroundColor Yellow
    & "$TargetDir\bin\postgresql\bin\initdb.exe" -D "$TargetDir\data\postgresql" -U postgres -E UTF8 --locale=C -A trust | Out-Null
}

# Nginx config
$nginxConf = @"
worker_processes  1;
events {
    worker_connections  1024;
}
http {
    include       mime.types;
    default_type  application/octet-stream;
    sendfile        on;
    keepalive_timeout  65;

    server {
        listen       80;
        server_name  localhost 127.0.0.1;
        root   $($TargetDir.Replace('\', '/'))/projects;
        index  index.php index.html index.htm;

        location / {
            try_files `$uri `$uri/ /index.php`$is_args`$args;
        }

        location /phpmyadmin {
            alias "$($TargetDir.Replace('\', '/'))/bin/phpmyadmin";
            index index.php index.html index.htm;

            location ~ \.php$ {
                fastcgi_pass   127.0.0.1:9000;
                fastcgi_index  index.php;
                fastcgi_param  SCRIPT_FILENAME  `$request_filename;
                include        fastcgi_params;
            }
        }

        location ~ \.php$ {
            fastcgi_pass   127.0.0.1:9000;
            fastcgi_index  index.php;
            fastcgi_param  SCRIPT_FILENAME  `$document_root`$fastcgi_script_name;
            include        fastcgi_params;
        }
    }

    include "$($TargetDir.Replace('\', '/'))/config/nginx/sites-enabled/*.conf";
    include "$($TargetDir.Replace('\', '/'))/etc/nginx/sites-enabled/*.conf";
}
"@
Set-Content -Path "$TargetDir\etc\nginx\nginx.conf" -Value $nginxConf -Encoding ASCII
if (Test-Path "$TargetDir\bin\nginx\conf") {
    Set-Content -Path "$TargetDir\bin\nginx\conf\nginx.conf" -Value $nginxConf -Encoding ASCII
    Copy-Item "$TargetDir\bin\nginx\conf\mime.types" "$TargetDir\etc\nginx\mime.types" -Force -ErrorAction SilentlyContinue
    Copy-Item "$TargetDir\bin\nginx\conf\fastcgi_params" "$TargetDir\etc\nginx\fastcgi_params" -Force -ErrorAction SilentlyContinue
}

# Apache httpd.conf
$apacheConf = @"
ServerRoot "$TargetDir/bin/apache"
Listen 8080
ServerName localhost:8080

LoadModule authz_core_module modules/mod_authz_core.so
LoadModule log_config_module modules/mod_log_config.so
LoadModule mime_module modules/mod_mime.so
LoadModule dir_module modules/mod_dir.so
LoadModule rewrite_module modules/mod_rewrite.so
LoadModule proxy_module modules/mod_proxy.so
LoadModule proxy_fcgi_module modules/mod_proxy_fcgi.so
LoadModule alias_module modules/mod_alias.so

DocumentRoot "$TargetDir/projects"
<Directory "$TargetDir/projects">
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

# Loka Default VirtualHost (Catch-all for localhost & 127.0.0.1)
<VirtualHost *:8080>
    ServerName localhost
    ServerAlias 127.0.0.1
    DocumentRoot "$TargetDir/projects"

    <Directory "$TargetDir/projects">
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

    Alias /phpmyadmin "$TargetDir/bin/phpmyadmin"
    <Directory "$TargetDir/bin/phpmyadmin">
        Options Indexes FollowSymLinks MultiViews
        AllowOverride All
        Require all granted
        DirectoryIndex index.php index.html index.htm
        <IfModule mod_proxy_fcgi.c>
            ProxyFCGISetEnvIf "reqenv('SCRIPT_NAME') =~ m#^/phpmyadmin/(.*)$#" SCRIPT_FILENAME "$TargetDir/bin/phpmyadmin/$1"
            <FilesMatch \.php$>
                SetHandler "proxy:fcgi://127.0.0.1:9000/"
            </FilesMatch>
        </IfModule>
    </Directory>

    ErrorLog "$TargetDir/logs/apache_default_error.log"
    CustomLog "$TargetDir/logs/apache_default_access.log" common
</VirtualHost>

# Loka Projects Sites
IncludeOptional "$TargetDir/config/apache/sites-enabled/*.conf"
IncludeOptional "$TargetDir/etc/apache/sites-enabled/*.conf"

DirectoryIndex index.php index.html
"@
Set-Content -Path "$TargetDir\etc\apache\httpd.conf" -Value $apacheConf -Encoding ASCII

# phpMyAdmin configuration
$pmaConfig = @"
<?php
`$i = 0;

// MariaDB
`$i++;
`$cfg['Servers'][`$i]['auth_type'] = 'cookie';
`$cfg['Servers'][`$i]['host'] = '127.0.0.1';
`$cfg['Servers'][`$i]['port'] = '3307';
`$cfg['Servers'][`$i]['user'] = 'root';
`$cfg['Servers'][`$i]['password'] = '';
`$cfg['Servers'][`$i]['verbose'] = 'MariaDB (3307)';

// MySQL
`$i++;
`$cfg['Servers'][`$i]['auth_type'] = 'cookie';
`$cfg['Servers'][`$i]['host'] = '127.0.0.1';
`$cfg['Servers'][`$i]['port'] = '3306';
`$cfg['Servers'][`$i]['user'] = 'root';
`$cfg['Servers'][`$i]['password'] = '';
`$cfg['Servers'][`$i]['verbose'] = 'MySQL (3306)';

`$cfg['UploadDir'] = '';
`$cfg['SaveDir'] = '';
"@
Set-Content -Path "$TargetDir\bin\phpmyadmin\config.inc.php" -Value $pmaConfig -Encoding ASCII

# Loka config JSON
$lokaJson = @"
{
  "Paths": {
    "RootPath": "$($TargetDir.Replace('\', '\\'))",
    "BinPath": "$($TargetDir.Replace('\', '\\'))\\bin",
    "RuntimesPath": "$($TargetDir.Replace('\', '\\'))\\bin",
    "EtcPath": "$($TargetDir.Replace('\', '\\'))\\etc",
    "DataPath": "$($TargetDir.Replace('\', '\\'))\\data",
    "ProjectsPath": "$($TargetDir.Replace('\', '\\'))\\projects",
    "LogsPath": "$($TargetDir.Replace('\', '\\'))\\logs",
    "ConfigPath": "$($TargetDir.Replace('\', '\\'))\\config",
    "BackupsPath": "$($TargetDir.Replace('\', '\\'))\\backups",
    "TmpPath": "$($TargetDir.Replace('\', '\\'))\\tmp",
    "TempPath": "$($TargetDir.Replace('\', '\\'))\\tmp",
    "SslPath": "$($TargetDir.Replace('\', '\\'))\\config\\ssl",
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
    "IsFirstRunCompleted": true
  },
  "Php": {
    "ActiveVersion": "8.4",
    "AvailableVersions": [ "8.3", "8.4", "8.5" ],
    "EnabledExtensions": [ "curl", "fileinfo", "mbstring", "openssl", "pdo_mysql", "mysqli", "zip", "pdo_pgsql", "pgsql", "pdo_sqlite", "sqlite3" ]
  },
  "Database": {
    "RootUser": "root",
    "RootPassword": "",
    "Host": "127.0.0.1",
    "PhpMyAdminUrl": "http://localhost/phpmyadmin"
  }
}
"@
Set-Content -Path "$TargetDir\config\loka.json" -Value $lokaJson -Encoding UTF8

# Central Component Manifest (Section 33)
$sourceManifest = Join-Path (Split-Path $PSScriptRoot -Parent) "config\components.json"
if (Test-Path $sourceManifest) {
    Copy-Item $sourceManifest "$TargetDir\config\components.json" -Force
} elseif (Test-Path "config\components.json") {
    Copy-Item "config\components.json" "$TargetDir\config\components.json" -Force
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "   PROVISIONING COMPLETED SUCCESSFULLY INTO $TargetDir   " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
