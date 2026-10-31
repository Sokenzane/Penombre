@echo off
chcp 65001 >nul
:: Self-test of Pénombre over a running Dofus 3 window (see src\SelfTest.cs).
:: Stops Pénombre, builds dist\Penombre-test.exe, runs it on a separate settings folder
:: (dist\selftest-data, your own settings are not touched), then rebuilds and starts
:: dist\Penombre.exe again.
:: Report: dist\selftest-data\selftest-report.txt
if not "%~1"=="min" (
    start "Pénombre self-test" /min cmd /c ""%~f0" min"
    exit /b
)
cd /d "%~dp0.."
taskkill /f /im Penombre-test.exe >nul 2>&1
:: a previous run may restart Penombre.exe when its test is stopped: wait, then stop it
timeout /t 2 /nobreak >nul
taskkill /f /im Penombre.exe >nul 2>&1
call build.bat test > dist\selftest-build.txt 2>&1
if errorlevel 1 exit /b 1
if exist dist\selftest-data rmdir /s /q dist\selftest-data
start "" /wait dist\Penombre-test.exe --selftest --data=dist\selftest-data
call build.bat ci > dist\build-output.txt 2>&1
start "" dist\Penombre.exe
