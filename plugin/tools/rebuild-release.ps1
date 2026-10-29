param([Parameter(Mandatory=$true)][string]$DalamudPath,[string]$DotnetPath='dotnet',[string]$WorkDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(!$WorkDirectory){$WorkDirectory=Join-Path $repo 'local/release-work'}
$work=[IO.Path]::GetFullPath($WorkDirectory)
$vendor=Join-Path $repo 'third_party/sources'
$output=Join-Path $work 'staging/plugin'
$audit=Join-Path $work 'dr-compatibility-integrated'
New-Item -ItemType Directory -Force -Path "$work/dr-integration/upstream","$work/dr-integration/dependencies" | Out-Null
Copy-Item -Path "$vendor/DailyRoutines.ModulesPublic/*" -Destination "$work/dr-integration/upstream" -Recurse -Force
Copy-Item -LiteralPath "$vendor/DR-source-manifest.json" -Destination "$work/dr-integration/source-manifest.json" -Force
Copy-Item -LiteralPath "$vendor/DRLocalizations/ChineseSimplified.json" -Destination "$work/dr-integration/ChineseSimplified.json" -Force
foreach($name in @('KamiToolKit','OmenTools.KamiToolKit')){Copy-Item -LiteralPath (Join-Path $vendor $name) -Destination "$work/dr-integration/dependencies" -Recurse -Force}
$env:DOTNET_CLI_HOME=Join-Path $work 'temporary/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $work 'tools/nuget'
$dotnetExecutable=(Get-Command $DotnetPath -ErrorAction Stop).Source
$dotnetRoot=Split-Path $dotnetExecutable -Parent
$generator=Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Filter System.Text.RegularExpressions.Generator.dll -Recurse | Sort-Object FullName -Descending | Select-Object -First 1
if(!$generator){throw 'Install the .NET 10 SDK reference packs (regex source generator required)'}
$env:YANJIU_REGEX_GENERATOR=$generator.FullName
& "$repo/plugin/build.ps1" -ProjectRoot $work -OutputDirectory $output -DotnetPath $dotnetExecutable -DalamudPath $DalamudPath
& $dotnetExecutable run --project "$repo/plugin/tools/DRCompatibilityAudit/DRCompatibilityAudit.csproj" -c Release "-p:YanjiuArtifactPath=$output" -- "$work/dr-integration/upstream" $DalamudPath $output $audit
if($LASTEXITCODE -ne 0){throw 'DR source audit failed'}
& $dotnetExecutable run --project "$repo/plugin/tools/ModulePackageCheck/ModulePackageCheck.csproj" -c Release "-p:YanjiuArtifactPath=$output" -- $output $DalamudPath $audit
if($LASTEXITCODE -ne 0){throw 'DR constructor bindings failed'}
& "$repo/plugin/tools/pack-dr.ps1" -ProjectRoot $work -AuditDirectory $audit -Source DR -HostDirectory $output
$emptyPackage=Join-Path $work 'omni-integration/package/Modules'
New-Item -ItemType Directory -Force -Path $emptyPackage | Out-Null
'[]' | Set-Content -LiteralPath (Join-Path $emptyPackage 'catalog.json') -Encoding utf8
(Get-FileHash -LiteralPath "$output/Yanjiu.dll").Hash | Set-Content -LiteralPath (Join-Path $emptyPackage 'host.sha256') -Encoding ascii
& "$repo/plugin/build.ps1" -ProjectRoot $work -OutputDirectory $output -DotnetPath $dotnetExecutable -DalamudPath $DalamudPath
$env:YANJIU_OFFLINE_TESTS='1'
& "$repo/plugin/tests/run-tests.ps1" -ProjectRoot $work -ArtifactPath $output -DotnetPath $dotnetExecutable -DalamudPath $DalamudPath
& $dotnetExecutable run --project "$repo/plugin/tools/Api1NativeRuntimeCheck/Api1NativeRuntimeCheck.csproj" -c Release "-p:YanjiuArtifactPath=$output" -- $output $DalamudPath
if($LASTEXITCODE -ne 0){throw "API 1 native runtime contract failed"}
Write-Output "Complete test release: $output"
