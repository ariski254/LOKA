Copy-Item "config\components.json" "C:\Loka\config\components.json" -Force

$iniTemplate = @'
[PHP]
engine = On
precision = 14
output_buffering = 4096
memory_limit = 512M
error_reporting = E_ALL & ~E_DEPRECATED & ~E_STRICT
display_errors = On
display_startup_errors = On
post_max_size = 128M
file_uploads = On
upload_max_filesize = 128M
max_file_uploads = 20
allow_url_fopen = On
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
'@

$versions = @("8.4", "8.5")
foreach ($v in $versions) {
    $vDir = "C:\Loka\bin\php\$v"
    if (Test-Path $vDir) {
        $iniPath = "$vDir\php.ini"
        [System.IO.File]::WriteAllText($iniPath, $iniTemplate)
    }
}

if (-not (Test-Path "C:\Loka\bin\php\ext")) { New-Item -ItemType Directory -Path "C:\Loka\bin\php\ext" -Force | Out-Null }
Copy-Item "C:\Loka\bin\php\8.4\ext\*" "C:\Loka\bin\php\ext\" -Recurse -Force
Copy-Item "C:\Loka\bin\php\8.4\php.exe" "C:\Loka\bin\php\php.exe" -Force
Copy-Item "C:\Loka\bin\php\8.4\php8.dll" "C:\Loka\bin\php\php8.dll" -Force
Copy-Item "C:\Loka\bin\php\8.4\php.ini" "C:\Loka\bin\php\php.ini" -Force

$composerDir = "C:\Loka\bin\composer"
if (Test-Path $composerDir) {
    $batContent = "@echo off`r`n`"%~dp0..\php\8.4\php.exe`" `"%~dp0composer.phar`" %*`r`n"
    [System.IO.File]::WriteAllText("$composerDir\composer.bat", $batContent)
}

Write-Host "Updated PHP configuration, composer.bat, and mirrors successfully."
