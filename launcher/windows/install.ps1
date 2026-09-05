# Run in Windows PowerShell 5.1 or later. Existing saves and mods are preserved.
[CmdletBinding()]
param(
    [string]$GameDir,
    [string]$Framework,
    [string]$Helper,
    [string]$Release = 'latest',
    [switch]$ReinstallLoader,
    [switch]$Dev
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Find-Game {
    $roots = New-Object 'System.Collections.Generic.List[string]'
    foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam')) {
        $entry = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($null -eq $entry) { continue }
        foreach ($name in @('SteamPath', 'InstallPath')) {
            $value = $entry.PSObject.Properties[$name]
            if ($null -ne $value -and $value.Value) { $roots.Add([string]$value.Value) }
        }
    }
    $libraries = New-Object 'System.Collections.Generic.List[string]'
    foreach ($root in $roots) {
        $libraries.Add($root)
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            $raw = Get-Content -LiteralPath $vdf -Raw
            foreach ($match in [regex]::Matches($raw, '"path"\s*"((?:\\.|[^"\\])*)"')) {
                $libraries.Add($match.Groups[1].Value.Replace('\\', '\'))
            }
            # Older Steam clients stored each library as a numeric key and path.
            foreach ($match in [regex]::Matches($raw, '"\d+"\s*"([A-Za-z]:[^"\r\n]*)"')) {
                $libraries.Add($match.Groups[1].Value.Replace('\\', '\'))
            }
        }
    }
    foreach ($library in ($libraries | Select-Object -Unique)) {
        $manifest = Join-Path $library 'steamapps\appmanifest_527230.acf'
        if (Test-Path -LiteralPath $manifest) {
            $match = [regex]::Match((Get-Content -LiteralPath $manifest -Raw), '"installdir"\s*"([^"\r\n]+)"')
            if ($match.Success) {
                $candidate = Join-Path (Join-Path $library 'steamapps\common') $match.Groups[1].Value
                if (Test-Path -LiteralPath (Join-Path $candidate 'FTK.exe')) { return $candidate }
            }
        }
        $candidate = Join-Path $library 'steamapps\common\For The King'
        if (Test-Path -LiteralPath (Join-Path $candidate 'FTK.exe')) { return $candidate }
    }
    throw 'For The King was not found. Run install.ps1 -GameDir "C:\path\to\For The King".'
}

function Assert-Game([string]$Directory) {
    $exe = Join-Path $Directory 'FTK.exe'
    $stream = [IO.File]::OpenRead($exe)
    $reader = New-Object IO.BinaryReader($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'FTK.exe is not a Windows PE executable.' }
        $stream.Position = 0x3C
        $offset = $reader.ReadInt32()
        if ($offset -lt 64 -or $offset -gt ($stream.Length - 6)) { throw 'FTK.exe has an invalid PE header.' }
        $stream.Position = $offset
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne 0x8664) { throw 'This installer requires the Windows x64 version of FTK.exe.' }
    } finally { $reader.Dispose() }
    if (-not (Test-Path -LiteralPath (Join-Path $Directory 'FTK_Data\Managed\Assembly-CSharp.dll'))) { throw 'The FTK managed game files are missing.' }
}

function Download([string]$Url, [string]$Path) {
    Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $Path
}

function Assert-Hash([string]$Path, [string]$Expected) {
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) { throw "SHA256 verification failed: $Path" }
}

function Set-ConfigValue([string]$Path, [string]$Section, [string]$Key, [string]$Value) {
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $inSection = $false
    $written = $false
    $source = @()
    if (Test-Path -LiteralPath $Path) { $source = @(Get-Content -LiteralPath $Path) }
    foreach ($line in $source) {
        if ($line -match '^\s*\[([^\]]+)\]\s*$') {
            if ($inSection -and -not $written) { $lines.Add("$Key = $Value"); $written = $true }
            $inSection = $Matches[1] -eq $Section
        }
        if ($inSection -and $line -match ('^\s*' + [regex]::Escape($Key) + '\s*=')) {
            if (-not $written) { $lines.Add("$Key = $Value"); $written = $true }
        } else { $lines.Add($line) }
    }
    if (-not $written) {
        if (-not $inSection) { $lines.Add("[$Section]") }
        $lines.Add("$Key = $Value")
    }
    [IO.File]::WriteAllLines($Path, $lines, (New-Object Text.UTF8Encoding($false)))
}

