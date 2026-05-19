param(
    [string]$ZipPath = "C:\Users\Allis\Desktop\Ragna_Forge_release.zip",
    [switch]$IncludeGit,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$staging = Join-Path $root "tmp\package-clean-staging"
Add-Type -AssemblyName System.IO.Compression.FileSystem

& (Join-Path $root "scripts\clean-workspace.ps1") -IncludeNodeModules -CleanPublishOutput
if (-not $SkipPublish) {
    & (Join-Path $root "scripts\publish-all.ps1")
}

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
New-Item -ItemType Directory -Path $staging -Force | Out-Null

$skipDirNames = @("bin", "obj", "node_modules", "TestResults", "coverage", ".codex", ".antigravity")
$skipRelPrefixes = @(
    "frontend\dist",
    "tmp",
    "data\cache",
    "data\indexes",
    "data\logs",
    "data\backups",
    "Agente_Setimmo\cache",
    "Agente_Setimmo\logs",
    "Agente_Setimmo\reports",
    "Agente_Setimmo\knowledge\index"
)
$skipExtensions = @(".trx", ".tsbuildinfo", ".zip", ".rar", ".7z", ".grf", ".gpf", ".thor", ".spr", ".act", ".bmp", ".tga", ".rsw", ".gnd", ".gat", ".rsm", ".pal")
$skipFileNames = @("repositories.local.json", "paths.json", "test_output.txt", "tests_output.txt")

function Rel([string]$Path) {
    return $Path.Substring($root.Length).TrimStart('\')
}

function ShouldSkipDir($Dir) {
    if ($Dir.Name -eq ".git" -and -not $IncludeGit) { return $true }
    if ($skipDirNames -contains $Dir.Name) { return $true }
    $rel = Rel $Dir.FullName
    foreach ($prefix in $skipRelPrefixes) {
        if ($rel -eq $prefix -or $rel.StartsWith($prefix + "\", [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function ShouldSkipFile($File) {
    if ($skipFileNames -contains $File.Name) { return $true }
    if ($File.Name -like ".env*" -and $File.Name -ne ".env.example") { return $true }
    if ($skipExtensions -contains $File.Extension.ToLowerInvariant()) { return $true }
    return $false
}

$stack = New-Object System.Collections.Stack
$stack.Push((Get-Item -LiteralPath $root))
while ($stack.Count -gt 0) {
    $dir = $stack.Pop()
    foreach ($child in Get-ChildItem -Force -LiteralPath $dir.FullName) {
        if ($child.FullName.StartsWith($staging, [StringComparison]::OrdinalIgnoreCase)) { continue }
        if ($child.PSIsContainer) {
            if (ShouldSkipDir $child) { continue }
            $rel = Rel $child.FullName
            New-Item -ItemType Directory -Path (Join-Path $staging $rel) -Force | Out-Null
            $stack.Push($child)
        } else {
            if (ShouldSkipFile $child) { continue }
            $rel = Rel $child.FullName
            $target = Join-Path $staging $rel
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Copy-Item -LiteralPath $child.FullName -Destination $target -Force
        }
    }
}

foreach ($keepDir in @("tmp", "data\cache", "data\indexes", "data\logs", "data\backups", "Agente_Setimmo\cache", "Agente_Setimmo\logs", "Agente_Setimmo\reports", "Agente_Setimmo\knowledge\index")) {
    $dir = Join-Path $staging $keepDir
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    New-Item -ItemType File -Path (Join-Path $dir ".gitkeep") -Force | Out-Null
}

$forbidden = @(
    "repositories.local.json", ".env", "node_modules", "\bin\", "\obj\", "TestResults", ".trx",
    ".tsbuildinfo", ".grf", ".gpf", ".thor", ".spr", ".act", ".bmp", ".tga", ".rsw", ".gnd", ".gat"
)
$stagedFiles = Get-ChildItem -LiteralPath $staging -Recurse -Force -File
foreach ($file in $stagedFiles) {
    foreach ($pattern in $forbidden) {
        if ($file.FullName.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Forbidden file in package staging: $($file.FullName)"
        }
    }
}

if (Test-Path -LiteralPath $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $ZipPath -Force

$zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
try {
    foreach ($entry in $zip.Entries) {
        foreach ($pattern in $forbidden) {
            if ($entry.FullName.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "Forbidden entry in package: $($entry.FullName)"
            }
        }
    }
}
finally {
    $zip.Dispose()
}

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}

Write-Host "Clean package created: $ZipPath"
