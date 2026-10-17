param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$AuditDirectory,[ValidateSet("DR","Omni")][string]$Source="DR",[string]$HostDirectory="")
$ErrorActionPreference='Stop'
$report=Get-Content -LiteralPath (Join-Path $AuditDirectory 'compatibility.json') -Raw | ConvertFrom-Json
$manifest=Get-Content -LiteralPath (Join-Path $ProjectRoot (($Source.ToLowerInvariant())+"-integration/source-manifest.json")) -Raw | ConvertFrom-Json
if($report.moduleCount -ne $manifest.Count){throw 'Source catalog and audit count differ'}
$destination=Join-Path $ProjectRoot (($Source.ToLowerInvariant())+"-integration/package/Modules")
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$bindingPath=Join-Path $AuditDirectory "binding.json";$bindings=if(Test-Path -LiteralPath $bindingPath){@(Get-Content -LiteralPath $bindingPath -Raw | ConvertFrom-Json)}else{@()};
$excluded=@((Get-Content -LiteralPath (Join-Path $PSScriptRoot "excluded-modules.json") -Raw | ConvertFrom-Json).$Source)
$language=if($Source -eq "DR"){Get-Content (Join-Path $ProjectRoot "dr-integration/ChineseSimplified.json") -Raw|ConvertFrom-Json}else{$null}
$catalog=@(foreach($sourceItem in $manifest){
 if($sourceItem.id -in $excluded){continue}
 $result=@($report.modules | Where-Object Name -eq $sourceItem.id)
 if($result.Count -ne 1){throw "Audit missing/duplicate module: $($sourceItem.id)"}
 $row=$result[0];$hash=''
 if($row.Compiles){
  $binary=Join-Path $AuditDirectory "compiled/$($sourceItem.id).dll"
  if(!(Test-Path -LiteralPath $binary)){throw "Missing compiled binary: $($sourceItem.id)"}
  $hash=(Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash
  Copy-Item -LiteralPath $binary -Destination (Join-Path $destination "$($sourceItem.id).dll") -Force
 }
 $binding=$bindings | Where-Object id -eq $sourceItem.id | Select-Object -First 1
 if($row.Compiles -and (!$binding -or !$binding.importable)){throw "Required module failed constructor binding: $($sourceItem.id)"}
 $description=$sourceItem.description
 $settingLabels=@{}
 foreach($label in $row.SettingLabels.PSObject.Properties){$translated=$language.($label.Value);$settingLabels[$label.Name]=if($translated){$translated}else{$label.Value}}
 $relations=@{}
 if($Source -eq "DR"){
  $sourcePath=Join-Path (Join-Path $ProjectRoot "dr-integration/upstream") $row.Source
  $moduleText=Get-Content -LiteralPath $sourcePath -Raw
  if([string]::IsNullOrWhiteSpace($description)){
   $key=[regex]::Match($moduleText,'Description\s*=\s*Lang.Get\s*\(\s*"([^"]+)"').Groups[1].Value
   if($key){$description=$language.$key}
   $command=[regex]::Match($moduleText,'const\s+string\s+COMMAND\s*=\s*"([^"]+)"').Groups[1].Value
   if($command){$description= $description.Replace('{command}',$command)}
  }
  foreach($relation in @("ModulesPrerequisite","ModulesConflict","ModulesPair")){
   $match=[regex]::Match($moduleText,($relation+'\s*=\s*\[([\s\S]*?)\]'))
   $relations[$relation]=@([regex]::Matches($match.Groups[1].Value,'"([^"]+)"')|ForEach-Object {$_.Groups[1].Value})
  }
 }
 [ordered]@{settingLabels=$settingLabels;prerequisites=@($relations["ModulesPrerequisite"]);conflicts=@($relations["ModulesConflict"]);pairs=@($relations["ModulesPair"]);source=$Source;importable=if($binding){[bool]$binding.importable}else{$Source -eq "DR"};id=$sourceItem.id;title=$sourceItem.title;description=$description;category=$sourceItem.category;compiles=[bool]$row.Compiles;sha256=$hash;error=if($binding -and !$binding.importable -and $row.Compiles){$binding.error}else{($row.Errors | ForEach-Object {"$($_.File):$($_.Line) $($_.Message)"}) -join [Environment]::NewLine}}
})
if(!$HostDirectory){$HostDirectory=Join-Path $ProjectRoot 'artifacts/plugin'}
$hostHash=(Get-FileHash -LiteralPath (Join-Path $HostDirectory 'Yanjiu.dll') -Algorithm SHA256).Hash
Set-Content -LiteralPath (Join-Path $destination 'host.sha256') -Value $hostHash -Encoding ascii
ConvertTo-Json -InputObject @($catalog) -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'catalog.json') -Encoding utf8
Write-Output "Packed $($report.compilePass)/$($report.moduleCount) compile-checked modules. Runtime validated: 0."




foreach($id in $excluded){$stale=Join-Path $destination "$id.dll";if(Test-Path -LiteralPath $stale){Remove-Item -LiteralPath $stale}}
Write-Output "Included $($catalog.Count) $Source modules; excluded $($excluded.Count) by user request."