if (-not $GameDir) { $GameDir = Find-Game }
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
Assert-Game $GameDir
if (Get-Process -Name FTK -ErrorAction SilentlyContinue) { throw 'Close For The King before installing.' }
$stage = Join-Path ([IO.Path]::GetTempPath()) ('ftkmf-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
Write-Host "Game: $GameDir"
Write-Host "Download staging: $stage"

$remoteFramework = -not $Framework
if (-not $Framework) {
    $bundled = Join-Path $PSScriptRoot 'FTKModFramework.dll'
    if (Test-Path -LiteralPath $bundled) { $Framework = $bundled }
}
if ($Framework) {
    $remoteFramework = $false
    $Framework = (Resolve-Path -LiteralPath $Framework).Path
    if ([IO.Path]::GetExtension($Framework) -ne '.dll') { throw '-Framework must name a local framework DLL.' }
} else {
    $endpoint = 'https://api.github.com/repos/jarlbrak/ftk-mod-framework/releases/'
    if ($Release -eq 'latest') { $endpoint += 'latest' } else { $endpoint += 'tags/' + [Uri]::EscapeDataString($Release) }
    $releaseInfo = Invoke-RestMethod -Uri $endpoint
    $asset = @($releaseInfo.assets | Where-Object { $_.name -eq 'FTKModFramework.dll' })
    $checksums = @($releaseInfo.assets | Where-Object { $_.name -eq 'SHA256SUMS' })
    if ($asset.Count -ne 1 -or $checksums.Count -ne 1) { throw 'Release must provide FTKModFramework.dll and SHA256SUMS. Alternatively use -Framework with a trusted local DLL.' }
    $Framework = Join-Path $stage 'FTKModFramework.dll'
    $sums = Join-Path $stage 'SHA256SUMS'
    Download $checksums[0].browser_download_url $sums
    $matches = @([regex]::Matches((Get-Content -LiteralPath $sums -Raw), '(?m)^([0-9a-fA-F]{64})\s+\*?FTKModFramework\.dll\s*$'))
    if ($matches.Count -ne 1) { throw 'SHA256SUMS must contain exactly one checksum for FTKModFramework.dll.' }
    Download $asset[0].browser_download_url $Framework
    Assert-Hash $Framework $matches[0].Groups[1].Value
}

# Validate both release assets before replacing the installed framework.
$helperName = 'ftkmf-launcher-helper.exe'
if ($remoteFramework) {
    if ($Helper) { throw '-Helper requires a trusted local -Framework.' }
    $helperAssetName = 'ftkmf-helper-windows-amd64.exe'
    $helperAssets = @($releaseInfo.assets | Where-Object { $_.name -eq $helperAssetName })
    if ($helperAssets.Count -ne 1) { throw "Release must provide $helperAssetName for the in-game marketplace." }
    $helperMatches = @([regex]::Matches((Get-Content -LiteralPath $sums -Raw), '(?m)^([0-9a-fA-F]{64})\s+\*?ftkmf-helper-windows-amd64\.exe\s*$'))
    if ($helperMatches.Count -ne 1) { throw 'SHA256SUMS must contain exactly one checksum for the marketplace helper.' }
    $Helper = Join-Path $stage $helperName
    Download $helperAssets[0].browser_download_url $Helper
    Assert-Hash $Helper $helperMatches[0].Groups[1].Value
} elseif (-not $Helper) {
    $sibling = Join-Path (Split-Path -Parent $Framework) $helperName
    if (Test-Path -LiteralPath $sibling) { $Helper = $sibling }
}
if ($Helper) {
    $Helper = (Resolve-Path -LiteralPath $Helper).Path
    if ((Get-Item -LiteralPath $Helper).Length -eq 0) { throw 'Marketplace helper is empty.' }
} else { Write-Warning 'Local DLL only: marketplace helper missing. Installed mods remain available; use launcher Install / Repair to enable downloads.' }

$loaderFiles = @('winhttp.dll', 'BepInEx\core\BepInEx.dll', 'doorstop_config.ini')
$presentLoaderFiles = @($loaderFiles | Where-Object { Test-Path -LiteralPath (Join-Path $GameDir $_) -PathType Leaf })
$hasLoader = $presentLoaderFiles.Count -eq $loaderFiles.Count
if ($presentLoaderFiles.Count -gt 0 -and -not $hasLoader -and -not $ReinstallLoader) {
    throw 'Partial or unrecognized loader installation found. Run install.ps1 -ReinstallLoader to back up and repair the loader explicitly.'
}
if (-not $hasLoader -or $ReinstallLoader) {
    $zip = Join-Path $stage 'BepInEx_win_x64_5.4.23.5.zip'
    Download 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip' $zip
    Assert-Hash $zip '82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4'
    $expanded = Join-Path $stage 'loader'
    Expand-Archive -LiteralPath $zip -DestinationPath $expanded
    # Back up every replaced loader file. Do not replace existing config or plugins.
    $backup = Join-Path $GameDir ('ftkmf-backup-' + [Guid]::NewGuid().ToString('N'))
    foreach ($file in (Get-ChildItem -LiteralPath $expanded -File -Recurse)) {
        $relative = $file.FullName.Substring($expanded.Length + 1)
        if ($relative -match '^BepInEx\\(config|plugins)\\') { continue }
        $destination = Join-Path $GameDir $relative
        if (Test-Path -LiteralPath $destination) {
            $saved = Join-Path $backup $relative
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $saved) | Out-Null
            Copy-Item -LiteralPath $destination -Destination $saved
        }
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }
} else { Write-Host 'Existing loader preserved. Use -ReinstallLoader to explicitly replace loader files.' }

