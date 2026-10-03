@echo off
title ADBLogin v2.0 - Migration & Update Tool
color 0A
cls

echo ========================================================
echo        HE THONG CAP NHAT TU DONG - ADBLOGIN v2.0
echo ========================================================
echo.

:: 1. Kiem tra va dong tien trinh neu dang chay
echo [1/3] Kiem tra tien trinh ADBLogin...
tasklist /fi "imagename eq ADBLogin.exe" | findstr /i "ADBLogin.exe" >nul
if %errorlevel% equ 0 (
    echo    - Phat hien phan mem dang mo. Dang tat tien trinh de cap nhat...
    taskkill /f /im ADBLogin.exe >nul 2>&1
    ping 127.0.0.1 -n 2 >nul
) else (
    echo    - Khong co tien trinh nao dang chiem dung file.
)
echo.

:: 2. Xac dinh file cap nhat
set "UPDATE_FILE="
if exist "ADBLogin_Update.exe" set "UPDATE_FILE=ADBLogin_Update.exe"
if exist "ADBLogin_New.exe" set "UPDATE_FILE=ADBLogin_New.exe"

if "%UPDATE_FILE%"=="" (
    color 0C
    echo [LOI] Khong tim thay file cai dat moi (ADBLogin_Update.exe)!
    echo       Vui long de chung file Update_ADBLogin.bat va ADBLogin_Update.exe vao cung thu muc!
    echo.
    goto END
)

:: 3. Sao luu phien ban cu
echo [2/3] Tien hanh sao luu phien ban cu (Backup)...
if exist "ADBLogin.exe" (
    if exist "ADBLogin_Backup.exe" del /f /q "ADBLogin_Backup.exe" >nul 2>&1
    copy /y "ADBLogin.exe" "ADBLogin_Backup.exe" >nul
    echo    - Da tao ban sao luu an toan: ADBLogin_Backup.exe
) else (
    echo    - Chua co ban cu, bo qua buoc sao luu.
)
echo.

:: 4. Trien khai ban moi
echo [3/3] Dang trien khai phien ban moi...
copy /y "%UPDATE_FILE%" "ADBLogin.exe" >nul
if %errorlevel% equ 0 (
    echo    - Cap nhat thanh cong! Phien ban ADBLogin v2.0 da san sang.
    echo.
    echo ========================================================
    echo    CAP NHAT HOAN TAT! BAN CO THE MO ADBLOGIN DE SU DUNG.
    echo ========================================================
) else (
    color 0C
    echo [LOI] Khong the thay the file. Vui long kiem tra quyen Administrator.
)

:END
echo.
pause