<#
.SYNOPSIS
Claude Traffic Light 发布脚本
#>

$ErrorActionPreference = "Stop"

# 获取脚本所在目录
$ScriptDir = $PSScriptRoot
$RootDir = Join-Path $ScriptDir ".."

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Claude Traffic Light - 发布脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 .NET SDK
Write-Host "[1/6] 检查 .NET SDK 环境..." -ForegroundColor Yellow
try {
    $SdkVersion = dotnet --version
    Write-Host "[成功] .NET SDK 版本: $SdkVersion" -ForegroundColor Green
}
catch {
    Write-Host "[错误] 未找到 .NET SDK，请先安装 .NET 8.0 SDK 或更高版本" -ForegroundColor Red
    Write-Host "下载地址: https://dotnet.microsoft.com/download"
    Read-Host "按回车退出"
    exit 1
}
Write-Host ""

# 2. 清理旧构建
Write-Host "[2/6] 清理旧构建文件..." -ForegroundColor Yellow
$foldersToClean = @("bin", "obj", "publish")
foreach ($folder in $foldersToClean) {
    $path = Join-Path $ScriptDir $folder
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
        Write-Host "[信息] 已删除 $folder\ 目录"
    }
}
Write-Host "[成功] 清理完成" -ForegroundColor Green
Write-Host ""

# 3. 读取版本号
Write-Host "[3/6] 读取版本信息..." -ForegroundColor Yellow
$ProjectFile = Join-Path $ScriptDir "TrafficLightApp.csproj"
$ProjectContent = Get-Content $ProjectFile -Raw
if ($ProjectContent -match '<Version>([^<]+)</Version>') {
    $Version = $matches[1]
}
else {
    $Version = "1.0.0"
}
Write-Host "[信息] 版本号: v$Version" -ForegroundColor Cyan
Write-Host ""

# 4. 执行发布
Write-Host "[4/6] 开始发布构建..." -ForegroundColor Yellow
Write-Host "       这可能需要几分钟时间..."
Write-Host ""

Set-Location $ScriptDir
dotnet publish -c Release -r win-x64 --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[错误] 构建失败！" -ForegroundColor Red
    Read-Host "按回车退出"
    exit 1
}
Write-Host "[成功] 构建完成" -ForegroundColor Green
Write-Host ""

# 5. 准备发布包
Write-Host "[5/6] 准备发布包..." -ForegroundColor Yellow
$PublishDir = Join-Path $ScriptDir "bin\Release\net8.0-windows\win-x64\publish"
$OutputDir = Join-Path $ScriptDir "publish\ClaudeTrafficLight-v$Version"

# 创建输出目录
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# 复制主程序
$ExePath = Join-Path $PublishDir "ClaudeTrafficLight.exe"
if (Test-Path $ExePath) {
    Copy-Item -Path $ExePath -Destination $OutputDir -Force
    Write-Host "[成功] ClaudeTrafficLight.exe" -ForegroundColor Green
}
else {
    Write-Host "[错误] 找不到生成的 ClaudeTrafficLight.exe！" -ForegroundColor Red
    Write-Host "[信息] 请检查构建输出: $PublishDir"
    Read-Host "按回车退出"
    exit 1
}

# 复制文档
Write-Host "[信息] 复制文档..."
$docs = @("README.md", "INSTALL.md", "CHANGELOG.md", "CONTRIBUTING.md", "LICENSE")
foreach ($doc in $docs) {
    $docPath = Join-Path $RootDir $doc
    if (Test-Path $docPath) {
        if ($doc -eq "LICENSE") {
            Copy-Item -Path $docPath -Destination (Join-Path $OutputDir "LICENSE.txt") -Force
        }
        else {
            Copy-Item -Path $docPath -Destination $OutputDir -Force
        }
        Write-Host "[成功] $doc" -ForegroundColor Green
    }
}

# 创建版本信息文件
$VersionFile = Join-Path $OutputDir "VERSION.txt"
@"
Claude Traffic Light v$Version
构建时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

使用说明:
1. 双击 ClaudeTrafficLight.exe 运行程序
2. 红绿灯会显示在桌面
3. 右键托盘图标可以配置 Claude Code Hooks
4. 详细说明请查看 INSTALL.md
"@ | Out-File -FilePath $VersionFile -Encoding UTF8
Write-Host "[成功] VERSION.txt" -ForegroundColor Green

Write-Host ""
Write-Host "[信息] 发布文件已复制到:" -ForegroundColor Cyan
Write-Host "       $OutputDir"
Write-Host ""

# 6. 构建摘要
Write-Host "[6/6] 构建摘要..." -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  发布成功！ 🎉" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  版本: v$Version"
Write-Host "  平台: Windows x64"
Write-Host "  模式: 自包含单文件"
Write-Host ""
Write-Host "  输出目录:"
Write-Host "    $OutputDir"
Write-Host ""

# 显示文件大小
$FileSize = (Get-Item (Join-Path $OutputDir "ClaudeTrafficLight.exe")).Length
$SizeMB = [math]::Round($FileSize / 1MB, 2)
Write-Host "  文件大小: $SizeMB MB"
Write-Host ""

# 列出所有文件
Write-Host "  文件列表："
Get-ChildItem $OutputDir | ForEach-Object { Write-Host "    - $($_.Name)" }
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# 询问是否打开输出目录
$OpenFolder = Read-Host "是否打开输出目录？(Y/N)"
if ($OpenFolder -eq "Y" -or $OpenFolder -eq "y") {
    explorer $OutputDir
}

Write-Host ""
Write-Host "完成！按回车退出..."
Read-Host
