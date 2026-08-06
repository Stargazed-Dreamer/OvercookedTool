@echo off
REM ============================================================
REM 胡闹厨房存档管理器 - Debug 版本构建脚本
REM 作用：构建 Debug 配置的全部项目（Core / App / Tests）
REM 用法：双击运行 或 在命令行执行 #build-debug.bat
REM ============================================================
chcp 65001 >nul
setlocal

REM 切换到脚本所在目录（即仓库根目录）
cd /d "%~dp0"

echo 正在构建 Debug 版本...
echo.

REM 调用 dotnet 构建 Debug 配置，最小化日志输出
dotnet build OvercookedTool.sln -c Debug -v minimal
if errorlevel 1 (
    echo.
    echo 构建失败！
    pause
    exit /b 1
)

echo.
echo 构建成功！输出目录：OvercookedTool.App\bin\Debug\net9.0-windows\
pause
endlocal
