@echo off
chcp 65001 >nul
:: Pictures for the README, taken over a running Dofus 3 window (see src\SelfTest.cs, --shots).
:: Stops Pénombre, builds dist\Penombre-test.exe, takes the pictures in dist\shots, then
:: starts dist\Penombre.exe again. Do not touch the game while it runs (about 20 s).
cd /d "%~dp0.."
taskkill /f /im Penombre-test.exe >nul 2>&1
taskkill /f /im Penombre.exe >nul 2>&1
call build.bat test > dist\shots-build.txt 2>&1
if errorlevel 1 exit /b 1
if exist dist\shots rmdir /s /q dist\shots
start "" /wait dist\Penombre-test.exe --selftest --shots --data=dist\shots
start "" dist\Penombre.exe
