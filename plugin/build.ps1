param([string]$ProjectRoot=(Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),[string]$DalamudPath='',[string]$DotnetPath='')
$ErrorActionPreference='Stop'
$sdk=if($DotnetPath){$DotnetPath}else{Join-Path $ProjectRoot 'tools/dotnet/dotnet.exe'}
if(!(Test-Path -LiteralPath $sdk)){$sdk=(Get-Command dotnet -ErrorAction Stop).Source}
if(!$DalamudPath){$hooks=Join-Path $env:APPDATA 'XIVLauncherCN/addon/Hooks';$DalamudPath=(Get-ChildItem -LiteralPath $hooks -Directory | Where-Object {Test-Path -LiteralPath (Join-Path $_.FullName 'Dalamud.dll')} | Sort-Object Name -Descending | Select-Object -First 1).FullName}
if(!$DalamudPath -or !(Test-Path -LiteralPath (Join-Path $DalamudPath 'Dalamud.dll'))){throw 'Provide -DalamudPath pointing to API 15 libraries'}
$env:DOTNET_CLI_HOME=Join-Path $ProjectRoot 'temporary/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $ProjectRoot 'tools/nuget'
$env:DOTNET_NOLOGO='true';$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='true'
$output=Join-Path $ProjectRoot 'artifacts/plugin'
$dep=Join-Path (Split-Path $PSScriptRoot -Parent) 'third_party/plugin-runtime'
$checks=Get-Content -LiteralPath (Join-Path $dep 'SHA256.json') -Raw | ConvertFrom-Json
foreach($check in $checks){$actual=(Get-FileHash -LiteralPath (Join-Path $dep $check.File) -Algorithm SHA256).Hash;if($actual -ne $check.Sha256){throw "Dependency checksum mismatch: $($check.File)"}}
& $sdk build (Join-Path $PSScriptRoot 'Yanjiu/Yanjiu.csproj') -c Release --nologo "-p:DalamudLibPath=$DalamudPath" "-p:YanjiuDependencyPath=$dep" "-p:BaseIntermediateOutputPath=$ProjectRoot/temporary/obj/" "-p:BaseOutputPath=$ProjectRoot/artifacts/bin/" -o $output
if($LASTEXITCODE -ne 0){throw 'Yanjiu plugin build failed'}
Write-Output "Plugin: $output/Yanjiu.dll"
