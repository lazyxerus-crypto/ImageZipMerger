$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
New-Item -ItemType Directory -Force build | Out-Null
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /target:exe /main:ReorderTests /out:build/ReorderTests.exe /win32icon:assets/ImageZipMerger.ico /resource:assets/ImageZipMerger.ico,ImageZipMerger.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ImageZipMerger.cs ModernImage.cs EpisodeListBox.cs ReorderTests.cs WorkflowTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& ./build/ReorderTests.exe
if ($LASTEXITCODE -ne 0) { throw 'Reorder regression tests failed' }
