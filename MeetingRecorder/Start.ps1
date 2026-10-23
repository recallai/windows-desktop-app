$ErrorActionPreference = 'Stop'
try {
 $projectRoot = $PSScriptRoot
 $buildNumber = [int](Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').CurrentBuildNumber
 if ($buildNumber -lt 22000) { throw 'This prototype requires Windows 11.' }
 if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64' -or $env:PROCESSOR_ARCHITEW6432 -eq 'ARM64') { throw 'This bundle targets x64 Windows 11.' }
 foreach ($runtimeName in @('vcruntime140.dll','vcruntime140_1.dll','msvcp140.dll','concrt140.dll')) {
  if (-not (Test-Path -LiteralPath (Join-Path $env:WINDIR ('System32\' + $runtimeName)))) { throw 'Install the Microsoft Visual C++ x64 Redistributable from https://aka.ms/vs/17/release/vc_redist.x64.exe, then run Start.cmd again.' }
 }
 if (-not (Test-Path -LiteralPath (Join-Path $env:WINDIR 'System32\mfplat.dll'))) { throw 'Windows Media Foundation is missing. On Windows N, install the Media Feature Pack in Optional Features, then restart Windows.' }
 & (Join-Path $projectRoot 'Build.ps1')
 $appPath = Join-Path $projectRoot 'bin\MeetingRecorder.exe'
 Start-Process -FilePath $appPath -WorkingDirectory (Join-Path $projectRoot 'bin')
} catch {
 Write-Host ''
 Write-Host $_ -ForegroundColor Red
 Write-Host 'If the app does not open, check build.log or bin\startup-error.log.'
 Read-Host 'Press Enter to close'
 exit 1
}
