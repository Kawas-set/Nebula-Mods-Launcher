param(
    [string]$IsccPath,
    [string]$PublishDirRelative = "..\artifacts\publish-release-current",
    [switch]$SkipPublish
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$issPath = Join-Path $scriptDir "ModLauncher.iss"
$projectPath = Join-Path $repoRoot "ModLauncher.csproj"
$publishDir = Resolve-Path (Join-Path $scriptDir $PublishDirRelative) -ErrorAction SilentlyContinue
$publishDirPath = if ($publishDir) { $publishDir.Path } else { Join-Path $scriptDir $PublishDirRelative }

if (-not $IsccPath) {
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    $IsccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $IsccPath) {
    throw "ISCC.exe not found. Install Inno Setup 6 and rerun this script."
}

if (-not (Test-Path $issPath)) {
    throw "Installer script not found: $issPath"
}

if (-not $SkipPublish) {
    if (-not (Test-Path $projectPath)) {
        throw "Project file not found: $projectPath"
    }

    Write-Host "Publishing fresh release build to $publishDirPath"
    dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $publishDirPath -nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish exited with code $LASTEXITCODE"
    }
}
elseif (-not (Test-Path $publishDirPath)) {
    throw "Publish directory not found: $publishDirPath"
}

Push-Location $scriptDir
try {
    Write-Host "Building installer from $issPath"
    & $IsccPath "/DMyPublishDir=$PublishDirRelative" $issPath
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC exited with code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
