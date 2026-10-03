@echo off
title Build ADBLogin.Core
cls

echo ========================================================
echo         BIEN DICH THU VIEN LOI ADBLOGIN.CORE
echo ========================================================
echo.

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

set "NETSTANDARD=C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll"

if not exist "%CSC%" (
    echo [LOI] Khong tim thay trinh bien dich csc.exe
    pause
    exit /b 1
)

echo [*] Dang bien dich ADBLogin.Core.dll...
"%CSC%" /nologo /target:library /out:src\ADBLogin.Core.dll /r:Newtonsoft.Json.dll,WebDriver.dll,"%NETSTANDARD%" src\Core\Models\*.cs src\Core\Services\*.cs

if %errorlevel% equ 0 (
    echo.
    echo [THANH CONG] Da tao tep: src\ADBLogin.Core.dll
) else (
    echo.
    echo [THAT BAI] Co loi xay ra trong qua trinh bien dich.
)

echo.
pause
