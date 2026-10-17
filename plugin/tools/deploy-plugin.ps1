param([Parameter(Mandatory=$true)][string]$SourceDirectory,[Parameter(Mandatory=$true)][string]$DestinationDirectory,[Parameter(Mandatory=$true)][string]$BackupDirectory)
$ErrorActionPreference='Stop'
$sourceRoot=[IO.Path]::GetFullPath($SourceDirectory)
$targetRoot=[IO.Path]::GetFullPath($DestinationDirectory)
if($sourceRoot -eq $targetRoot){throw 'Source and destination must differ'}
$hostFile=Join-Path $sourceRoot 'Yanjiu.dll'
$hostDigest=(Get-FileHash -LiteralPath $hostFile -Algorithm SHA256).Hash
foreach($kind in @('DR','Omni')){
 $moduleRoot=Join-Path $sourceRoot "$kind/Modules"
 if((Get-Content -LiteralPath (Join-Path $moduleRoot 'host.sha256') -Raw).Trim() -ne $hostDigest){throw "$kind host checksum mismatch; audit and pack before deployment"}
 $catalog=@(Get-Content -LiteralPath (Join-Path $moduleRoot 'catalog.json') -Raw|ConvertFrom-Json)
 foreach($module in $catalog){
  if($module.id -notmatch '^[A-Za-z][A-Za-z0-9_]{0,127}$'){throw 'Invalid catalog identifier'}
  if($module.compiles -and $module.importable){$file=Join-Path $moduleRoot ($module.id+'.dll');if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $module.sha256){throw "Module checksum mismatch: $($module.id)"}}
 }
}
New-Item -ItemType Directory -Force -Path $BackupDirectory | Out-Null
if(Test-Path -LiteralPath $targetRoot){Copy-Item -LiteralPath $targetRoot -Destination (Join-Path $BackupDirectory 'plugin') -Recurse -Force}
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
$files=@(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse)
foreach($file in $files | Where-Object FullName -ne $hostFile){
 $relative=[IO.Path]::GetRelativePath($sourceRoot,$file.FullName);$destination=Join-Path $targetRoot $relative
 if((Test-Path -LiteralPath $destination -PathType Leaf) -and (Get-FileHash -LiteralPath $file.FullName).Hash -eq (Get-FileHash -LiteralPath $destination).Hash){continue}
 New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
 Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}
Copy-Item -LiteralPath $hostFile -Destination (Join-Path $targetRoot 'Yanjiu.dll') -Force
foreach($file in $files){$destination=Join-Path $targetRoot ([IO.Path]::GetRelativePath($sourceRoot,$file.FullName));if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash){throw "Post-deployment checksum mismatch: $destination"}}
Write-Output "Verified deployment: $($files.Count) files; host $hostDigest"
