#!/usr/bin/env pwsh
# build-win.ps1 - Windows 发布脚本
# 用法: .\build-win.ps1 [x64] (默认 x64)

param(
    [string]$Arch = "x64"
)

$project = "src/Liangwengu/Liangwengu.fsproj"
$config = "Release"
$outputDir = "artifacts/windows-$Arch"

# 验证 dotnet 是否可用
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Error: dotnet is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

# 验证项目文件是否存在
if (-not (Test-Path $project)) {
    Write-Host "Error: Project file not found: $project" -ForegroundColor Red
    exit 1
}

# 验证架构参数
if ($Arch -ne "x64") {
    Write-Host "Error: Invalid architecture '$Arch'. Supported: x64" -ForegroundColor Red
    exit 1
}

Write-Host "Publishing for Windows $Arch..." -ForegroundColor Cyan

dotnet publish $project -c $config -f net10.0-windows10.0.17763.0 -r win-$Arch --self-contained true -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`nPublish complete!" -ForegroundColor Green
Write-Host ""
Write-Host "运行方式:"
Write-Host "  .\$outputDir\liangwengu.exe"
