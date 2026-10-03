@echo off
title Build ADBLogin Executable
cls

echo ========================================================
echo         BIEN DICH UNG DUNG ADBLOGIN (FILE .EXE)
echo ========================================================
echo.

:: Dong tien trinh cu neu dang chay
taskkill /f /im ADBLogin.exe >nul 2>&1
ping 127.0.0.1 -n 2 >nul

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

echo [*] Dang bien dich ADBLogin.exe...
"%CSC%" /nologo /target:winexe /out:ADBLogin.exe /r:System.Windows.Forms.dll,System.Drawing.dll,Newtonsoft.Json.dll,WebDriver.dll,Leaf.xNet.dll,Faker.dll,"%NETSTANDARD%" src\Core\Models\*.cs src\Core\Services\*.cs src\UI\*.cs

if %errorlevel% equ 0 (
    copy /y ADBLogin.exe ADBLogin_Update.exe >nul
    echo.
    echo [THANH CONG] Da tao thanh cong: ADBLogin.exe va ADBLogin_Update.exe
) else (
    echo.
    echo [THAT BAI] Co loi xay ra trong qua trinh bien dich.
)

echo.
pause
