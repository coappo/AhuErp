@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem =========================================================================
rem AhuErp - регенерация .resx-снапшота последней EF6-миграции.
rem
rem Что делает:
rem   1. Патчит tools\MigrationGenerator\App.config твоей строкой подключения
rem      (в конце скрипта восстанавливает оригинал).
rem   2. Удаляет scaffold-БД (по умолчанию AhuErpDb_Scaffold).
rem   3. Билдит src\AhuErp.Core и tools\MigrationGenerator (через dotnet).
rem   4. Запускает MigrationGenerator: он накатывает все Up()-миграции
rem      на пустую БД и просит EF6 MigrationScaffolder сгенерировать
rem      "дельту" между текущей моделью DbContext и последним .resx-Target.
rem      В сгенерированном .resx будет КОРРЕКТНЫЙ полный снапшот модели.
rem   5. Копирует свежий .resx поверх последней миграции
rem      (по умолчанию 20260430000000_AddSearchIndex.resx) и удаляет
rem      временные .cs / .Designer.cs / *_ResxSnapshot.resx.
rem
rem Использование:
rem   tools\regen-migrations.bat
rem   tools\regen-migrations.bat "DESKTOP-I1OTVEB\SQLEXPRESS"
rem   tools\regen-migrations.bat "DESKTOP-I1OTVEB\SQLEXPRESS" AhuErpDb_Scaffold
rem
rem Требования:
rem   - dotnet SDK (любой современный, скачивает net48 reference assemblies)
rem   - sqlcmd (SQL Server Command Line Utilities)
rem   - SQL Server (LocalDB / Express / любой), доступный по Integrated Security
rem
rem После успешного прогона:
rem   - в Migrations\ обновлён только .resx последней миграции;
rem   - тебе остаётся открыть Package Manager Console в Visual Studio и
rem     выполнить `Add-Migration TestEmpty` - получится пустая Up()/Down()
rem     - значит модель и слепок синхронны, .resx можно коммитить.
rem =========================================================================

if "%~1" neq "" (
    set "SQLSERVER=%~1"
) else if not defined SQLSERVER (
    set "SQLSERVER=DESKTOP-I1OTVEB\SQLEXPRESS"
)

if "%~2" neq "" (
    set "SCAFFOLD_DB=%~2"
) else if not defined SCAFFOLD_DB (
    set "SCAFFOLD_DB=AhuErpDb_Scaffold"
)

set "TARGETMIG=20260430000000_AddSearchIndex"

set "ROOT=%~dp0.."
pushd "%ROOT%" >nul
set "ROOT=%CD%"
popd >nul

set "CORE=%ROOT%\src\AhuErp.Core"
set "MIGS=%CORE%\Migrations"
set "GEN=%ROOT%\tools\MigrationGenerator"
set "GENBIN=%GEN%\bin\Debug\net48"
set "APPCFG=%GEN%\App.config"

echo.
echo === AhuErp resx regenerator ===
echo SQL Server instance : %SQLSERVER%
echo Scaffold DB         : %SCAFFOLD_DB%
echo Repo root           : %ROOT%
echo Target migration    : %TARGETMIG%
echo.

rem --- 0/6: проверяем инструменты ------------------------------------------
where dotnet >nul 2>&1 || (
    echo [X] dotnet not found in PATH. Install .NET SDK from https://dotnet.microsoft.com/download
    goto :err
)
where sqlcmd >nul 2>&1 || (
    echo [X] sqlcmd not found in PATH. Install "SQL Server Command Line Utilities".
    goto :err
)
where powershell >nul 2>&1 || (
    echo [X] powershell.exe not found, что странно. Скрипт использует его для патча App.config.
    goto :err
)

rem --- 1/6: патчим App.config (с бекапом) ----------------------------------
echo === [1/6] Patching App.config connection string
copy /Y "%APPCFG%" "%APPCFG%.bak" >nul || goto :err

set "PATCHED_CONN=Data Source=%SQLSERVER%;Initial Catalog=%SCAFFOLD_DB%;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$cfg = '%APPCFG%';" ^
    "$xml = [xml](Get-Content -Raw -LiteralPath $cfg);" ^
    "$node = $xml.configuration.connectionStrings.add | Where-Object { $_.name -eq 'AhuErpDb' };" ^
    "$node.providerName = 'System.Data.SqlClient';" ^
    "$node.connectionString = '%PATCHED_CONN%';" ^
    "$xml.Save($cfg);" || goto :err

