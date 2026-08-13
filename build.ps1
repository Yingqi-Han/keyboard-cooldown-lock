param([switch]$LockedMode)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dotnet = $env:YINGQI_DOTNET
if (-not $dotnet) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
if (-not (Test-Path -LiteralPath $dotnet)) { throw 'Set YINGQI_DOTNET to a valid .NET 10 SDK dotnet.exe.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$build = [System.IO.Path]::GetFullPath((Join-Path $root 'build'))
$rootPrefix = [System.IO.Path]::GetFullPath($root).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $build.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe build path: $build" }
$solution = Join-Path $root 'KeyboardCooldownLock.slnx'
New-Item -ItemType Directory -Force -Path $build | Out-Null
Get-ChildItem -LiteralPath $build -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
$restoreArgs = @('restore', $solution)
if ($LockedMode) { $restoreArgs += '--locked-mode' }
& $dotnet @restoreArgs
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
& $dotnet build $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& $dotnet test (Join-Path $root 'tests\KeyboardLockComponent.Tests\KeyboardLockComponent.Tests.csproj') -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
& $dotnet publish (Join-Path $root 'src\KeyboardCooldownLock.App\KeyboardCooldownLock.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false --no-restore -o $build
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Get-ChildItem -LiteralPath $build -Filter '*.pdb' -File -ErrorAction SilentlyContinue | Remove-Item -Force
$test = Start-Process (Join-Path $build 'KeyboardCoolDownLock.exe') -ArgumentList '--self-test' -PassThru -Wait
if ($test.ExitCode -ne 0) { throw "Self-test failed: $($test.ExitCode)" }
$sessionTest = Start-Process (Join-Path $build 'KeyboardCoolDownLock.exe') -ArgumentList '--seconds', '6' -PassThru
$readyDeadline = [DateTime]::UtcNow.AddSeconds(15)
do {
    Start-Sleep -Milliseconds 100
    $sessionTest.Refresh()
} while (-not $sessionTest.HasExited -and $sessionTest.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $readyDeadline)
if ($sessionTest.HasExited -or $sessionTest.MainWindowHandle -eq 0 -or [string]::IsNullOrWhiteSpace($sessionTest.MainWindowTitle)) {
    $failureState = "HasExited=$($sessionTest.HasExited); ExitCode=$($sessionTest.ExitCode); Handle=$($sessionTest.MainWindowHandle); Title=$($sessionTest.MainWindowTitle)"
    if (-not $sessionTest.HasExited) { $sessionTest.CloseMainWindow() | Out-Null }
    throw "Session test failed: the visible recovery window did not become ready. $failureState"
}
if (-not $sessionTest.WaitForExit(10000)) {
    $sessionTest.CloseMainWindow() | Out-Null
    throw 'Session test failed: automatic unlock timed out.'
}
if ($sessionTest.ExitCode -ne 0) { throw "Session test failed: $($sessionTest.ExitCode)" }

# Keep one process locked while a second process verifies the named mutex, then
# terminate only the test-owned process and confirm Windows released its hook.
$ownerTest = Start-Process (Join-Path $build 'KeyboardCoolDownLock.exe') -ArgumentList '--seconds', '20' -PassThru
try {
    $ownerDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 100
        $ownerTest.Refresh()
    } while (-not $ownerTest.HasExited -and $ownerTest.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $ownerDeadline)
    if ($ownerTest.HasExited -or $ownerTest.MainWindowHandle -eq 0) { throw 'Single-instance test owner did not become ready.' }

    $contenderTest = Start-Process (Join-Path $build 'KeyboardCoolDownLock.exe') -ArgumentList '--seconds', '3' -PassThru -Wait
    if ($contenderTest.ExitCode -ne 2) { throw "Single-instance contender returned $($contenderTest.ExitCode), expected 2." }
}
finally {
    if (-not $ownerTest.HasExited) {
        Stop-Process -Id $ownerTest.Id -Force
        $ownerTest.WaitForExit()
    }
}
$postCrashTest = Start-Process (Join-Path $build 'KeyboardCoolDownLock.exe') -ArgumentList '--self-test' -PassThru -Wait
if ($postCrashTest.ExitCode -ne 0) { throw "Post-crash hook cleanup test failed: $($postCrashTest.ExitCode)" }
Get-ChildItem $build | Select-Object Name, Length, LastWriteTime
