[CmdletBinding()]
param(
    [string]$SetupPath = "C:\Users\LENOVO\Herd\Loka\release\Loka-Setup.exe",
    [switch]$Silent = $true
)

$ErrorActionPreference = "Stop"

Write-Output "=========================================="
Write-Output "   LOKA ELEVATED INSTALLER LAUNCHER       "
Write-Output "=========================================="

# 1. Pastikan path executable valid dan file benar-benar tersedia
if (-not (Test-Path $SetupPath)) {
    Write-Error "Executable installer tidak ditemukan di path: '$SetupPath'. Harap jalankan 'build-installer.ps1' terlebih dahulu."
    exit 2 # ERROR_FILE_NOT_FOUND
}

$setupItem = Get-Item $SetupPath
$workingDir = $setupItem.DirectoryName
Write-Output "Executable terverifikasi : $($setupItem.FullName) ($([math]::Round($setupItem.Length / 1MB, 2)) MB)"
Write-Output "Working Directory        : $workingDir"

# 2. Periksa konteks sesi (interactive vs Session 0 / background service)
$isInteractive = [System.Environment]::UserInteractive
$currentPid = [System.Diagnostics.Process]::GetCurrentProcess().Id
$currentSessionId = [System.Diagnostics.Process]::GetCurrentProcess().SessionId
Write-Output "Sesi Eksekusi            : SessionId $currentSessionId (UserInteractive: $isInteractive)"

if ($currentSessionId -eq 0 -or -not $isInteractive) {
    Write-Error "Installer tidak dapat menampilkan dialog konfirmasi UAC di Session 0 atau sesi layanan non-interaktif. Harap jalankan dari terminal pengguna aktif (Session 1)."
    exit 1058 # ERROR_SERVICE_CANNOT_ACCEPT_CTRL
}

# 3. Periksa apakah proses saat ini sudah berjalan sebagai Administrator
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
$isElevated = $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
Write-Output "Caller Token             : $($identity.Name), Administrator: $isElevated"

# 4. Siapkan argumen peluncuran
$arguments = if ($Silent) { "--silent" } else { "" }

if ($isElevated) {
    # 5. Sudah elevated: jalankan langsung tanpa elevasi kedua (mencegah loop/elevasi berulang)
    Write-Output "Status: Caller sudah memiliki hak Administrator. Menjalankan installer langsung..."
    
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $setupItem.FullName
    $psi.Arguments = $arguments
    $psi.WorkingDirectory = $workingDir
    $psi.UseShellExecute = $false

    try {
        $proc = [System.Diagnostics.Process]::Start($psi)
        if ($null -ne $proc) {
            $proc.WaitForExit()
            Write-Output "Installer selesai dengan ExitCode: $($proc.ExitCode)"
            exit $proc.ExitCode
        } else {
            Write-Error "Gagal memulai proses installer."
            exit 1
        }
    } catch {
        Write-Error "Gagal menjalankan installer elevated: $($_.Exception.Message)"
        exit 1
    }
} else {
    # 6. Belum elevated: gunakan mekanisme Windows Shell 'runas' untuk meminta elevasi UAC
    Write-Output "Status: Caller berjalan sebagai standard user (non-elevated)."
    Write-Output "Meminta konfirmasi UAC Administrator ke Windows via ShellExecute 'runas'..."
    Write-Output "CATATAN: Jika 'PromptOnSecureDesktop' dinonaktifkan, periksa ikon perisai (Shield) yang berkedip pada Taskbar Windows dan klik 'Yes'."

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $setupItem.FullName
    $psi.Arguments = $arguments
    $psi.WorkingDirectory = $workingDir
    $psi.UseShellExecute = $true
    $psi.Verb = "runas"

    try {
        $proc = [System.Diagnostics.Process]::Start($psi)
        if ($null -ne $proc) {
            Write-Output "Proses elevated berhasil dimulai (PID: $($proc.Id)). Menunggu instalasi selesai..."
            $proc.WaitForExit()
            Write-Output "Proses elevated selesai dengan ExitCode: $($proc.ExitCode)"
            exit $proc.ExitCode
        } else {
            Write-Output "Proses elevated dimulai via Windows Shell (handle dikelola oleh OS)."
            exit 0
        }
    }
    catch [System.ComponentModel.Win32Exception] {
        $win32Ex = $_.Exception
        if ($win32Ex.NativeErrorCode -eq 1223) {
            # ERROR_CANCELLED (Pengguna menekan 'No' atau menutup dialog UAC)
            Write-Warning "Permintaan elevasi UAC dibatalkan oleh pengguna (Win32 Error 1223: ERROR_CANCELLED)."
            exit 1223
        } elseif ($win32Ex.NativeErrorCode -eq 5) {
            # ERROR_ACCESS_DENIED
            Write-Error "Akses ditolak saat meminta elevasi UAC Administrator (Win32 Error 5: ERROR_ACCESS_DENIED)."
            exit 5
        } elseif ($win32Ex.NativeErrorCode -eq 740) {
            # ERROR_ELEVATION_REQUIRED
            Write-Error "Operasi membutuhkan elevasi hak akses Administrator (Win32 Error 740: ERROR_ELEVATION_REQUIRED)."
            exit 740
        } else {
            Write-Error "Win32Exception saat meminta elevasi UAC (Error $($win32Ex.NativeErrorCode)): $($win32Ex.Message)"
            exit $win32Ex.NativeErrorCode
        }
    }
    catch {
        Write-Error "Terjadi kesalahan tidak terduga saat meminta elevasi: $($_.Exception.Message)"
        exit 1
    }
}
