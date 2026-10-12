param([string]$ProjectRoot=(Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent),[string]$DalamudPath,[string]$DotnetPath='')
$ErrorActionPreference='Stop'
$sdk=if($DotnetPath){$DotnetPath}else{Join-Path $ProjectRoot 'tools/dotnet/dotnet.exe'}
if(!(Test-Path -LiteralPath $sdk)){$sdk=(Get-Command dotnet -ErrorAction Stop).Source}
if(!$DalamudPath){throw 'Provide -DalamudPath for API 15'}
$env:DOTNET_CLI_HOME="$ProjectRoot/temporary/dotnet-home";$env:NUGET_PACKAGES="$ProjectRoot/tools/nuget";$env:DOTNET_NOLOGO='true'
& $sdk build "$PSScriptRoot/Yanjiu.Tests.csproj" -c Release -o "$ProjectRoot/tests/bin" "-p:BaseIntermediateOutputPath=$ProjectRoot/temporary/source-tests-obj/" "-p:YanjiuArtifactPath=$ProjectRoot/artifacts/plugin" "-p:DalamudLibPath=$DalamudPath" --nologo
if($LASTEXITCODE -ne 0){throw 'Test build failed'}
& $sdk "$ProjectRoot/tests/bin/Yanjiu.Tests.dll" $ProjectRoot $DalamudPath
if($LASTEXITCODE -ne 0){throw 'Tests failed'}
