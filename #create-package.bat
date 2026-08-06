@echo off
REM ============================================================
REM 胡闹厨房存档管理器 - 打包发布脚本
REM 作用：把 Release 产物整理为可分发的 OvercookedSaveTool-Package 目录
REM 前置：请先运行 #build-release.bat 生成 Release 产物
REM 用法：双击运行 或 在命令行执行 #create-package.bat
REM ============================================================
chcp 65001 >nul
setlocal

REM 切换到脚本所在目录（即仓库根目录）
cd /d "%~dp0"

REM Release 输出目录与打包输出目录
set RELEASE_DIR=OvercookedTool.App\bin\Release\net9.0-windows
set PACKAGE_DIR=OvercookedSaveTool-Package

echo 胡闹厨房存档管理器 - 打包发布
echo =================================

REM 检查 Release 版本是否已构建
if not exist "%RELEASE_DIR%\OvercookedTool.App.exe" (
    echo.
    echo 错误: 未找到 Release 版本，请先运行 #build-release.bat
    pause
    exit /b 1
)

echo.
echo 步骤1: 准备打包目录...
if exist "%PACKAGE_DIR%" (
    echo 删除旧的包目录...
    rmdir /s /q "%PACKAGE_DIR%"
)
mkdir "%PACKAGE_DIR%"
if errorlevel 1 (
    echo 创建目录失败！
    pause
    exit /b 1
)

echo.
echo 步骤2: 复制程序文件...

REM 主程序与托管程序集
copy "%RELEASE_DIR%\OvercookedTool.App.exe" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\OvercookedTool.App.dll" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\OvercookedTool.Core.dll" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\libcoffee.dll" "%PACKAGE_DIR%\" >nul

REM 配置文件
copy "%RELEASE_DIR%\save_display_config.json" "%PACKAGE_DIR%\" >nul

REM 运行时配置文件
copy "%RELEASE_DIR%\OvercookedTool.App.runtimeconfig.json" "%PACKAGE_DIR%\" >nul
copy "%RELEASE_DIR%\OvercookedTool.App.deps.json" "%PACKAGE_DIR%\" >nul

REM 复制 Unity 设备标识测试工具
echo 复制 Unity 设备标识测试工具...
xcopy "%RELEASE_DIR%\UnityHarness" "%PACKAGE_DIR%\UnityHarness\" /E /I /Y >nul
if errorlevel 1 (
    echo 文件复制失败！
    pause
    exit /b 1
)

echo.
echo 步骤3: 验证文件...
if not exist "%PACKAGE_DIR%\OvercookedTool.App.exe" (
    echo 错误: 主程序文件缺失！
    pause
    exit /b 1
)
if not exist "%PACKAGE_DIR%\OvercookedTool.Core.dll" (
    echo 错误: 核心库文件缺失！
    pause
    exit /b 1
)
if not exist "%PACKAGE_DIR%\UnityHarness\_UnityDeviceUniqueIdentifierHarness.exe" (
    echo 警告: Unity 设备标识测试工具缺失！
)

echo.
echo =================================
echo 打包成功！
echo.
echo 目录: %cd%\%PACKAGE_DIR%\
echo.
echo 包含文件:
for %%f in ("%PACKAGE_DIR%\*.exe" "%PACKAGE_DIR%\*.dll" "%PACKAGE_DIR%\*.json") do (
    echo - %%~nxf
)

pause
endlocal
