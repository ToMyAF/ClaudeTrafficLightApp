<#
.SYNOPSIS
Claude Traffic Light 发布脚本
#>

# 获取脚本所在目录 - 兼容各种调用方式
if ($PSScriptRoot) {
    $ScriptDir = $PSScriptRoot
}
else {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

# 转为绝对路径
$ScriptDir = (Resolve-Path $ScriptDir).Path
$RootDir = (Resolve-Path (Join-Path $ScriptDir "..")).Path

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Claude Traffic Light - 发布脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "脚本目录: $ScriptDir"
Write-Host "项目根目录: $RootDir"
Write-Host ""

# 1. 检查 .NET SDK
Write-Host "[1/6] 检查 .NET SDK 环境..." -ForegroundColor Yellow
try {
    $SdkVersion = dotnet --version
    if ($LASTEXITCODE -ne 0) {
        throw "未找到 dotnet 命令"
    }
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
        try {
            Remove-Item -Path $path -Recurse -Force
            Write-Host "[信息] 已删除 $folder\ 目录"
        }
        catch {
            Write-Host "[警告] 无法删除 $folder\ 目录: $_" -ForegroundColor Yellow
        }
    }
}
Write-Host "[成功] 清理完成" -ForegroundColor Green
Write-Host ""

# 3. 读取版本号
Write-Host "[3/6] 读取版本信息..." -ForegroundColor Yellow
$ProjectFile = Join-Path $ScriptDir "TrafficLightApp.csproj"
Write-Host "[信息] 项目文件: $ProjectFile"

try {
    $ProjectContent = Get-Content $ProjectFile -Raw -Encoding UTF8
    if ($ProjectContent -match '<Version>([^<]+)</Version>') {
        $Version = $matches[1]
    }
    else {
        $Version = "1.0.0"
        Write-Host "[警告] 未找到 Version 节点，使用默认值: $Version" -ForegroundColor Yellow
    }
}
catch {
    $Version = "1.0.0"
    Write-Host "[警告] 读取项目文件失败，使用默认版本: $Version" -ForegroundColor Yellow
}
Write-Host "[信息] 版本号: v$Version" -ForegroundColor Cyan
Write-Host ""

# 4. 执行发布
Write-Host "[4/6] 开始发布构建..." -ForegroundColor Yellow
Write-Host "       这可能需要几分钟时间..."
Write-Host ""

$OriginalLocation = Get-Location
Set-Location $ScriptDir

try {
    dotnet publish -c Release -r win-x64 --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish 返回非零退出码: $LASTEXITCODE"
    }
}
catch {
    Write-Host ""
    Write-Host "[错误] 构建失败！" -ForegroundColor Red
    Write-Host "错误信息: $_" -ForegroundColor Red
    Set-Location $OriginalLocation
    Read-Host "按回车退出"
    exit 1
}
finally {
    Set-Location $OriginalLocation
}

Write-Host "[成功] 构建完成" -ForegroundColor Green
Write-Host ""

# 5. 准备发布包
Write-Host "[5/6] 准备发布包..." -ForegroundColor Yellow
$PublishDir = Join-Path $ScriptDir "bin\Release\net8.0-windows\win-x64\publish"
$OutputDir = Join-Path $ScriptDir "publish\ClaudeTrafficLight-v$Version"

Write-Host "[信息] 构建输出目录: $PublishDir"

# 检查构建输出
if (-not (Test-Path $PublishDir)) {
    Write-Host "[错误] 构建输出目录不存在: $PublishDir" -ForegroundColor Red

    # 尝试查找可能的输出位置
    Write-Host ""
    Write-Host "[信息] 尝试查找其他可能的输出位置..."
    $AltPaths = @(
        Join-Path $ScriptDir "bin\Release\net8.0\win-x64\publish",
        Join-Path $ScriptDir "bin\Release\win-x64\publish"
    )
    foreach ($altPath in $AltPaths) {
        if (Test-Path $altPath) {
            Write-Host "[发现] 找到替代路径: $altPath" -ForegroundColor Cyan
            $PublishDir = $altPath
            break
        }
    }

    if (-not (Test-Path $PublishDir)) {
        Write-Host "[错误] 无法找到构建输出！" -ForegroundColor Red
        Read-Host "按回车退出"
        exit 1
    }
}

# 创建输出目录
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# 复制主程序
$ExePath = Join-Path $PublishDir "ClaudeTrafficLight.exe"
Write-Host "[信息] 查找主程序: $ExePath"

if (Test-Path $ExePath) {
    Copy-Item -Path $ExePath -Destination $OutputDir -Force
    Write-Host "[成功] ClaudeTrafficLight.exe" -ForegroundColor Green
}
else {
    Write-Host "[错误] 找不到生成的 ClaudeTrafficLight.exe！" -ForegroundColor Red
    Write-Host "[信息] 目录内容:"
    Get-ChildItem $PublishDir | ForEach-Object { Write-Host "  - $($_.Name)" }
    Read-Host "按回车退出"
    exit 1
}

# 复制 PDB 文件（如果有）
$PdbPath = Join-Path $PublishDir "ClaudeTrafficLight.pdb"
if (Test-Path $PdbPath) {
    Copy-Item -Path $PdbPath -Destination $OutputDir -Force
    Write-Host "[成功] ClaudeTrafficLight.pdb" -ForegroundColor Green
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
    else {
        Write-Host "[警告] 找不到文件: $doc" -ForegroundColor Yellow
    }
}

# 创建版本信息文件
$VersionFile = Join-Path $OutputDir "VERSION.txt"
$versionContent = @"
Claude Traffic Light v$Version
构建时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

使用说明:
1. 双击 ClaudeTrafficLight.exe 运行程序
2. 红绿灯会显示在桌面
3. 右键托盘图标可以配置 Claude Code Hooks
4. 详细说明请查看 INSTALL.md
"@
$versionContent | Out-File -FilePath $VersionFile -Encoding Default
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
try {
    $FileSize = (Get-Item (Join-Path $OutputDir "ClaudeTrafficLight.exe")).Length
    $SizeMB = [math]::Round($FileSize / 1MB, 2)
    Write-Host "  文件大小: $SizeMB MB"
}
catch {
    Write-Host "  文件大小: 获取失败" -ForegroundColor Yellow
}
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
