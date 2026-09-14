$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskIconSource = [Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'assets/icon.png'))
$taskIconFrames = @()
try {
 foreach ($taskIconSize in 16,24,32,48,64,128,256) {
  $taskIconBitmap=[Drawing.Bitmap]::new($taskIconSize,$taskIconSize)
  $taskIconGraphics=[Drawing.Graphics]::FromImage($taskIconBitmap)
  $taskIconMemory=[IO.MemoryStream]::new()
  try {
   $taskIconGraphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $taskIconGraphics.DrawImage($taskIconSource,0,0,$taskIconSize,$taskIconSize)
   $taskIconBitmap.Save($taskIconMemory,[Drawing.Imaging.ImageFormat]::Png)
   $taskIconFrames+=@{Size=$taskIconSize;Bytes=$taskIconMemory.ToArray()}
  }finally {$taskIconGraphics.Dispose();$taskIconBitmap.Dispose();$taskIconMemory.Dispose()}
 }
 $taskIconStream=[IO.File]::Create((Join-Path $PSScriptRoot 'assets/ImageZipMerger.ico'))
 $taskIconWriter=[IO.BinaryWriter]::new($taskIconStream)
 try {
  $taskIconWriter.Write([uint16]0);$taskIconWriter.Write([uint16]1);$taskIconWriter.Write([uint16]$taskIconFrames.Count)
  $taskIconOffset=6+16*$taskIconFrames.Count
  foreach($taskIconFrame in $taskIconFrames){
   $taskIconDimension=if($taskIconFrame.Size -eq 256){0}else{$taskIconFrame.Size}
   $taskIconWriter.Write([byte]$taskIconDimension);$taskIconWriter.Write([byte]$taskIconDimension)
   $taskIconWriter.Write([uint16]0);$taskIconWriter.Write([uint16]1);$taskIconWriter.Write([uint16]32)
   $taskIconWriter.Write([uint32]$taskIconFrame.Bytes.Length);$taskIconWriter.Write([uint32]$taskIconOffset)
   $taskIconOffset+=$taskIconFrame.Bytes.Length
  }
  foreach($taskIconFrame in $taskIconFrames){$taskIconWriter.Write([byte[]]$taskIconFrame.Bytes)}
 }finally {$taskIconWriter.Dispose()}
}finally {$taskIconSource.Dispose()}
