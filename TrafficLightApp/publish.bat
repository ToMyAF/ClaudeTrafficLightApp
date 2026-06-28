@echo off
setlocal enabledelayedexpansion

:: ========================================
:: Claude Traffic Light 发布脚本
:: ========================================

echo.
echo ========================================
echo   Claude Traffic Light - 发布脚本
echo ========================================
echo.

:: 检查 .NET SDK
echo [1/6] 检查 .NET SDK 环境...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 未找到 .NET SDK，请先安装 .NET 8.0 SDK 或更高版本
    echo 下载地址: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)
for /f "tokens=*" %%i in ('dotnet --version') do set SDK_VERSION=%%i
echo [成功] .NET SDK 版本: %SDK_VERSION%
echo.

:: 清理旧构建
echo [2/6] 清理旧构建文件...
if exist "bin\" (
    rmdir /s /q "bin\"
    echo [信息] 已删除 bin\ 目录
)
if exist "obj\" (
    rmdir /s /q "obj\"
    echo [信息] 已删除 obj\ 目录
)
if exist "publish\" (
    rmdir /s /q "publish\"
    echo [信息] 已删除 publish\ 目录
)
echo [成功] 清理完成
echo.

:: 从项目文件读取版本号
echo [3/6] 读取版本信息...
for /f "tokens=2 delims=><" %%i in ('findstr "<Version>" TrafficLightApp.csproj') do set VERSION=%%i
if "%VERSION%"=="" set VERSION=1.0.0
echo [信息] 版本号: v%VERSION%
echo.

:: 执行发布
echo [4/6] 开始发布构建...
echo        这可能需要几分钟时间...
echo.
dotnet publish -c Release -r win-x64 --nologo

if %errorlevel% neq 0 (
    echo.
    echo [错误] 构建失败！
    pause
    exit /b 1
)
echo [成功] 构建完成
echo.

:: 准备发布包
echo [5/6] 准备发布包...
set PUBLISH_DIR=bin\Release\net8.0-windows\win-x64\publish
set OUTPUT_DIR=publish\ClaudeTrafficLight-v%VERSION%

if not exist "publish\" mkdir "publish\"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

:: 复制文件
copy "%PUBLISH_DIR%\ClaudeTrafficLight.exe" "%OUTPUT_DIR%\" >nul
copy "..\README.md" "%OUTPUT_DIR%\" >nul
copy "..\INSTALL.md" "%OUTPUT_DIR%\" >nul

:: 创建版本信息文件
echo Claude Traffic Light v%VERSION% > "%OUTPUT_DIR%\VERSION.txt"
echo 构建时间: %date% %time% >> "%OUTPUT_DIR%\VERSION.txt"

echo [信息] 发布文件已复制到 %OUTPUT_DIR%\
echo.

:: 显示文件信息
echo [6/6] 构建摘要...
echo.
echo ========================================
echo   发布成功！ 🎉
echo ========================================
echo.
echo   版本: v%VERSION%
echo   平台: Windows x64
echo   模式: 自包含单文件
echo.
echo   输出目录:
echo     %~dp0%OUTPUT_DIR%
echo.

:: 显示文件大小
for %%A in ("%OUTPUT_DIR%\ClaudeTrafficLight.exe") do (
    set SIZE=%%~zA
    set /a SIZE_MB=!SIZE! / 1048576
)
echo   文件大小: !SIZE_MB! MB
echo.
echo ========================================
echo.

:: 询问是否打开输出目录
set /p OPEN_FOLDER="是否打开输出目录？(Y/N): "
if /i "%OPEN_FOLDER%"=="Y" (
    explorer "%OUTPUT_DIR%"
)

echo.
pause
