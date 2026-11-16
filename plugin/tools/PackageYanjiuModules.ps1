# SPDX-License-Identifier: GPL-3.0-or-later
param(
    [Parameter(Mandatory)][string]$PluginDirectory,
    [Parameter(Mandatory)][string]$ModuleDirectory
)
$ErrorActionPreference='Stop'
$pluginRoot=(Resolve-Path -LiteralPath $PluginDirectory).Path
$moduleRoot=(Resolve-Path -LiteralPath $ModuleDirectory).Path
$titles=[ordered]@{
    MultiToolbar='多功能工具栏'
    BetterUserMacro='更好的用户宏'
    BetterGlamourManagement='更好的幻化管理'
    AutoRetainer='自动雇员'
    QuickCrossWorldTravel='快捷跨界传送'
}
# Validate the complete input before changing the package.
$hostHash=(Get-FileHash -LiteralPath (Join-Path $pluginRoot 'Yanjiu.dll')).Hash
foreach($id in $titles.Keys){
    $file=Join-Path $moduleRoot "Yanjiu.Modules.$id.dll"
    if(!(Test-Path -LiteralPath $file -PathType Leaf)){throw "缺少模块：$file"}
    if((Get-Item -LiteralPath $file).Length -gt 4MB){throw "模块超过大小限制：$file"}
}
$destination=Join-Path $pluginRoot 'Yanjiu/Modules'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$catalog=@(foreach($id in $titles.Keys){
    $file="Yanjiu.Modules.$id.dll"
    Copy-Item -LiteralPath (Join-Path $moduleRoot $file) -Destination (Join-Path $destination $file)
    @{
        id=$id;title=$titles[$id];description='';source='Yanjiu';category='General'
        sha256=(Get-FileHash -LiteralPath (Join-Path $destination $file)).Hash
        compiles=$true;importable=$true;error=''
    }
})
$catalog | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $destination 'catalog.json') -Encoding utf8
Set-Content -LiteralPath (Join-Path $destination 'host.sha256') -Value $hostHash -NoNewline
Write-Output '已打包 5 个 Yanjiu 模块；原始 DLL 文件名保持不变。'
