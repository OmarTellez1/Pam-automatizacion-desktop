[CmdletBinding()]
param(
    [switch]$ReplaceExisting
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotNetCommand {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Host ""
    Write-Host ("dotnet " + ($Arguments -join ' ')) -ForegroundColor Cyan
    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "El comando dotnet terminó con el código $LASTEXITCODE. No se generó una publicación válida."
    }
}

$projectRoot = Split-Path -Parent $PSCommandPath
$projectFile = Join-Path $projectRoot 'PamAutomation.App\PamAutomation.App.csproj'
$artifactsRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $projectRoot 'artifacts\publish\final'))

Push-Location $projectRoot

try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'No se encontró dotnet en PATH. Instala o habilita el SDK x64 de .NET 10.'
    }

    [xml]$projectDocument = Get-Content -Raw -LiteralPath $projectFile
    $version = [string]($projectDocument.Project.PropertyGroup.Version | Select-Object -First 1)

    if ($version -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version debe tener el formato numérico X.Y.Z. Valor encontrado: '$version'."
    }

    $expectedWindowsVersion = "$version.0"
    # Validate the published metadata against the version declared by this branch.

    $outputDirectory = [System.IO.Path]::GetFullPath(
        (Join-Path $artifactsRoot "PAM-Automation-$version"))
    $archivePath = [System.IO.Path]::GetFullPath(
        (Join-Path $artifactsRoot "PAM-Automation-$version-win-x64.zip"))
    $requiredPrefix = $artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar

    if (-not $outputDirectory.StartsWith(
            $requiredPrefix,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $archivePath.StartsWith(
            $requiredPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'La ruta de publicación calculada salió del directorio de artefactos permitido.'
    }

    if (Test-Path -LiteralPath $outputDirectory) {
        if (-not $ReplaceExisting) {
            throw "Ya existe '$outputDirectory'. Incrementa la versión o ejecuta con -ReplaceExisting."
        }

        Write-Host "Eliminando únicamente la publicación existente de la versión $version..." -ForegroundColor Yellow
        Remove-Item -LiteralPath $outputDirectory -Recurse -Force
    }

    if (Test-Path -LiteralPath $archivePath) {
        if (-not $ReplaceExisting) {
            throw "Ya existe '$archivePath'. Incrementa la versión o ejecuta con -ReplaceExisting."
        }

        Remove-Item -LiteralPath $archivePath -Force
    }

    Write-Host "Generando PAM Automation V. $version" -ForegroundColor Green

    Invoke-DotNetCommand @(
        'restore',
        'PamAutomation.slnx',
        '-r', 'win-x64',
        '-p:Platform=x64')

    Invoke-DotNetCommand @(
        'build',
        'PamAutomation.slnx',
        '-c', 'Release',
        '-p:Platform=x64',
        '--no-restore')

    Invoke-DotNetCommand @(
        'run',
        '--project', 'PamAutomation.Tests\PamAutomation.Tests.csproj',
        '-c', 'Release',
        '-p:Platform=x64',
        '--no-build')

    Invoke-DotNetCommand @(
        'publish',
        'PamAutomation.App\PamAutomation.App.csproj',
        '-c', 'Release',
        '-p:Platform=x64',
        '-p:PublishProfile=Portable-x64',
        '--no-restore',
        '-o', $outputDirectory)

    $executablePath = Join-Path $outputDirectory 'PAM Automation.exe'
    if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
        throw 'La publicación no contiene PAM Automation.exe.'
    }

    $executable = Get-Item -LiteralPath $executablePath
    $publishedFiles = @(Get-ChildItem -LiteralPath $outputDirectory -File -Recurse)
    # This branch publishes the executable and its dependencies together, with the
    # original relative paths of Playwright preserved inside .playwright.
    foreach ($dependency in @(
        'PAM Automation.dll', 'PAM Automation.deps.json', 'PAM Automation.runtimeconfig.json',
        'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll', 'PresentationFramework.dll',
        'Microsoft.Playwright.dll', 'PamAutomation.Outlook.dll',
        '.playwright\node\win32_x64\node.exe', '.playwright\package\cli.js')) {
        if (-not (Test-Path -LiteralPath (Join-Path $outputDirectory $dependency) -PathType Leaf)) {
            throw "Falta una dependencia del paquete portable: $dependency."
        }
    }
    $fileInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable.FullName)
    if ($fileInfo.ProductVersion -ne $version -or $fileInfo.FileVersion -ne $expectedWindowsVersion) {
        throw 'Los metadatos del ejecutable no coinciden con la versión solicitada.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $outputDirectory,
        $archivePath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $true)

    $archive = Get-Item -LiteralPath $archivePath
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $archive.FullName

    Write-Host ""
    Write-Host 'Publicación completada correctamente.' -ForegroundColor Green
    Write-Host "Carpeta portable: $outputDirectory"
    Write-Host "Paquete para distribuir: $($archive.FullName)"
    Write-Host "Versión: $version"
    Write-Host "Archivos incluidos: $($publishedFiles.Count)"
    Write-Host "Tamaño ZIP: $($archive.Length) bytes"
    Write-Host "SHA-256 del ZIP: $($hash.Hash)"
    Write-Host ""
    Write-Host 'Siguiente paso manual: adjuntar el ZIP a un GitHub Release y actualizar latest.json al final.'
}
finally {
    Pop-Location
}
