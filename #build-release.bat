@echo off
REM ============================================================
REM 胡闹厨房存档管理器 - Release 版本构建脚本
REM 作用：先清理再构建 Release 配置，适合发布给最终用户
REM 用法：双击运行 或 在命令行执行 #build-release.bat
REM ============================================================
chcp 65001 >nul
setlocal

REM 切换到脚本所在目录（即仓库根目录）
cd /d "%~dp0"

echo 步骤1: 清理之前的构建...
dotnet clean OvercookedTool.sln -c Release -v minimal
if errorlevel 1 (
    echo.
    echo 清理失败！
    pause
    exit /b 1
)

echo.
echo 步骤2: 构建 Release 版本...
dotnet build OvercookedTool.sln -c Release -v minimal
if errorlevel 1 (
    echo.
    echo 构建失败！
    pause
    exit /b 1
)

echo.
echo 构建成功！
pause
endlocal
