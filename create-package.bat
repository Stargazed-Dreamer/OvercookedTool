::@echo off
REM 胡闹厨房存档工具 - 分发包创建脚本
REM 这个脚本会创建一个干净的、可以安全分发的程序包
setlocal

cd /d "%~dp0"

set RELEASE_DIR=OvercookedTool.App\bin\Release\net9.0-windows
set PACKAGE_DIR=OvercookedSaveTool-Package

echo 胡闹厨房存档工具 - 分发包创建
echo =================================

REM 检查Release版本是否存在
if not exist "%RELEASE_DIR%\OvercookedTool.App.exe" (
    echo 错误: 未找到Release版本!
    echo 请先运行 build-release.bat 编译Release版本
    pause
    exit /b 1
)

echo 步骤1: 创建分发包目录...
if exist "%PACKAGE_DIR%" (
    echo 删除旧的包目录...
    rmdir /s /q "%PACKAGE_DIR%"
)
mkdir "%PACKAGE_DIR%"

if errorlevel 1 (
    echo 创建目录失败!
    pause
    exit /b 1
)

echo.
echo 步骤2: 复制必需的文件...

REM 复制主要程序文件
copy "%RELEASE_DIR%\OvercookedTool.App.exe" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\OvercookedTool.App.dll" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\OvercookedTool.Core.dll" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\libcoffee.dll" "%PACKAGE_DIR%\" >nul

REM 复制配置文件
copy "%RELEASE_DIR%\save_display_config.json" "%PACKAGE_DIR%\" >nul

REM 复制运行时配置文件
copy "%RELEASE_DIR%\OvercookedTool.App.runtimeconfig.json" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\OvercookedTool.App.deps.json" "%PACKAGE_DIR%\" >nul

if errorlevel 1 (
    echo 文件复制失败!
    pause
    exit /b 1
)

echo.
echo 步骤3: 验证文件...
if not exist "%PACKAGE_DIR%\OvercookedTool.App.exe" (
    echo 错误: 主程序文件缺失!
    pause
    exit /b 1
)

if not exist "%PACKAGE_DIR%\OvercookedTool.Core.dll" (
    echo 错误: 核心库文件缺失!
    pause
    exit /b 1
)

echo.
echo =================================
echo ✓ 分发包创建成功!
echo.
echo 包目录: %cd%\%PACKAGE_DIR%\
echo.
echo 包含的文件:
for %%f in ("%PACKAGE_DIR%\*.*") do (
    echo - %%~nxf
)

pause
