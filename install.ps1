param(
    [ValidateRange(1, 120)]
    [int]$Minutes = 15
)

$ErrorActionPreference = 'Stop'
$sourceExe = Join-Path $PSScriptRoot 'build\KeyboardCoolDownLock.exe'
if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Build output not found: $sourceExe"
}

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\KeyboardCoolDownLock'
$installedExe = Join-Path $installDir 'KeyboardCoolDownLock.exe'
$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutName = (-join ([char[]](0x952E, 0x76D8, 0x964D, 0x6E29, 0x9501))) + '.lnk'
$shortcutPath = Join-Path $desktop $shortcutName

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $installedExe -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $installedExe
$shortcut.Arguments = "--minutes $Minutes"
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$installedExe,0"
$shortcut.Description = "Lock keyboard only; mouse remains available; auto-unlock after $Minutes minutes"
$shortcut.Save()

[pscustomobject]@{
    InstalledExe = $installedExe
    Shortcut = $shortcutPath
    Minutes = $Minutes
}
