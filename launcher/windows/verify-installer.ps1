# Offline contract checks. Runs in PowerShell 7 on Linux too; not a Windows smoke test.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$installer = Join-Path $PSScriptRoot 'install.ps1'
$tokens = $null
$parseErrors = $null
$null = [Management.Automation.Language.Parser]::ParseFile($installer, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }
Write-Host 'PASS: installer parses'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('ftkmf-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
Write-Host "Fixtures retained at $fixtureRoot"
$global:FtkFixtureScenario = ''
$global:FtkFixtureRequests = 0
$global:FtkFixtureFrameworkBytes = [Text.Encoding]::UTF8.GetBytes('offline-framework-fixture')
$sha = [Security.Cryptography.SHA256]::Create()
try { $global:FtkFixtureFixtureHash = ([BitConverter]::ToString($sha.ComputeHash($global:FtkFixtureFrameworkBytes))).Replace('-', '') } finally { $sha.Dispose() }

function New-Game([string]$Name, [uint16]$Machine = 0x8664) {
    $path = Join-Path $fixtureRoot $Name
    New-Item -ItemType Directory -Path (Join-Path $path 'FTK_Data/Managed') -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $path 'FTK_Data/Managed/Assembly-CSharp.dll'), [byte[]]@())
    $bytes = New-Object byte[] 256
    [BitConverter]::GetBytes([uint16]0x5A4D).CopyTo($bytes, 0)
    [BitConverter]::GetBytes([int]128).CopyTo($bytes, 60)
    [BitConverter]::GetBytes([uint32]0x4550).CopyTo($bytes, 128)
    [BitConverter]::GetBytes($Machine).CopyTo($bytes, 132)
    [IO.File]::WriteAllBytes((Join-Path $path 'FTK.exe'), $bytes)
    return $path
}

function Invoke-RestMethod {
    param($Uri)
    $assets = @([pscustomobject]@{ name = 'FTKModFramework.dll'; browser_download_url = 'https://fixture.invalid/framework' })
    if ($global:FtkFixtureScenario -ne 'missing-checksums') { $assets += [pscustomobject]@{ name = 'SHA256SUMS'; browser_download_url = 'https://fixture.invalid/sums' } }
    return [pscustomobject]@{ assets = $assets }
}

function Invoke-WebRequest {
    param([switch]$UseBasicParsing, $Uri, $OutFile)
    $global:FtkFixtureRequests++
    switch ($Uri) {
        'https://fixture.invalid/sums' {
            $entry = "$global:FtkFixtureFixtureHash  FTKModFramework.dll"
            if ($global:FtkFixtureScenario -eq 'missing-entry') { $entry = "$global:FtkFixtureFixtureHash  unrelated.dll" }
            if ($global:FtkFixtureScenario -eq 'bad-hash') { $entry = ('0' * 64) + '  FTKModFramework.dll' }
            [IO.File]::WriteAllText($OutFile, $entry)
        }
        'https://fixture.invalid/framework' { [IO.File]::WriteAllBytes($OutFile, $global:FtkFixtureFrameworkBytes) }
        default { throw "Unexpected network request: $Uri" }
    }
}

function Expect-Failure([string]$Name, [string]$Pattern, [string]$Path) {
    $message = ''
    try { & $installer -GameDir $Path } catch { $message = $_.Exception.Message }
    if ($message -notlike $Pattern) { throw "$Name failed: expected '$Pattern', received '$message'" }
    if (Test-Path -LiteralPath (Join-Path $Path 'BepInEx/plugins/FTKModFramework.dll')) { throw "$Name mutated framework before validation" }
    Write-Host "PASS: $Name"
}

$game = New-Game 'invalid-pe'
[IO.File]::WriteAllBytes((Join-Path $game 'FTK.exe'), [byte[]]@(0, 0))
Expect-Failure 'reject invalid executable' '*not a Windows PE*' $game
$game = New-Game 'x86' 0x14c
Expect-Failure 'reject x86 game' '*requires the Windows x64*' $game
foreach ($case in @(
    @('missing-checksums', '*must provide FTKModFramework.dll and SHA256SUMS*'),
    @('missing-entry', '*exactly one checksum*'),
    @('bad-hash', '*SHA256 verification failed*')
)) {
    $global:FtkFixtureScenario = $case[0]
    $game = New-Game $case[0]
    Expect-Failure $case[0] $case[1] $game
}

$global:FtkFixtureScenario = 'valid-release'
$game = New-Game 'partial-loader'
[IO.File]::WriteAllText((Join-Path $game 'winhttp.dll'), 'foreign-or-incomplete-loader')
Expect-Failure 'reject partial loader without reinstall' '*-ReinstallLoader*' $game
if ([IO.File]::ReadAllText((Join-Path $game 'winhttp.dll')) -ne 'foreign-or-incomplete-loader') { throw 'Partial loader changed without explicit reinstall' }

$game = New-Game 'valid-release'
[IO.File]::WriteAllText((Join-Path $game 'winhttp.dll'), 'existing-loader')
New-Item -ItemType Directory -Path (Join-Path $game 'BepInEx/core') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $game 'BepInEx/core/BepInEx.dll'), 'existing-core')
[IO.File]::WriteAllText((Join-Path $game 'doorstop_config.ini'), 'existing-config')
& $installer -GameDir $game
if ((Get-FileHash -LiteralPath (Join-Path $game 'BepInEx/plugins/FTKModFramework.dll')).Hash -ne $global:FtkFixtureFixtureHash) { throw 'Verified framework was not installed' }
if ([IO.File]::ReadAllText((Join-Path $game 'winhttp.dll')) -ne 'existing-loader') { throw 'Existing loader changed' }
Write-Host 'PASS: verified release installs and preserves loader'

$source = Join-Path $fixtureRoot 'local-framework.dll'
[IO.File]::WriteAllBytes($source, $global:FtkFixtureFrameworkBytes)
$before = $global:FtkFixtureRequests
& $installer -GameDir $game -Framework $source
if ($global:FtkFixtureRequests -ne $before) { throw 'Local framework unexpectedly used network' }
$backups = @(Get-ChildItem -LiteralPath (Join-Path $game 'BepInEx/plugins') -Filter '*.bak')
if ($backups.Count -ne 1) { throw 'Existing framework was not backed up' }
Write-Host 'PASS: local framework installs offline and backs up existing DLL'

$config = Join-Path $game 'BepInEx/config/com.ftkmf.framework.cfg'
[IO.File]::WriteAllText($config, "[Diagnostics]`nRunSelfTests = true`nEnableScaleBudgetGate = true`nSyntheticContentCount = 99`n[Enemies]`nForceCustomEnemy = true`n[Adventures]`nForceCustomEncounter = true`n[Demo]`nEnableSampleContent = false`n")
& $installer -GameDir $game -Framework $source
$playerConfig = Get-Content -LiteralPath $config -Raw
foreach ($expected in @('RunSelfTests = false', 'EnableScaleBudgetGate = false', 'SyntheticContentCount = 0', 'ForceCustomEnemy = false', 'ForceCustomEncounter = false', 'EnableSampleContent = false')) {
    if (-not $playerConfig.Contains($expected)) { throw "Missing preserved/reset config: $expected" }
}
Write-Host 'PASS: player install clears developer flags and preserves gameplay selection'
& $installer -GameDir $game -Framework $source -Dev
if (-not (Get-Content -LiteralPath $config -Raw).Contains('RunSelfTests = true')) { throw 'Explicit developer mode did not enable self-tests' }
Write-Host 'PASS: explicit developer mode remains available'
