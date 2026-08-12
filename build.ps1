param()

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$sourceDir = Join-Path $projectRoot 'src'
$buildDir = Join-Path $projectRoot 'build'

$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'The .NET Framework C# compiler (csc.exe) was not found.'
}

New-Item -ItemType Directory -Path $buildDir -Force | Out-Null

$iconBuilder = Join-Path $buildDir 'IconBuilder.exe'
$iconPath = Join-Path $buildDir 'KeyboardCoolDownLock.ico'
$appPath = Join-Path $buildDir 'KeyboardCoolDownLock.exe'
$componentPath = Join-Path $buildDir 'KeyboardLockComponent.dll'

& $compiler /nologo /target:exe /optimize+ "/out:$iconBuilder" /reference:System.Drawing.dll (Join-Path $sourceDir 'IconBuilder.cs')
if ($LASTEXITCODE -ne 0) { throw 'IconBuilder compilation failed.' }

& $iconBuilder $iconPath
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }

& $compiler /nologo /target:library /platform:anycpu /optimize+ "/out:$componentPath" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $sourceDir 'KeyboardLockComponent.cs')
if ($LASTEXITCODE -ne 0) { throw 'Component compilation failed.' }

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/win32icon:$iconPath" "/win32manifest:$(Join-Path $sourceDir 'app.manifest')" "/out:$appPath" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$componentPath" (Join-Path $sourceDir 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }

$testProcess = Start-Process -FilePath $appPath -ArgumentList '--self-test' -PassThru -Wait
if ($testProcess.ExitCode -ne 0) {
    throw "Self-test failed with exit code $($testProcess.ExitCode)."
}

Get-Item -LiteralPath $appPath | Select-Object FullName, Length, LastWriteTime
