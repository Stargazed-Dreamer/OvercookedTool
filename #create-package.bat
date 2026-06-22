@echo off
REM ���ֳ����浵������ - �ַ�����ű�
REM �˽ű��ᴴ��һ���ɾ��ġ��ɰ�ȫ�ַ��ĳ���
setlocal

cd /d "%~dp0"

set RELEASE_DIR=OvercookedTool.App\bin\Release\net9.0-windows
set PACKAGE_DIR=OvercookedSaveTool-Package

echo ���ֳ����浵������ - �ַ����
echo =================================

REM ���Release�汾�Ƿ����
if not exist "%RELEASE_DIR%\OvercookedTool.App.exe" (
    echo ����: δ�ҵ�Release�汾!
    echo �������� build-release.bat ����Release�汾
    pause
    exit /b 1
)

echo ����1: �����ַ�Ŀ��Ŀ¼...
if exist "%PACKAGE_DIR%" (
    echo ɾ���ɵİ�Ŀ¼...
    rmdir /s /q "%PACKAGE_DIR%"
)
mkdir "%PACKAGE_DIR%"

if errorlevel 1 (
    echo ����Ŀ¼ʧ��!
    pause
    exit /b 1
)

echo.
echo ����2: ���ƺ����ļ�...

REM ���Ʊ�Ҫ�����ļ�
copy "%RELEASE_DIR%\OvercookedTool.App.exe" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\OvercookedTool.App.dll" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\OvercookedTool.Core.dll" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\libcoffee.dll" "%PACKAGE_DIR%\" >nul

REM ���������ļ�
copy "%RELEASE_DIR%\save_display_config.json" "%PACKAGE_DIR%\" >nul

REM ��������ʱ�����ļ�
copy "%RELEASE_DIR%\OvercookedTool.App.runtimeconfig.json" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\OvercookedTool.App.deps.json" "%PACKAGE_DIR%\" >nul

REM ���� Unity �豸��ʶ����
echo ���� Unity �豸��ʶ����...
xcopy "%RELEASE_DIR%\UnityHarness" "%PACKAGE_DIR%\UnityHarness\" /E /I /Y >nul

if errorlevel 1 (
    echo �ļ�����ʧ��!
    pause
    exit /b 1
)

echo.
echo ����3: ��֤�ļ�...
if not exist "%PACKAGE_DIR%\OvercookedTool.App.exe" (
    echo ����: �������ļ�ȱʧ!
    pause
    exit /b 1
)

if not exist "%PACKAGE_DIR%\OvercookedTool.Core.dll" (
    echo ����: ���Ŀ��ļ�ȱʧ!
    pause
    exit /b 1
)

if not exist "%PACKAGE_DIR%\UnityHarness\_UnityDeviceUniqueIdentifierHarness.exe" (
    echo ����: Unity �豸��ʶ����ȱʧ!
)

echo.
echo =================================
echo �ַ�������!
echo.
echo ��Ŀ¼: %cd%\%PACKAGE_DIR%\
echo.
echo �����ļ�:
for %%f in ("%PACKAGE_DIR%\*.*") do (
    echo - %%~nxf
)

pause
