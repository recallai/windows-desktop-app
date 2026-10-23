$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Use a 64-bit Windows 11 computer.' }
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $frameworkRoot 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The built-in .NET Framework compiler is missing. Enable .NET Framework 4.8 in Windows.' }
$library = Join-Path $projectRoot 'lib\ScreenRecorderLib.dll'
if (-not (Test-Path -LiteralPath $library)) { throw 'ScreenRecorderLib.dll is missing. Extract the complete ZIP before launching.' }
$binDir = Join-Path $projectRoot 'bin'
New-Item -ItemType Directory -Path $binDir -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Xml.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$compileArgs = @('/nologo','/target:winexe','/platform:x64','/langversion:5','/optimize+','/utf8output',('/out:' + (Join-Path $binDir 'MeetingRecorder.exe')),('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')),('/reference:' + $library))
foreach ($reference in $references) { $compileArgs += '/reference:' + (Join-Path $frameworkRoot $reference) }
$compileArgs += Join-Path $projectRoot 'RecorderApp.cs'
Write-Host 'Building Meeting Recorder...'
& $compiler @compileArgs 2>&1 | Tee-Object -FilePath (Join-Path $projectRoot 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'Build failed. See build.log in this folder.' }
Copy-Item -LiteralPath $library -Destination (Join-Path $binDir 'ScreenRecorderLib.dll') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'MainWindow.xaml') -Destination (Join-Path $binDir 'MainWindow.xaml') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'MeetingRecorder.exe.config') -Destination (Join-Path $binDir 'MeetingRecorder.exe.config') -Force
Write-Host 'Build complete.'
