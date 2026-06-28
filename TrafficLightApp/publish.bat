@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   Claude Traffic Light - 发布脚本
echo ========================================
echo.

:: 检查 .NET SDK
echo [1/5] 检查 .NET SDK...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 未找到 .NET SDK
    pause
    exit /b 1
)
for /f "tokens=*" %%i in ('dotnet --version') do echo [成功] .NET SDK 版本: %%i
echo.

:: 清理旧构建
echo [2/5] 清理旧构建...
if exist "bin\" rmdir /s /q "bin\"
if exist "obj\" rmdir /s /q "obj\"
if exist "publish\" rmdir /s /q "publish\"
echo [成功] 清理完成
echo.

:: 执行发布
echo [3/5] 开始发布构建...
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
echo [4/5] 准备发布包...
set "PUBLISH_DIR=bin\Release\net8.0-windows\win-x64\publish"
set "OUTPUT_DIR=publish\ClaudeTrafficLight"

mkdir "%OUTPUT_DIR%" 2>nul

:: 复制主程序
if exist "%PUBLISH_DIR%\ClaudeTrafficLight.exe" (
    copy "%PUBLISH_DIR%\ClaudeTrafficLight.exe" "%OUTPUT_DIR%\" >nul
    echo [成功] ClaudeTrafficLight.exe
) else (
    echo [错误] 找不到生成的 ClaudeTrafficLight.exe！
    echo [信息] 请检查: %PUBLISH_DIR%
    pause
    exit /b 1
)

:: 复制文档
if exist "..\README.md" copy "..\README.md" "%OUTPUT_DIR%\" >nul && echo [成功] README.md
if exist "..\INSTALL.md" copy "..\INSTALL.md" "%OUTPUT_DIR%\" >nul && echo [成功] INSTALL.md
if exist "..\CHANGELOG.md" copy "..\CHANGELOG.md" "%OUTPUT_DIR%\" >nul && echo [成功] CHANGELOG.md
if exist "..\CONTRIBUTING.md" copy "..\CONTRIBUTING.md" "%OUTPUT_DIR%\" >nul && echo [成功] CONTRIBUTING.md
if exist "..\LICENSE" copy "..\LICENSE" "%OUTPUT_DIR%\LICENSE.txt" >nul && echo [成功] LICENSE

:: 创建版本信息文件
echo Claude Traffic Light > "%OUTPUT_DIR%\VERSION.txt"
echo 构建时间: %date% %time% >> "%OUTPUT_DIR%\VERSION.txt"
echo [成功] VERSION.txt

echo.
echo [信息] 发布文件已复制到: %OUTPUT_DIR%\
echo.

:: 完成
echo [5/5] 发布完成！
echo.
echo ========================================
echo   发布成功！
echo ========================================
echo.

:: 显示文件大小
for %%A in ("%OUTPUT_DIR%\ClaudeTrafficLight.exe") do (
    set /a SIZE_MB=%%~zA / 1048576
)
echo   文件大小: !SIZE_MB! MB
echo.
echo   文件列表：
dir /b "%OUTPUT_DIR%"
echo.
echo ========================================
echo.

:: 询问是否打开输出目录
set /p OPEN_FOLDER="是否打开输出目录？(Y/N): "
if /i "%OPEN_FOLDER%"=="Y" explorer "%OUTPUT_DIR%"

echo.
pause
