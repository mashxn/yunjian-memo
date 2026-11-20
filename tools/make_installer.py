from pathlib import Path

root = Path(__file__).resolve().parent.parent

install = r'''param(
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
'''

uninstall = r'''param(
    [string]$Target = (Join-Path $env:LOCALAPPDATA 'Programs\YunJianMemo'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if (-not $Force) {
    $answer = Read-Host '确定卸载 云笺 · 桌面备忘录？安装目录和快捷方式会移入回收站 (y/N)'
    if ($answer -notin @('y', 'Y', 'yes', 'YES')) {
        Write-Host '已取消。' -ForegroundColor DarkGray
        exit 0
    }
}

Get-Process -Name YunJianMemo -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Add-Type -AssemblyName Microsoft.VisualBasic
$recycle = [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin
$dialogs = [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs

$paths = @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) '云笺备忘录.lnk'),
    (Join-Path (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs') '云笺备忘录.lnk')
)
foreach ($path in $paths) {
    if (Test-Path -LiteralPath $path) {
        [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile($path, $dialogs, $recycle)
        Write-Host "快捷方式已移入回收站：$path" -ForegroundColor DarkGray
    }
}

try {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    Remove-ItemProperty -Path $runKey -Name 'YunJianMemo' -ErrorAction SilentlyContinue
} catch { }

if (Test-Path -LiteralPath $Target) {
    [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($Target, $dialogs, $recycle)
    Write-Host "安装目录已移入回收站：$Target" -ForegroundColor Green
} else {
    Write-Host '安装目录不存在，可能已经卸载。' -ForegroundColor DarkGray
}

$data = Join-Path $env:LOCALAPPDATA 'YunJianMemo'
Write-Host "个人数据仍保留在：$data" -ForegroundColor Yellow
Write-Host '如需彻底清除，请手动确认后删除该目录。' -ForegroundColor DarkGray
'''

(root / 'install.ps1').write_text(install, encoding='utf-8-sig')
(root / 'uninstall.ps1').write_text(uninstall, encoding='utf-8-sig')
print('install.ps1, uninstall.ps1 generated with UTF-8 BOM')
