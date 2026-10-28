# Comprehensive Apache httpd.conf configuration for LOKA
# Configures modules (ssl, socache, proxy, proxy_fcgi, rewrite, headers), phpmyadmin, and ports

$confPath = "C:\Loka\bin\apache\conf\httpd.conf"
if (-not (Test-Path $confPath)) {
    Write-Host "Apache httpd.conf not found at $confPath"
    exit 1
}

$raw = [System.IO.File]::ReadAllText($confPath)

# 1. Normalize paths
$raw = $raw -replace 'C:/Apache24-64/htdocs', 'C:/Loka/projects'
$raw = $raw -replace 'c:/Apache24/htdocs', 'C:/Loka/projects'
$raw = $raw -replace 'C:/Apache24-64', 'C:/Loka/bin/apache'
$raw = $raw -replace 'c:/Apache24', 'C:/Loka/bin/apache'

# 2. Fix SRVROOT and ServerRoot
$raw = [System.Text.RegularExpressions.Regex]::Replace($raw, 'Define SRVROOT ".*"', 'Define SRVROOT "C:/Loka/bin/apache"')
$raw = [System.Text.RegularExpressions.Regex]::Replace($raw, 'ServerRoot ".*"', 'ServerRoot "C:/Loka/bin/apache"')

# 3. Enable essential modules
$modules = @(
    'ssl_module',
    'socache_shmcb_module',
    'proxy_module',
    'proxy_fcgi_module',
    'rewrite_module',
    'headers_module',
    'vhost_alias_module',
    'env_module',
    'setenvif_module',
    'dir_module',
    'mime_module'
)

foreach ($mod in $modules) {
    $pattern = '(?m)^\s*#\s*LoadModule\s+' + [System.Text.RegularExpressions.Regex]::Escape($mod) + '\s+(modules/\S+)'
    $replacement = 'LoadModule ' + $mod + ' $1'
    $raw = [System.Text.RegularExpressions.Regex]::Replace($raw, $pattern, $replacement)
}

# 4. Clean previous Listen directives and add exact listeners
$raw = [System.Text.RegularExpressions.Regex]::Replace($raw, '(?m)^\s*Listen\s+.*$\r?\n?', '')
$raw = [System.Text.RegularExpressions.Regex]::Replace($raw, '(?m)^\s*#\s*Listen\s+12\.34\.56\.78:80.*$\r?\n?', "#Listen 12.34.56.78:80`r`nListen 8080`r`nListen 8443`r`n")

# 5. ServerName
$raw = [System.Text.RegularExpressions.Regex]::Replace($raw, '(?m)^\s*#?\s*ServerName\s+[^:\r\n]+(:\d+)?', 'ServerName localhost:8080')

# 6. Ensure DocumentRoot points to C:/Loka/projects
$raw = $raw -replace 'DocumentRoot "C:/Loka/bin/apache/htdocs"', 'DocumentRoot "C:/Loka/projects"'
$raw = $raw -replace '<Directory "C:/Loka/bin/apache/htdocs">', '<Directory "C:/Loka/projects">'

# 7. Strip any trailing custom blocks added before
$cutoff = $raw.IndexOf("# Loka Projects Sites")
if ($cutoff -gt 0) {
    $raw = $raw.Substring(0, $cutoff)
}
$cutoff2 = $raw.IndexOf("# Loka Projects Directory")
if ($cutoff2 -gt 0) {
    $raw = $raw.Substring(0, $cutoff2)
}
$cutoff3 = $raw.IndexOf("Alias /phpmyadmin")
if ($cutoff3 -gt 0) {
    $raw = $raw.Substring(0, $cutoff3)
}

# 8. Append clean tail configuration
$tail = @"

# Loka Projects Directory & FastCGI Handler
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

# Loka Default VirtualHost (Catch-all for localhost & 127.0.0.1)
<VirtualHost *:80>
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

# Loka Projects Sites
IncludeOptional "C:/Loka/config/apache/sites-enabled/*.conf"
IncludeOptional "C:/Loka/etc/apache/sites-enabled/*.conf"
"@

$raw += $tail

[System.IO.File]::WriteAllText($confPath, $raw)
Write-Host "Updated Apache httpd.conf successfully."

# Mirror to C:\Loka\etc\apache\httpd.conf
$etcPath = "C:\Loka\etc\apache\httpd.conf"
if (-not (Test-Path "C:\Loka\etc\apache")) { New-Item -ItemType Directory -Path "C:\Loka\etc\apache" -Force | Out-Null }
Copy-Item $confPath $etcPath -Force

# Ensure junction exists
if (-not (Test-Path "C:\Loka\projects\phpmyadmin")) {
    New-Item -ItemType Junction -Path "C:\Loka\projects\phpmyadmin" -Target "C:\Loka\bin\phpmyadmin" | Out-Null
}

# Test syntax
& "C:\Loka\bin\apache\bin\httpd.exe" -d "C:/Loka/bin/apache" -t
