@echo off
setlocal
pushd "%~dp0"
call build-native.cmd
if errorlevel 1 exit /b 1
dotnet publish src\ASnapdo\ASnapdo.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:SatelliteResourceLanguages=ko -o release\app\editor
set "build_result=%ERRORLEVEL%"
popd
exit /b %build_result%
