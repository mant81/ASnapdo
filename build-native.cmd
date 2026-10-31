@echo off
setlocal
for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "vs_path=%%i"
if not defined vs_path exit /b 1
call "%vs_path%\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1
pushd "%~dp0src\ASnapdo.Tray"
if not exist "..\..\release\app" mkdir "..\..\release\app"
if not exist "..\..\release\native-build" mkdir "..\..\release\native-build"
rc /nologo /fo "..\..\release\native-build\tray.res" app.rc
if errorlevel 1 exit /b 1
cl /nologo /O1 /MT /GS /sdl /guard:cf /utf-8 /TC /Fo"..\..\release\native-build\tray.obj" main.c /link /SUBSYSTEM:WINDOWS /DYNAMICBASE /HIGHENTROPYVA /NXCOMPAT /guard:cf /CETCOMPAT /OUT:"..\..\release\app\ASnapdo.exe" "..\..\release\native-build\tray.res" kernel32.lib user32.lib shell32.lib advapi32.lib
set "result=%ERRORLEVEL%"
popd
exit /b %result%
