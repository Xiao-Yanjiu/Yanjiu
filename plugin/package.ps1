param([string]$ProjectRoot=(Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),[string]$ArtifactPath='',[string]$Tag='')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(!$ArtifactPath){$ArtifactPath=Join-Path $ProjectRoot 'artifacts/plugin'}
$manifest=Get-Content -LiteralPath "$ArtifactPath/Yanjiu.json" -Raw | ConvertFrom-Json
$version=[Version]$manifest.AssemblyVersion
$versionLabel="$($version.Major).$($version.Minor).$($version.Build)"
if($version.Revision -gt 0){$versionLabel += ".$($version.Revision)"}
if(!$Tag){$Tag="v$versionLabel-test.1"}
$hostHash=(Get-FileHash -LiteralPath "$ArtifactPath/Yanjiu.dll").Hash
if([Reflection.AssemblyName]::GetAssemblyName("$ArtifactPath/Yanjiu.dll").Version -ne $version){throw 'Manifest and assembly version differ'}
foreach($source in @('DR','Omni')){
 $moduleRoot=Join-Path $ArtifactPath "$source/Modules"
 if((Get-Content -LiteralPath "$moduleRoot/host.sha256" -Raw).Trim() -ne $hostHash){throw "$source host checksum differs"}
 foreach($item in @(Get-Content -LiteralPath "$moduleRoot/catalog.json" -Raw | ConvertFrom-Json)){
  if($item.id -notmatch '^[A-Za-z][A-Za-z0-9_]{0,127}$'){throw 'Invalid module id'}
  if($item.compiles -and $item.importable -and (Get-FileHash -LiteralPath "$moduleRoot/$($item.id).dll").Hash -ne $item.sha256){throw "Module hash differs: $($item.id)"}
 }
}
Copy-Item -LiteralPath "$repo/LICENSE" -Destination "$ArtifactPath/LICENSE" -Force
Copy-Item -LiteralPath "$PSScriptRoot/README.md" -Destination "$ArtifactPath/README.md" -Force
New-Item -ItemType Directory -Force -Path "$ArtifactPath/licenses" | Out-Null
Copy-Item -Path "$PSScriptRoot/licenses/*","$repo/third_party/licenses/*" -Destination "$ArtifactPath/licenses" -Force
@("Corresponding source: https://github.com/Xiao-Yanjiu/Yanjiu/tree/$Tag","Source archive: https://github.com/Xiao-Yanjiu/Yanjiu/releases/download/$Tag/Yanjiu-$versionLabel-source.zip","Rebuild: plugin/tools/rebuild-release.ps1; full third-party source: third_party/sources/","Development test build $version; individual upstream modules have not all been verified in-game.") | Set-Content -LiteralPath "$ArtifactPath/SOURCE.txt" -Encoding utf8
$destination=Join-Path $ProjectRoot "artifacts/Yanjiu-$versionLabel-test.zip"
New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
Compress-Archive -Path "$ArtifactPath/*" -DestinationPath $destination -Force
Get-Item -LiteralPath $destination | Select-Object FullName,Length
Get-FileHash -LiteralPath $destination
