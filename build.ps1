$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$taskOutput = Join-Path $PSScriptRoot 'build/ImageZipMerger-dev'
New-Item -ItemType Directory -Force $taskOutput | Out-Null
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /target:winexe "/out:$taskOutput\ImageZipMerger.exe" /win32icon:assets/ImageZipMerger.ico /resource:assets/ImageZipMerger.ico,ImageZipMerger.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ImageZipMerger.cs ModernImage.cs EpisodeListBox.cs
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$taskDeps = Join-Path $PSScriptRoot 'build/dependencies'
New-Item -ItemType Directory -Force $taskDeps | Out-Null
& npm.cmd install --prefix $taskDeps --ignore-scripts --no-audit --no-fund --no-package-lock '@img/sharp-win32-x64@0.35.4'
if ($LASTEXITCODE -ne 0) { throw 'Image library download failed' }
$taskPackage = Join-Path $taskDeps 'node_modules/@img/sharp-win32-x64'
Copy-Item (Join-Path $taskPackage 'lib/libvips-42.dll') $taskOutput
Copy-Item (Join-Path $taskPackage 'LICENSE') (Join-Path $taskOutput 'THIRD-PARTY-LICENSE.txt')
Copy-Item README.md $taskOutput
Copy-Item '사용방법.txt',ImageZipMerger.cs,ModernImage.cs,EpisodeListBox.cs $taskOutput
Compress-Archive -Path $taskOutput -DestinationPath 'build/ImageZipMerger-dev.zip' -Force
