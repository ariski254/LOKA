# Loka Hosts Synchronization Script
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

# Grant modify permissions to Users on hosts file so future writes from Loka app succeed silently
try {
    icacls "$env:SystemRoot\System32\drivers\etc\hosts" /grant "BUILTIN\Users:(M)" | Out-Null
} catch {}

$hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
$content = [System.IO.File]::ReadAllText($hostsPath)
$begin = "# --- LOKA MANAGED HOSTS BEGIN ---"
$end = "# --- LOKA MANAGED HOSTS END ---"

$projectsDir = "C:\Loka\projects"
$entries = [System.Collections.Generic.List[string]]::new()
if (Test-Path $projectsDir) {
    Get-ChildItem -Path $projectsDir -Directory | ForEach-Object {
        $dom = ($_.Name.ToLower() -replace '\s+', '-') + ".test"
        $line = "127.0.0.1 $dom"
        if (-not $entries.Contains($line)) {
            $entries.Add($line)
        }
    }
}



$block = $begin + "`r`n" + ($entries -join "`r`n") + "`r`n" + $end

$beginEscaped = [regex]::Escape($begin)
$endEscaped = [regex]::Escape($end)
$pattern = "(?s)" + $beginEscaped + ".*?" + $endEscaped

if ($content -match $pattern) {
    $content = [regex]::Replace($content, $pattern, $block)
} else {
    $content = $content.TrimEnd() + "`r`n`r`n" + $block + "`r`n"
}

[System.IO.File]::WriteAllText($hostsPath, $content)
ipconfig /flushdns | Out-Null
Write-Host "Hosts synchronization completed successfully!" -ForegroundColor Green
Write-Host "Configured domains:" -ForegroundColor Cyan
foreach ($e in $entries) { Write-Host " - $e" -ForegroundColor Yellow }
Start-Sleep -Seconds 2
