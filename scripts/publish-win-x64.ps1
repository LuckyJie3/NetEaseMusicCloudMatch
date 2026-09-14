[CmdletBinding()]
param(
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'src\NeteaseMusicCloudMatch.App\NeteaseMusicCloudMatch.App.csproj'
$outputPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\win-x64'))
$allowedPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

if (-not $outputPath.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "发布目录不在仓库内部，已拒绝继续：$outputPath"
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

$arguments = @(
    'publish',
    $projectPath,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--output', $outputPath,
    "--self-contained:$(-not $FrameworkDependent)",
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PublishTrimmed=false',
    '-p:DebugType=embedded'
)

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败，退出码：$LASTEXITCODE"
}

$executable = Join-Path $outputPath 'NeteaseMusicCloudMatch.App.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "发布完成但未找到可执行文件：$executable"
}

Write-Host "发布成功：$outputPath"