rem --- 2/6: дропаем scaffold-БД --------------------------------------------
echo === [2/6] Drop %SCAFFOLD_DB% (если существует)
sqlcmd -S "%SQLSERVER%" -E -b -Q "IF DB_ID('%SCAFFOLD_DB%') IS NOT NULL BEGIN ALTER DATABASE [%SCAFFOLD_DB%] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [%SCAFFOLD_DB%]; END" || goto :err

rem --- 3/6: билдим Core ----------------------------------------------------
echo === [3/6] Build AhuErp.Core
dotnet build "%CORE%\AhuErp.Core.csproj" -c Debug -v minimal --nologo || goto :err

rem --- 4/6: билдим MigrationGenerator --------------------------------------
echo === [4/6] Build MigrationGenerator
dotnet build "%GEN%\MigrationGenerator.csproj" -c Debug -v minimal --nologo || goto :err

rem --- 5/6: бекапим старые .resx и запускаем scaffolder --------------------
echo === [5/6] Backup existing .resx and scaffold a fresh snapshot
set "BACKUPDIR=%ROOT%\Migrations.backup-%RANDOM%%RANDOM%"
mkdir "%BACKUPDIR%" >nul 2>&1
copy /Y "%MIGS%\20260427000000_AddOrgAndSubstitution.resx" "%BACKUPDIR%\" >nul
copy /Y "%MIGS%\20260428000000_AddNotifications.resx"     "%BACKUPDIR%\" >nul
copy /Y "%MIGS%\20260429000000_AddSignatures.resx"        "%BACKUPDIR%\" >nul
copy /Y "%MIGS%\20260430000000_AddSearchIndex.resx"       "%BACKUPDIR%\" >nul
echo Old .resx backed up to: %BACKUPDIR%

pushd "%GENBIN%" >nul
"%GENBIN%\MigrationGenerator.exe" "%MIGS%" "ResxSnapshot"
set "RC=%ERRORLEVEL%"
popd >nul
if %RC% neq 0 goto :err

rem --- 6/6: подменяем .resx на свежий и удаляем временные файлы -----------
echo === [6/6] Apply fresh snapshot to %TARGETMIG%.resx
set "TEMPRESX="
for /f "delims=" %%F in ('dir /B /OD "%MIGS%\*_ResxSnapshot.resx" 2^>nul') do set "TEMPRESX=%%F"
if not defined TEMPRESX (
    echo [X] Не найден свежесгенерированный *_ResxSnapshot.resx в %MIGS%
    goto :err
)
set "TEMPBASE=!TEMPRESX:.resx=!"
copy /Y "%MIGS%\!TEMPRESX!" "%MIGS%\%TARGETMIG%.resx" >nul

del "%MIGS%\!TEMPBASE!.cs"          >nul 2>&1
del "%MIGS%\!TEMPBASE!.Designer.cs" >nul 2>&1
del "%MIGS%\!TEMPRESX!"             >nul 2>&1

call :restore_appconfig

echo.
echo === Done ===
echo  Updated: %MIGS%\%TARGETMIG%.resx
echo  Backup : %BACKUPDIR%
echo.
echo Next steps:
echo   1. Открой решение в Visual Studio, дождись восстановления NuGet.
echo   2. Tools - NuGet Package Manager - Package Manager Console:
echo        Add-Migration TestEmpty -ProjectName AhuErp.Core -StartUpProjectName AhuErp.UI
echo      Если Up()/Down() пусты - значит модель и слепок синхронны.
echo      Удали TestEmpty (Remove-Migration) и закоммить только обновлённый .resx.
echo   3. Если Up() не пустой - значит модель ушла дальше .resx, дополни код
echo      и повтори этот скрипт.
echo.
endlocal
exit /b 0

:err
call :restore_appconfig
echo.
echo [X] regen-migrations.bat failed.
endlocal
exit /b 1

:restore_appconfig
if exist "%APPCFG%.bak" (
    move /Y "%APPCFG%.bak" "%APPCFG%" >nul
)
exit /b 0
