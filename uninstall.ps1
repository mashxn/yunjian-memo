param(
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
