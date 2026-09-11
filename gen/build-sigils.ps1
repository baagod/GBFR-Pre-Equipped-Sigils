# 一条命令生成 sigils.xlsx（当前目录）与 sigils.json（规则见 gen\sigils.xlsx 生成文档.md）。实现只有 gen\build-sigils.js 一个文件，无中间产物。
# 前置：gen\extracted\gbfr.db（提取全表，见文档 §1）、gen\GBFRDataTools\Data\ids.txt。
# Usage: pwsh gen\build-sigils.ps1 [-Data <目录>] [-OutXlsx <文件>] [-OutJson <文件>]
[CmdletBinding()]
param(
    [string]$Data = (Join-Path $PSScriptRoot 'extracted'),
    [string]$OutXlsx = 'sigils.xlsx',   # 当前目录
    [string]$OutJson = (Join-Path (Split-Path $PSScriptRoot -Parent) 'GBFR.PreEquippedSigils\sigils.json')
)
$ErrorActionPreference = 'Stop'
& node (Join-Path $PSScriptRoot 'build-sigils.js') --data $Data --out-xlsx $OutXlsx --out-json $OutJson
if ($LASTEXITCODE -ne 0) { throw '生成失败' }
