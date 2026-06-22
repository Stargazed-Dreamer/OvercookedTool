@echo off
REM ���ֳ����浵������ - Release�汾�����ű�
REM �˽ű�����Release�汾���ʺϷַ����û�
setlocal

cd /d "%~dp0"
echo ���ڹ������ֳ����浵������Release�汾...
echo.

echo ����1: ����֮ǰ�Ĺ���...
dotnet clean OvercookedTool.sln -c Release -v minimal
if errorlevel 1 (
    echo ����ʧ��!
    pause
    exit /b 1
)

echo.
echo ����2: ����Release�汾...
dotnet build OvercookedTool.sln -c Release -v minimal
if errorlevel 1 (
    echo ����ʧ��!
    pause
    exit /b 1
)

echo.
echo �����ɹ�!
pause
