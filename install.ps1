param(
    [string]$Target = (Join-Path $env:LOCALAPPDATA 'Programs\YunJianMemo'),
    [switch]$NoLaunch,
    [switch]$NoShortcut
)

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'dist\YunJianMemo'
$exeName = 'YunJianMemo.exe'

if (-not (Test-Path -LiteralPath (Join-Path $source $exeName))) {
    throw "找不到发布文件：$source"
}

Write-Host '正在安装 云笺 · 桌面备忘录…' -ForegroundColor Cyan

Get-Process -Name YunJianMemo -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

if (Test-Path -LiteralPath $Target) {
    $backup = "$Target.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Move-Item -LiteralPath $Target -Destination $backup
    Write-Host "旧版本已保留：$backup" -ForegroundColor DarkGray
}

New-Item -ItemType Directory -Force -Path $Target | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $Target -Recurse -Force

$exe = Join-Path $Target $exeName

if (-not $NoShortcut) {
    $shell = New-Object -ComObject WScript.Shell
    $desktop = [Environment]::GetFolderPath('Desktop')
    $startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'

    $linkPath = Join-Path $desktop '云笺备忘录.lnk'
    if (Test-Path -LiteralPath $linkPath) {
        Move-Item -LiteralPath $linkPath -Destination "$linkPath.bak-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    }
    $shortcut = $shell.CreateShortcut($linkPath)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $Target
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Description = '云笺 · 桌面备忘录'
    $shortcut.Save()

    $startLink = Join-Path $startMenu '云笺备忘录.lnk'
    if (Test-Path -LiteralPath $startLink) {
        Move-Item -LiteralPath $startLink -Destination "$startLink.bak-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    }
    $shortcut2 = $shell.CreateShortcut($startLink)
    $shortcut2.TargetPath = $exe
    $shortcut2.WorkingDirectory = $Target
    $shortcut2.IconLocation = "$exe,0"
    $shortcut2.Description = '云笺 · 桌面备忘录'
    $shortcut2.Save()
    Write-Host '已创建桌面和开始菜单快捷方式。' -ForegroundColor DarkGray
}

Write-Host "安装完成：$Target" -ForegroundColor Green
Write-Host '首次运行请在设置中开启「开机自启」（可选）。' -ForegroundColor DarkGray

if (-not $NoLaunch) {
    Start-Process -FilePath $exe
}
