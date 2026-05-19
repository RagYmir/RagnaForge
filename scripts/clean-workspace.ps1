param(
    [switch]$IncludeNodeModules,
    [switch]$CleanPublishOutput
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Assert-UnderRoot([string]$Path) {
    $full = [System.IO.Path]::GetFullPath($Path)
    $rootFull = [System.IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    if (-not ($full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing to clean outside project root: $full"
    }
    return $full
}

function Remove-DirectoryIfExists([string]$Path) {
    $full = Assert-UnderRoot $Path
    if (Test-Path -LiteralPath $full) {
        Remove-Item -LiteralPath $full -Recurse -Force
        Write-Host "Removed directory: $full"
    }
}

function Remove-FileIfExists([string]$Path) {
    $full = Assert-UnderRoot $Path
    if (Test-Path -LiteralPath $full) {
        Remove-Item -LiteralPath $full -Force
        Write-Host "Removed file: $full"
    }
}

function Clear-RuntimeDirectory([string]$Path, [string[]]$Extensions) {
    $full = Assert-UnderRoot $Path
    if (-not (Test-Path -LiteralPath $full)) {
        New-Item -ItemType Directory -Path $full -Force | Out-Null
    }

    foreach ($ext in $Extensions) {
        Get-ChildItem -LiteralPath $full -File -Recurse -Filter "*$ext" -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne ".gitkeep" } |
            ForEach-Object {
                Remove-Item -LiteralPath $_.FullName -Force
                Write-Host "Removed runtime file: $($_.FullName)"
            }
    }

    $keep = Join-Path $full ".gitkeep"
    if (-not (Test-Path -LiteralPath $keep)) {
        New-Item -ItemType File -Path $keep -Force | Out-Null
    }
}

foreach ($base in @("backend", "Agente_Setimmo\src", "Agente_Setimmo\tests")) {
    $basePath = Join-Path $root $base
    if (Test-Path -LiteralPath $basePath) {
        Get-ChildItem -LiteralPath $basePath -Directory -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -in @("bin", "obj") } |
            ForEach-Object { Remove-DirectoryIfExists $_.FullName }
    }
}

Remove-DirectoryIfExists (Join-Path $root "frontend\dist")
if ($IncludeNodeModules) {
    Remove-DirectoryIfExists (Join-Path $root "frontend\node_modules")
    Remove-DirectoryIfExists (Join-Path $root "node_modules")
}
if ($CleanPublishOutput) {
    Remove-DirectoryIfExists (Join-Path $root "dist\api")
    Remove-DirectoryIfExists (Join-Path $root "dist\agent")
}

Remove-DirectoryIfExists (Join-Path $root "TestResults")
Remove-DirectoryIfExists (Join-Path $root "coverage")
Remove-DirectoryIfExists (Join-Path $root "Agente_Setimmo\TestResults")

Get-ChildItem -LiteralPath $root -File -Recurse -Filter "*.trx" -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-FileIfExists $_.FullName }
Get-ChildItem -LiteralPath $root -File -Recurse -Filter "*.tsbuildinfo" -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-FileIfExists $_.FullName }

Clear-RuntimeDirectory (Join-Path $root "tmp") @(".json", ".md", ".log", ".tmp", ".dll", ".exe", ".pdb", ".deps.json", ".runtimeconfig.json")
Clear-RuntimeDirectory (Join-Path $root "data\cache") @(".json", ".md", ".log")
Clear-RuntimeDirectory (Join-Path $root "data\indexes") @(".json", ".md", ".log")
Clear-RuntimeDirectory (Join-Path $root "data\logs") @(".json", ".md", ".log")
Clear-RuntimeDirectory (Join-Path $root "data\backups") @(".json", ".md", ".log", ".bak")
Clear-RuntimeDirectory (Join-Path $root "Agente_Setimmo\cache") @(".json", ".md", ".log")
Clear-RuntimeDirectory (Join-Path $root "Agente_Setimmo\logs") @(".json", ".md", ".log")
Clear-RuntimeDirectory (Join-Path $root "Agente_Setimmo\reports") @(".json", ".md", ".log")

Write-Host "Workspace cleanup complete."
