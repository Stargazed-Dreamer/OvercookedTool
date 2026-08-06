@echo off
REM ============================================================
REM 胡闹厨房存档管理器 - 统一构建入口
REM 用法:
REM   build.bat            等同于 build.bat debug
REM   build.bat debug      构建 Debug 版本
REM   build.bat release    构建 Release 版本（先 clean 再 build）
REM   build.bat test       运行全部单元测试（Debug 配置）
REM   build.bat package    先构建 Release，再打包到 OvercookedSaveTool-Package
REM   build.bat clean      清理 Debug 与 Release 构建产物
REM   build.bat help       显示本帮助信息
REM ============================================================
chcp 65001 >nul
setlocal

cd /d "%~dp0"

REM 未传参时默认走 debug
if "%~1"=="" (
    call "#build-debug.bat"
    exit /b %errorlevel%
)

if /i "%~1"=="debug" (
    call "#build-debug.bat"
    exit /b %errorlevel%
)

if /i "%~1"=="release" (
    call "#build-release.bat"
    exit /b %errorlevel%
)

if /i "%~1"=="test" (
    echo 正在运行测试（Debug 配置）...
    dotnet test OvercookedTool.sln -c Debug --nologo
    if errorlevel 1 (
        echo.
        echo 测试失败！
        exit /b 1
    )
    echo.
    echo 测试通过！
    exit /b 0
)

if /i "%~1"=="package" (
    echo [package] 步骤1: 构建 Release 版本...
    call "#build-release.bat"
    if errorlevel 1 (
        echo.
        echo [package] Release 构建失败，终止打包。
        exit /b 1
    )
    echo.
    echo [package] 步骤2: 打包发布物...
    call "#create-package.bat"
    exit /b %errorlevel%
)

if /i "%~1"=="clean" (
    echo 正在清理 Debug 构建产物...
    dotnet clean OvercookedTool.sln -c Debug
    if errorlevel 1 (
        echo Debug 清理失败！
        exit /b 1
    )
    echo.
    echo 正在清理 Release 构建产物...
    dotnet clean OvercookedTool.sln -c Release
    if errorlevel 1 (
        echo Release 清理失败！
        exit /b 1
    )
    echo.
    echo 清理完成！
    exit /b 0
)

if /i "%~1"=="help" goto :PrintHelp
if /i "%~1"=="/?"  goto :PrintHelp
if /i "%~1"=="-h"  goto :PrintHelp

echo 未知参数: %~1
echo.
goto :PrintHelp

:PrintHelp
echo 胡闹厨房存档管理器 - 统一构建入口
echo =================================
echo 用法: build.bat [子命令]
echo.
echo 子命令:
echo   debug     构建 Debug 版本（默认）
echo   release   构建 Release 版本（先 clean 再 build）
echo   test      运行全部单元测试（Debug 配置）
echo   package   先构建 Release，再打包到 OvercookedSaveTool-Package
echo   clean     清理 Debug 与 Release 构建产物
echo   help      显示本帮助信息
exit /b 0
