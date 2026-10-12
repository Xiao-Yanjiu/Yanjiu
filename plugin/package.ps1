param([string]$ProjectRoot=(Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$output=Join-Path $ProjectRoot 'artifacts/plugin'
if(!(Test-Path -LiteralPath "$output/Yanjiu.dll")){throw 'Build the plugin first'}
Copy-Item -LiteralPath "$repo/LICENSE" -Destination "$output/LICENSE"
Copy-Item -LiteralPath "$PSScriptRoot/README.md" -Destination "$output/README.md"
Copy-Item -LiteralPath "$PSScriptRoot/licenses" -Destination "$output/licenses" -Recurse -Force
$qqLicenses=Join-Path $output 'licenses/QQ'
New-Item -ItemType Directory -Path $qqLicenses -Force | Out-Null
foreach($file in @('COPYING-GPL-3.0.txt','LICENSE-Apache-2.0.txt','protobuf-net-版权声明.txt','许可及构建说明.txt')){Copy-Item -LiteralPath "$repo/third_party/Lagrange-V1/$file" -Destination $qqLicenses}
$commit=(& git -c "safe.directory=$repo" -C $repo rev-parse HEAD).Trim()
"Corresponding source: https://github.com/Xiao-Yanjiu/Yanjiu/tree/$commit`nDevelopment test build 0.1.0; not verified in a live game." | Set-Content -LiteralPath "$output/SOURCE.txt" -Encoding utf8
$zip=Join-Path $ProjectRoot 'artifacts/Yanjiu-0.1.0-test.zip'
Compress-Archive -Path "$output/*" -DestinationPath $zip -Force
Get-Item -LiteralPath $zip | Select-Object FullName,Length
