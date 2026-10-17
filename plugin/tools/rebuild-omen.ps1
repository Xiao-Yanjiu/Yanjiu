param([Parameter(Mandatory=$true)][string]$DalamudPath,[string]$DotnetPath='dotnet',[string]$WorkDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(!$WorkDirectory){$WorkDirectory=Join-Path $repo 'local/omen-rebuild'}
New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null
$source=Join-Path $repo 'third_party/sources/OmenTools'
$paths=@(Get-ChildItem -LiteralPath $DalamudPath -Filter '*.dll')+@(Get-ChildItem -LiteralPath "$repo/third_party/plugin-runtime" -Filter '*.dll' | Where-Object BaseName -ne 'OmenTools')
$unique=@{};foreach($file in $paths){try{$name=[Reflection.AssemblyName]::GetAssemblyName($file.FullName).Name;$unique[$name]=$file.FullName}catch{}}
$references=@(foreach($name in $unique.Keys){'<Reference Include="'+$name+'"><HintPath>'+[Security.SecurityElement]::Escape($unique[$name])+'</HintPath><Private>false</Private></Reference>'})
$escaped=[Security.SecurityElement]::Escape($source.Replace('\','/'))
$xml='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks><LangVersion>preview</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>OmenTools</AssemblyName><Version>1.0.0</Version><DebugType>none</DebugType><Optimize>true</Optimize></PropertyGroup><ItemGroup>'+($references -join '')+'<Compile Include="'+$escaped+'/**/*.cs" Exclude="'+$escaped+'/**/obj/**;'+$escaped+'/**/bin/**"/></ItemGroup></Project>'
$project=Join-Path $WorkDirectory 'OmenTools.csproj'
$xml | Set-Content -LiteralPath $project -Encoding utf8
& $DotnetPath build $project -c Release -o "$WorkDirectory/built" --nologo
if($LASTEXITCODE -ne 0){throw 'OmenTools rebuild failed'}
Write-Output 'Review rebuilt DLL, update pinned hashes, and rebuild the complete release before deployment.'
