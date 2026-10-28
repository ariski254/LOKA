$definedKeys = [System.Collections.Generic.HashSet[string]]::new()

# Load all defined keys from Colors.xaml, Styles.xaml, App.xaml
$resourceFiles = Get-ChildItem -Path "Loka.App\Themes", "Loka.App\App.xaml" -Filter "*.xaml" -Recurse
foreach ($rf in $resourceFiles) {
    $content = Get-Content $rf.FullName -Raw
    $matches = [regex]::Matches($content, 'x:Key="([^"]+)"')
    foreach ($m in $matches) {
        [void]$definedKeys.Add($m.Groups[1].Value)
    }
}

# Standard WPF built-in converters or system keys
[void]$definedKeys.Add("BoolToVis")

Write-Host "Total defined resource keys in themes: $($definedKeys.Count)"

$allXaml = Get-ChildItem -Path "Loka.App" -Filter "*.xaml" -Recurse
$missingList = @()

foreach ($xf in $allXaml) {
    $content = Get-Content $xf.FullName -Raw
    $localKeys = [System.Collections.Generic.HashSet[string]]::new($definedKeys)
    $localMatches = [regex]::Matches($content, 'x:Key="([^"]+)"')
    foreach ($m in $localMatches) {
        [void]$localKeys.Add($m.Groups[1].Value)
    }

    $usages = [regex]::Matches($content, 'StaticResource\s+([^}\s]+)')
    foreach ($u in $usages) {
        $key = $u.Groups[1].Value
        # ignore x:Type
        if ($key -match '\{x:Type') { continue }
        if (-not $localKeys.Contains($key)) {
            $missingList += "$($xf.Name) -> $key"
        }
    }
}

if ($missingList.Count -eq 0) {
    Write-Host "ALL STATIC RESOURCES IN LOKA.APP ARE VALID!" -ForegroundColor Green
} else {
    Write-Host "MISSING STATIC RESOURCES IN LOKA.APP:" -ForegroundColor Red
    $missingList | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
}
