@echo off
chcp 65001 >nul
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
rem Собираем под arm64 — на нём работают все телефоны последних лет
dotnet publish "%PROJECT%" -f net10.0-android -c %CONFIG% -o "%OUTDIR%"
if errorlevel 1 (
    echo.
    echo Сборка не прошла.
    exit /b 1
)

rem Пакет один: publish каждый раз перезаписывает его
set APK=
for %%F in ("%OUTDIR%\*-Signed.apk") do (
    copy /y "%%F" "%OUTDIR%\cyberforum.apk" >nul
    set APK=1
)

if not defined APK (
    echo.
    echo Пакет не нашёлся - посмотрите, что написал dotnet выше.
    exit /b 1
)

echo.
echo Готово: %OUTDIR%\cyberforum.apk
endlocal
