#Requires -Version 7
# NuGetForUnity の CLI で Assets/packages.config の NuGet パッケージを Assets/Packages へ復元する。
# - CLI は .NET 9 向けなので、.NET 10 で動かすためにロールフォワードを許可する
# - Roslyn アナライザ/Source Generator の DLL は、Unity が通常のアセンブリとしても参照して型の二重定義(CS0433)を
#   起こすため、復元後に meta を「全プラットフォーム無効・明示参照のみ」に書き換える(RoslynAnalyzer ラベルは維持)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$env:DOTNET_ROLL_FORWARD = 'Major'
& nugetforunity restore $project
if ($LASTEXITCODE -ne 0) { throw "nugetforunity restore failed (exit $LASTEXITCODE)" }

$packages = Join-Path $project 'Assets\Packages'
$metas = Get-ChildItem $packages -Recurse -Filter '*.dll.meta' | Where-Object { $_.FullName -like '*analyzers*' }
foreach ($meta in $metas) {
    $guid = (Select-String -Path $meta.FullName -Pattern '^guid: ([0-9a-f]+)').Matches[0].Groups[1].Value
    $content = @"
fileFormatVersion: 2
guid: $guid
labels:
- RoslynAnalyzer
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 1
  validateReferences: 0
  platformData:
  - first:
      : Any
    second:
      enabled: 0
      settings:
        Exclude Editor: 1
        Exclude Linux64: 1
        Exclude OSXUniversal: 1
        Exclude Win: 1
        Exclude Win64: 1
  - first:
      Any: 
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
    Set-Content -Path $meta.FullName -Value $content -NoNewline -Encoding utf8
    "analyzer meta rewritten: " + $meta.FullName.Substring($project.Length + 1)
}
