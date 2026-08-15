@echo off
rem Сборка apk для Android - того самого файла, который можно отдать человеку.
rem   scripts\build-android.bat
rem Своего ключа пока нет, поэтому пакет подписывается отладочным: при установке
rem система спросит разрешение на "неизвестный источник", это нормально.
rem Конфигурацию можно переопределить:  set CONFIG=Debug ^& scripts\build-android.bat

setlocal enabledelayedexpansion
cd /d "%~dp0.."

if "%CONFIG%"=="" set CONFIG=Release

set PROJECT=src\CyberForum.App\CyberForum.App.csproj
set OUTDIR=%~dp0..\dist

echo Собираем %CONFIG% для Android...
rem arm64 и arm сразу: 32-битные телефоны ещё встречаются
dotnet publish "%PROJECT%" -f net10.0-android -c %CONFIG% -o "%OUTDIR%" -p:RuntimeIdentifiers=android-arm64;android-arm
if errorlevel 1 (
    echo.
    echo Сборка не прошла.
    exit /b 1
)

rem Берём самый свежий подписанный пакет: их несколько, если собирали разные версии
set APK=
for /f "delims=" %%F in ('dir /b /s /o-d "%OUTDIR%\*-Signed.apk" 2^>nul') do (
    if not defined APK set APK=%%F
)

if not defined APK (
    echo.
    echo Пакет не нашёлся - посмотрите, что написал dotnet выше.
    exit /b 1
)

if not exist "%OUTDIR%" mkdir "%OUTDIR%"
copy /y "!APK!" "%OUTDIR%\cyberforum.apk" >nul

echo.
echo Готово: %OUTDIR%\cyberforum.apk
endlocal
