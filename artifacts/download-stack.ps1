param(
    [string]$CacheDir = "$PSScriptRoot\cache"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $CacheDir)) {
    New-Item -ItemType Directory -Path $CacheDir -Force | Out-Null
}

$downloads = [ordered]@{
    "php-8.3.zip"      = @{ Url = "https://windows.php.net/downloads/releases/archives/php-8.3.36-nts-Win32-vs16-x64.zip"; Hash = "3C9242622C7402B47C38D2EC2ED779CEF3A290FD6F2BDE6C35F9C482699E7900" }
    "php-8.4.zip"      = @{ Url = "https://windows.php.net/downloads/releases/archives/php-8.4.26-nts-Win32-vs17-x64.zip"; Hash = "DA68394F9193B7F6B89D0C76861A4034AE10EFEE7FD55A7255D8118C2ACF70D7" }
    "php-8.5.zip"      = @{ Url = "https://windows.php.net/downloads/releases/archives/php-8.5.11-nts-Win32-vs17-x64.zip"; Hash = "0EA96E0D2B9B737A6036F05CF4E95C49313FAA6D0F27BD97EDB2742503F0C043" }
    "nginx.zip"        = @{ Url = "https://nginx.org/download/nginx-1.28.3.zip"; Hash = "AAD7BF75D669ECE7671688BFDF35F1093D6A30D2E62469405F4D55D8D82D5FD3" }
    "apache.zip"       = @{ Url = "https://www.apachelounge.com/download/VS17/binaries/httpd-2.4.66-251206-Win64-VS17.zip"; Hash = "2CD1F349B6705E43E784E876A233EEB1A859FA6B8ABC693A91F87B35A368F7F9" }
    "mariadb-11.8.zip" = @{ Url = "https://archive.mariadb.org/mariadb-11.8.9/winx64-packages/mariadb-11.8.9-winx64.zip"; Hash = "830C46727D9278EAE212AE3ECA44EEB9E71B2A68704E95F344A64FBA7B1963F5" }
    "mysql-8.4.zip"    = @{ Url = "https://cdn.mysql.com/Downloads/MySQL-8.4/mysql-8.4.0-winx64.zip"; Hash = "FB1F0C6CCE229BE4B3208BEC8A605576BB47EABB00512BCCBCC4396AF332C25C" }
    "postgresql-17.zip"= @{ Url = "https://get.enterprisedb.com/postgresql/postgresql-17.4-1-windows-x64-binaries.zip"; Hash = "80BB49E80DF0100DD52E7B3C647896F8D8CFDE760B6473E5180819C8DF4B23C4" }
    "node-v22.zip"     = @{ Url = "https://nodejs.org/dist/v22.23.3/node-v22.23.3-win-x64.zip"; Hash = "2B0FF57B049CDA1BBCEA2240EEC20467018713C1EFE1F7360C2681859B90ED71" }
    "mingit.zip"       = @{ Url = "https://github.com/git-for-windows/git/releases/download/v2.55.0.windows.5/MinGit-2.55.0.5-64-bit.zip"; Hash = "56D7B226B7693196CFC71FEF26568F536C4A021AB6C37FF2DB4287BED908E96E" }
    "composer.phar"    = @{ Url = "https://getcomposer.org/download/latest-stable/composer.phar"; Hash = "7A2D379D5B8FFDAA028580EF26494C36D2FEEF4B178D3DD1473A4DBC5E17C8D6" }
    "phpmyadmin.zip"   = @{ Url = "https://files.phpmyadmin.net/phpMyAdmin/5.2.2/phpMyAdmin-5.2.2-all-languages.zip"; Hash = "6B99534F72FFB1D7275F50D23CA4141E1495C97D7CADB73A41D6DC580ED5CE29" }
}

foreach ($item in $downloads.GetEnumerator()) {
    $name = $item.Key
    $meta = $item.Value
    $url = $meta.Url
    $expectedHash = $meta.Hash
    $target = Join-Path $CacheDir $name

    $needsDownload = $true
    if ((Test-Path $target) -and (Get-Item $target).Length -ge 1000) {
        $actualHash = (Get-FileHash -Path $target -Algorithm SHA256).Hash
        if ($actualHash -eq $expectedHash) {
            $needsDownload = $false
            $size = [math]::Round((Get-Item $target).Length / 1MB, 2)
            Write-Host "Cached $name verified with SHA-256 ($size MB)" -ForegroundColor DarkGray
        } else {
            Write-Warning "Cached $name hash mismatch (Expected: $expectedHash, Found: $actualHash). Re-downloading..."
            Remove-Item $target -Force -ErrorAction SilentlyContinue
        }
    }

    if ($needsDownload) {
        Write-Host "Downloading $name from $url..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $url -OutFile $target -UseBasicParsing
        $actualHash = (Get-FileHash -Path $target -Algorithm SHA256).Hash
        if ($actualHash -ne $expectedHash) {
            Remove-Item $target -Force -ErrorAction SilentlyContinue
            throw "Downloaded $name failed SHA-256 verification! Expected: $expectedHash, Actual: $actualHash"
        }
        $size = [math]::Round((Get-Item $target).Length / 1MB, 2)
        Write-Host "Downloaded and verified $name ($size MB)" -ForegroundColor Green
    }
}

Write-Host "ALL STACK PACKAGES READY AND VERIFIED IN $CacheDir" -ForegroundColor Green
