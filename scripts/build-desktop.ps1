$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'desktop\src\WordstatCheck.Desktop\WordstatCheck.Desktop.csproj'
$output = Join-Path $projectRoot 'artifacts\win-x64'

dotnet test (Join-Path $projectRoot 'desktop\WordstatCheck.slnx') -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish $project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o $output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "WORDSTATCHEK.exe: $output"
