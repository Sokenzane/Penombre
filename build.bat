@echo off
chcp 65001 >nul
setlocal
:: Builds dist\Penombre.exe from the sources in src\ with the C# compiler that ships
:: with Windows (.NET Framework 4): nothing to install. 64-bit only: the Magnification
:: API used by the shader does not work in a 32-bit process on 64-bit Windows.
::   build.bat       build dist\Penombre.exe, then wait for a key
::   build.bat ci    same, without pausing
::   build.bat test  build dist\Penombre-test.exe (with src\SelfTest.cs), without pausing

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo C# compiler not found: is .NET Framework 4 installed?
    set "ERR=1"
    goto :end
)

cd /d "%~dp0"
if not exist dist mkdir dist
set "OUT=dist\Penombre.exe"
set "SRC=src\Penombre.cs src\Interface.cs"
if /i "%~1"=="test" (
    set "OUT=dist\Penombre-test.exe"
    set "SRC=src\Penombre.cs src\Interface.cs src\SelfTest.cs"
)
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 ^
    /win32icon:assets\penombre.ico /resource:assets\penombre.ico,penombre.ico ^
    /resource:assets\tray-icons.png,tray-icons.png ^
    /resource:assets\fonts\Nunito-Bold.ttf,Nunito-Bold.ttf ^
    /resource:assets\fonts\Nunito-ExtraBold.ttf,Nunito-ExtraBold.ttf ^
    /resource:assets\fonts\Nunito-Black.ttf,Nunito-Black.ttf ^
    /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    /out:%OUT% %SRC%
set "ERR=%errorlevel%"

echo.
if "%ERR%"=="0" (echo Built: %OUT%) else (echo Build failed, see the errors above.)

:end
if "%~1"=="" pause
exit /b %ERR%
