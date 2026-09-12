param([switch]$Package)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$projectPath = Join-Path $root 'DadsStorage.csproj'
$packageRoot = Join-Path $root 'package'
$dllPath = Join-Path $root 'bin\Release\net48\DadsStorage.dll'
$manifestPath = Join-Path $packageRoot 'manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

dotnet build $projectPath -c Release
if ($LASTEXITCODE -ne 0) { throw "DadsStorage source build exited with code $LASTEXITCODE" }

$assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($dllPath)
$referencedAssemblies = $assembly.GetReferencedAssemblies().Name
foreach ($forbiddenReference in @('AzuAutoStore', 'AzuExtendedPlayerInventory', 'ServerSync', 'Backpacks', 'YamlDotNet')) {
    if ($referencedAssemblies -contains $forbiddenReference) {
        throw "DadsStorage contains an unexpected external assembly reference: $forbiddenReference"
    }
}
if ($assembly.GetName().Name -ne 'DadsStorage') {
    throw "Unexpected assembly name: $($assembly.GetName().Name)"
}

if ($Package) {
    if ($manifest.name -notmatch '^[A-Za-z0-9_]+$') {
        throw "Thunderstore package name is invalid: $($manifest.name)"
    }
    if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') {
        throw "Thunderstore version_number must use MAJOR.MINOR.PATCH: $($manifest.version_number)"
    }
    if ([string]::IsNullOrWhiteSpace($manifest.description) -or $manifest.description.Length -gt 250) {
        throw 'Thunderstore description must contain 1-250 characters.'
    }
    if ($null -eq $manifest.dependencies) {
        throw 'Thunderstore dependencies must be present in manifest.json.'
    }

    $packageEntries = [ordered]@{
        'DadsStorage.dll' = $dllPath
        'manifest.json' = $manifestPath
        'README.md' = (Join-Path $packageRoot 'README.md')
        'icon.png' = (Join-Path $packageRoot 'icon.png')
        'CHANGELOG.md' = (Join-Path $root 'CHANGELOG.md')
        'LICENSE' = (Join-Path $root 'LICENSE')
        'NOTICE' = (Join-Path $root 'NOTICE')
    }
    foreach ($sourcePath in $packageEntries.Values) {
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Required package file is missing: $sourcePath"
        }
    }

    Add-Type -AssemblyName System.Drawing
    $icon = [System.Drawing.Image]::FromFile($packageEntries['icon.png'])
    try {
        if ($icon.Width -ne 256 -or $icon.Height -ne 256) {
            throw "Thunderstore icon.png must be exactly 256x256; found $($icon.Width)x$($icon.Height)."
        }
        if ($icon.RawFormat.Guid -ne [System.Drawing.Imaging.ImageFormat]::Png.Guid) {
            throw 'Thunderstore icon.png is not a PNG image.'
        }
    }
    finally {
        $icon.Dispose()
    }

    $assemblyVersion = $assembly.GetName().Version
    $assemblySemVer = "$($assemblyVersion.Major).$($assemblyVersion.Minor).$($assemblyVersion.Build)"
    if ($assemblySemVer -ne $manifest.version_number) {
        throw "Assembly version $assemblySemVer does not match manifest version $($manifest.version_number)."
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $distRoot = Join-Path $root 'dist'
    $artifactArchiveRoot = Join-Path $root 'Archive\package-builds'
    New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $artifactArchiveRoot -Force | Out-Null

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    foreach ($artifact in Get-ChildItem -LiteralPath $distRoot -Force) {
        $archivedPath = Join-Path $artifactArchiveRoot "$($artifact.BaseName)-$stamp$($artifact.Extension)"
        if (Test-Path -LiteralPath $archivedPath) {
            throw "Package archive target already exists: $archivedPath"
        }
        Move-Item -LiteralPath $artifact.FullName -Destination $archivedPath
    }

    $folderPath = Join-Path $distRoot "DadsStorage-$($manifest.version_number)"
    New-Item -ItemType Directory -Path $folderPath | Out-Null
    foreach ($entry in $packageEntries.GetEnumerator()) {
        Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $folderPath $entry.Key)
    }

    $zipPath = Join-Path $distRoot "DadsStorage-$($manifest.version_number).zip"
    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $packageEntries.GetEnumerator()) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip,
                $entry.Value,
                $entry.Key,
                [System.IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
    }
    finally {
        $zip.Dispose()
    }

    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entryNames = @($zip.Entries | ForEach-Object FullName)
        foreach ($requiredEntry in $packageEntries.Keys) {
            if ($requiredEntry -notin $entryNames) {
                throw "Package ZIP is missing required root entry: $requiredEntry"
            }
        }
        if ($entryNames | Where-Object { $_ -match '[/\\]' }) {
            throw 'Package ZIP contains a nested directory.'
        }
        if ($entryNames | Where-Object { $_ -like 'AzuAutoStore*' -or $_ -like 'AzuExtendedPlayerInventory*' }) {
            throw 'Package ZIP contains an Azu assembly or file.'
        }
    }
    finally {
        $zip.Dispose()
    }

    Write-Host "Thunderstore package: $zipPath"
    Write-Host "SHA256: $((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash)"
}
