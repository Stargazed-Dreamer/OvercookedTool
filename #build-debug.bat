@echo off
REM 胡闹厨房存档管理器 - Debug版本构建脚本
setlocal

cd /d "%~dp0"
echo 正在构建Debug版本...
echo.

dotnet build OvercookedTool.sln -c Debug -v minimal
if errorlevel 1 (
    echo 构建失败!
    pause
    exit /b 1
)

echo.
echo 构建成功!
pause
