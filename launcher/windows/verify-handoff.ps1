# Windows-only UI contract check. Never invokes Play, installation, or Steam.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The shipped launcher targets .NET Framework 4.8. Exercise it in the same CLR,
# including WinForms, even when CI starts this script with PowerShell 7.
if ($PSVersionTable.PSEdition -eq 'Core') {
    if (-not $IsWindows) { throw 'The launcher handoff UI check requires Windows.' }
    & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -File $PSCommandPath
    if ($LASTEXITCODE -ne 0) { throw "Launcher handoff UI check exited with code $LASTEXITCODE." }
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
$assemblyPath = Join-Path $PSScriptRoot 'bin\Release\net48\FtkModdedLauncher.exe'
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$optionsType = $assembly.GetType('FtkModdedLauncher.LaunchOptions', $true)
$launcherType = $assembly.GetType('FtkModdedLauncher.Launcher', $true)
$instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$staticFlags = [Reflection.BindingFlags]'Static,NonPublic'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('ftkmf-handoff-' + [Guid]::NewGuid().ToString('N'))
$child = $null
$signal = $null
$form = $null

try {
    $null = New-Item -ItemType Directory -Path $fixtureRoot
    $eventName = 'Local\FTKMFBootstrap-' + [Guid]::NewGuid().ToString('N')
    $signal = New-Object Threading.EventWaitHandle($false, [Threading.EventResetMode]::AutoReset, $eventName)
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
    $start.Arguments = '-NoProfile -NonInteractive -Command "Start-Sleep -Seconds 120"'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $child = [Diagnostics.Process]::Start($start)

    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'ftkmf-game-directory.txt'), $fixtureRoot + "`r`n")
    $arguments = New-Object 'object[]' 2
    $arguments[0] = [string[]]@('--wait-for-process', [string]$child.Id, '--ready-event', $eventName)
    $arguments[1] = $fixtureRoot.PSObject.BaseObject
    $options = $optionsType.GetMethod('FromDirectory', $staticFlags).Invoke($null, $arguments)
    $constructor = $launcherType.GetConstructor($instanceFlags, $null, [Type[]]@($optionsType), $null)
    $form = $constructor.Invoke([object[]]@($options.PSObject.BaseObject))
    if ($signal.WaitOne(0)) { throw 'Launcher acknowledged readiness before showing its waiting UI.' }
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    if (-not $signal.WaitOne(2000)) { throw 'Launcher did not acknowledge its initialized waiting UI.' }
    if ($child.HasExited) { throw 'Fixture child exited before the waiting-state assertion.' }
    $actions = $launcherType.GetField('_actions', $instanceFlags).GetValue($form)
    foreach ($button in $actions) {
        if ($button.Enabled) { throw "Action '$($button.Text)' remained enabled while the game process was alive." }
    }
    $status = $launcherType.GetField('_artworkStatus', $instanceFlags).GetValue($form)
    if ($status.Text -notlike 'Close For The King*') { throw "Unexpected waiting status: $($status.Text)" }
    Write-Host 'PASS: readiness event follows initialized UI; actions wait for the original process'

    $child.Kill()
    if (-not $child.WaitForExit(5000)) { throw 'Fixture child did not exit.' }
    $null = $launcherType.GetMethod('CheckHandoffGame', $instanceFlags).Invoke($form, $null)
    [Windows.Forms.Application]::DoEvents()
    foreach ($button in $actions) {
        if (-not $button.Enabled) { throw "Action '$($button.Text)' remained blocked after process exit." }
    }
    if ($status.Text -notlike 'Choose Play to finish installation*') { throw "Unexpected ready status: $($status.Text)" }
    if ($launcherType.GetField('_busy', $instanceFlags).GetValue($form)) { throw 'Launcher remained busy after process exit.' }
    Write-Host 'PASS: process exit enables Play and prompts completion without running an installer'

    $form.Close()
    $form.Dispose()
    $form = $null
    # No bundled helper exists in this PowerShell host directory, so the normal
    # artwork worker can only report a missing tool; it cannot touch Steam.
    if (Test-Path -LiteralPath (Join-Path ([AppDomain]::CurrentDomain.BaseDirectory) 'ftkmf-launcher-helper.exe')) {
        throw 'Unexpected helper in the fixture host directory.'
    }
    $entry = Join-Path $fixtureRoot 'FTKModdedBootstrap.exe'
    [IO.File]::WriteAllText($entry, 'fixture entry')
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'ftkmf-bootstrap-entry.txt'), $entry + "`r`n")
    $arguments[0] = [string[]]@('--ready-event', $eventName)
    $options = $optionsType.GetMethod('FromDirectory', $staticFlags).Invoke($null, $arguments)
    $boundEntry = $optionsType.GetProperty('BootstrapEntry', $instanceFlags).GetValue($options, $null)
    if ($boundEntry -ne $entry) { throw 'Persistent bootstrap entry was not loaded.' }
    $form = $constructor.Invoke([object[]]@($options.PSObject.BaseObject))
    if ($signal.WaitOne(0)) { throw 'Refresh acknowledged readiness before showing the launcher.' }
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    if (-not $signal.WaitOne(2000)) { throw 'Normal refresh did not acknowledge launcher readiness.' }
    $actions = $launcherType.GetField('_actions', $instanceFlags).GetValue($form)
    foreach ($button in $actions) {
        if (-not $button.Enabled) { throw 'Normal refresh unexpectedly blocked launcher actions.' }
    }
    Write-Host 'PASS: refresh readiness works without a running game and retains the durable bootstrap entry'
}
finally {
    if ($null -ne $form) { $form.Close(); $form.Dispose() }
    if ($null -ne $child) {
        try { if (-not $child.HasExited) { $child.Kill(); $null = $child.WaitForExit(5000) } }
        finally { $child.Dispose() }
    }
    if ($null -ne $signal) { $signal.Dispose() }
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}
