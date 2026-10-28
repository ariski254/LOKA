@echo off
title Loka - Trust Root Certificate Authority
echo ===============================================================
echo   Loka Local Development - Trust Root Certificate Authority (CA)
echo ===============================================================
echo.
echo Registering Loka Root CA certificate into Windows Trust Store...
echo.
echo [ATTENTION] If a "Security Warning" prompt appears from Windows,
echo please click "Yes" so that browsers (Chrome / Edge) trust
echo all local .test websites with secure HTTPS connections (green lock).
echo.

certutil -user -addstore Root "C:\Loka\config\ssl\ca\loka-ca.crt"

echo.
if %ERRORLEVEL% EQU 0 (
    echo ===============================================================
    echo [SUCCESS] Loka Root CA is now trusted by Windows!
    echo.
    echo Please restart your browser (Chrome / Edge),
    echo then visit your local site: https://lara.test
    echo ===============================================================
) else (
    echo [FAILED/CANCELLED] Certificate was not installed or was rejected by user.
)
echo.
pause
