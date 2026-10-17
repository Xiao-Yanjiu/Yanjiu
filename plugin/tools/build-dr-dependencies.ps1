param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$DalamudPath,[Parameter(Mandatory=$true)][string]$DotnetPath)
$ErrorActionPreference='Stop'
$source=Join-Path $ProjectRoot 'dr-integration/dependencies'
$out=Join-Path $ProjectRoot 'dr-dependencies/rebuilt'
New-Item -ItemType Directory -Force -Path $out | Out-Null
foreach($name in @('KamiToolKit','OmenTools.KamiToolKit')){
 $root=Join-Path $source $name
 if(!(Test-Path -LiteralPath (Join-Path $root 'LICENSE'))){throw "Source/license missing: $name"}
 $refs=@(Get-ChildItem -LiteralPath $DalamudPath -Filter '*.dll' | ForEach-Object {
  try{[void][Reflection.AssemblyName]::GetAssemblyName($_.FullName);'<Reference Include="'+$_.BaseName+'"><HintPath>'+[Security.SecurityElement]::Escape($_.FullName)+'</HintPath><Private>false</Private></Reference>'}catch{}
 })
 $omen=Join-Path $ProjectRoot 'artifacts/plugin/OmenTools.dll'
 $refs+='<Reference Include="OmenTools"><HintPath>'+[Security.SecurityElement]::Escape($omen)+'</HintPath><Private>false</Private></Reference>'
 if($name -eq 'OmenTools.KamiToolKit'){$refs+='<Reference Include="KamiToolKit"><HintPath>'+[Security.SecurityElement]::Escape((Join-Path $out 'KamiToolKit.dll'))+'</HintPath><Private>false</Private></Reference>'}
 $path=Join-Path $ProjectRoot "dr-dependencies/$name.rebuild.csproj"
 $escaped=[Security.SecurityElement]::Escape($root.Replace('\','/'))
 $xml='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0-windows</TargetFramework><UseWindowsForms>true</UseWindowsForms><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks><LangVersion>preview</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>'+$name+'</AssemblyName></PropertyGroup><ItemGroup>'+($refs -join '')+'<Compile Include="'+$escaped+'/**/*.cs" Exclude="'+$escaped+'/**/obj/**;'+$escaped+'/**/bin/**"/><Content Include="'+$escaped+'/Assets/**/*.png" CopyToOutputDirectory="PreserveNewest" Link="Assets/%(RecursiveDir)%(Filename)%(Extension)"/></ItemGroup></Project>'
 Set-Content -LiteralPath $path -Value $xml -Encoding utf8
 & $DotnetPath build $path -c Release -o $out "-p:BaseIntermediateOutputPath=$ProjectRoot/temporary/dr-rebuild-$name/" --nologo
 if($LASTEXITCODE -ne 0){throw "Dependency rebuild failed: $name"}
}
Write-Output "Public-source dependencies rebuilt into $out. Review before replacing pinned runtime libraries."
