@echo off
chcp 65001 >nul
setlocal
:: Builds dist\Penombre.exe from src\Penombre.cs with the C# compiler that ships
:: with Windows (.NET Framework 4): nothing to install. 64-bit only: the Magnification
:: API used by the shader does not work in a 32-bit process on 64-bit Windows.
::   build.bat       build, then wait for a key
::   build.bat ci    build without pausing

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo C# compiler not found: is .NET Framework 4 installed?
    set "ERR=1"
    goto :end
)

cd /d "%~dp0"
if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 ^
    /win32icon:assets\penombre.ico /resource:assets\penombre.ico,penombre.ico ^
    /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    /out:dist\Penombre.exe src\Penombre.cs
set "ERR=%errorlevel%"

echo.
if "%ERR%"=="0" (echo Built: dist\Penombre.exe) else (echo Build failed, see the errors above.)

:end
if /i not "%~1"=="ci" pause
exit /b %ERR%