if ($Helper) {
    $helperDir = Join-Path $GameDir 'BepInEx\ftkmf'
    New-Item -ItemType Directory -Force -Path $helperDir | Out-Null
    $helperDestination = Join-Path $helperDir $helperName
    if (Test-Path -LiteralPath $helperDestination) {
        Copy-Item -LiteralPath $helperDestination -Destination ($helperDestination + '.' + [Guid]::NewGuid().ToString('N') + '.bak')
    }
    Copy-Item -LiteralPath $Helper -Destination $helperDestination -Force
    $helperRecord = @{ schemaVersion = 1; protocolVersion = 1; sha256 = (Get-FileHash -LiteralPath $Helper -Algorithm SHA256).Hash.ToLowerInvariant() }
    [IO.File]::WriteAllText((Join-Path $helperDir 'helper.json'), ($helperRecord | ConvertTo-Json -Compress), (New-Object Text.UTF8Encoding($false)))
}
$plugins = Join-Path $GameDir 'BepInEx\plugins'
New-Item -ItemType Directory -Force -Path $plugins | Out-Null
$destination = Join-Path $plugins 'FTKModFramework.dll'
if (Test-Path -LiteralPath $destination) {
    Copy-Item -LiteralPath $destination -Destination ($destination + '.' + [Guid]::NewGuid().ToString('N') + '.bak')
}
Copy-Item -LiteralPath $Framework -Destination $destination -Force
$configDir = Join-Path $GameDir 'BepInEx\config'
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
$config = Join-Path $configDir 'com.ftkmf.framework.cfg'
if (Test-Path -LiteralPath $config) {
    Copy-Item -LiteralPath $config -Destination ($config + '.' + [Guid]::NewGuid().ToString('N') + '.bak')
}
if ($Dev) { Set-ConfigValue $config 'Diagnostics' 'RunSelfTests' 'true' }
else {
    Set-ConfigValue $config 'Diagnostics' 'RunSelfTests' 'false'
    Set-ConfigValue $config 'Diagnostics' 'EnableScaleBudgetGate' 'false'
    Set-ConfigValue $config 'Diagnostics' 'SyntheticContentCount' '0'
    Set-ConfigValue $config 'Enemies' 'ForceCustomEnemy' 'false'
    Set-ConfigValue $config 'Adventures' 'ForceCustomEncounter' 'false'
}
Write-Host 'Installed. No Steam launch option is needed on native Windows.'
Write-Host 'Launch using Play, then check BepInEx\LogOutput.log for SELF-TEST PASS.'
