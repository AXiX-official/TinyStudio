# Build (and optionally verify) TinyStudio publish variants for win-x64.
# Shared by CI (.github/workflows/build-windows.yml) and local builds.
# Compatible with Windows PowerShell 5.1 as well as PowerShell 7 (pwsh).
#
# Notes learned the hard way -- do not "simplify" these away:
#   * Never insert `dotnet clean` between publishes: the standalone restore runs
#     without a RID, so clean needs an assets target it does not have and fails
#     with NETSDK1047/NETSDK1005. Delete only the RID-specific obj directory.
#   * AOT publishes need BOTH -p:UnityAssetTargetFramework=<tfm> (consumed by
#     UnityAsset.NET/Directory.Build.targets; without it the netstandard2.1
#     submodule target triggers NETSDK1207) AND -p:TargetFrameworks=<tfm>
#     (without it the multi-targeted app resolves its other TFM against a
#     net11-only submodule and fails with NU1201).
#   * Verification checks the MANAGED intermediate dll, not the native exe:
#     whether ILCompiler keeps type names in the native image is version
#     dependent, so a missing marker there is only a notice, never a failure.

[CmdletBinding()]
param(
    [switch]$Verify,
    [ValidateSet('all', 'net10', 'net10-aot', 'net11', 'net11-aot')]
    [string]$Variant = 'all',
    [Alias('h')]
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

$CONFIG  = 'Release'
$PROJECT = 'TinyStudio/TinyStudio.csproj'
$RID     = 'win-x64'
$EXE     = 'TinyStudio.exe'

$REPO = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
Set-Location -LiteralPath $REPO

$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'

# GitHub Actions annotations only when actually running in CI; plain text locally.
function Write-Annotation {
    param([string]$Level, [string]$Message, [string]$File = '')
    if ($env:GITHUB_ACTIONS -eq 'true') {
        if ($File) { Write-Host "::$Level file=$File::$Message" }
        else       { Write-Host "::$Level::$Message" }
    }
    else {
        switch ($Level) {
            'error'   { Write-Host "ERROR: $Message" }
            'warning' { Write-Host "WARNING: $Message" }
            'notice'  { Write-Host "NOTE: $Message" }
        }
    }
}

# GitHub's pwsh wrapper dot-sources a temp script and appends `exit $LASTEXITCODE`.
# Set the global before exiting so both exit semantics propagate the code.
function Fail-Exit { param([int]$Code) $global:LASTEXITCODE = $Code; exit $Code }
function Exit-Ok   { $global:LASTEXITCODE = 0; exit 0 }

# Binary-safe ASCII marker search: no grep, no Get-Content, no Select-String.
# Encoding.ASCII maps bytes >= 0x80 to '?', which cannot create false matches
# for a pure-ASCII needle.
function Test-Marker {
    param([Parameter(Mandatory = $true)][string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
    return $ascii.Contains('CompiledAvaloniaXaml')
}

function Show-Usage {
    Write-Host @'
Usage: scripts\build.ps1 [-Verify] [-Variant NAME] [-Help]
       (or scripts\build.bat -Verify -Variant NAME)

Build (and optionally verify) TinyStudio publish variants for win-x64.
Runs `dotnet restore` and sets the CI build env vars itself.

  -Verify            Verify AOT variants contain compiled Avalonia XAML.
  -Variant NAME      all (default) | net10 | net10-aot | net11 | net11-aot
  -Help, -h          Show this help and exit.

Verification applies to AOT variants only; non-AOT variants are skipped
with a notice. Requires the .NET 11 SDK (setup-dotnet 11.0.x in CI).
'@
}

if ($Help) { Show-Usage; Exit-Ok }

# Order is load-bearing: plain before AOT for each TFM. net10 and net10-aot
# share the same obj dir, so the AOT publish must be the last one for its TFM.
$AllVariants = @(
    [pscustomobject]@{ Name = 'net10';     Tfm = 'net10.0'; Aot = $false; Out = 'net10' }
    [pscustomobject]@{ Name = 'net10-aot'; Tfm = 'net10.0'; Aot = $true;  Out = 'net10-aot' }
    [pscustomobject]@{ Name = 'net11';     Tfm = 'net11.0'; Aot = $false; Out = 'net11' }
    [pscustomobject]@{ Name = 'net11-aot'; Tfm = 'net11.0'; Aot = $true;  Out = 'net11-aot' }
)
if ($Variant -eq 'all') {
    $Selected = $AllVariants
}
else {
    $Selected = @($AllVariants | Where-Object { $_.Name -eq $Variant })
}

Write-Host '>>> Restoring'
& dotnet restore $PROJECT
if ($LASTEXITCODE -ne 0) {
    Write-Annotation 'error' "restore failed (exit $LASTEXITCODE)"
    Fail-Exit $LASTEXITCODE
}

$built = @()
foreach ($v in $Selected) {
    Write-Host ">>> Building $($v.Name) (tfm=$($v.Tfm), aot=$($v.Aot), rid=$RID)"

    $objDir = Join-Path $REPO "TinyStudio/obj/$CONFIG/$($v.Tfm)/$RID"
    if (Test-Path -LiteralPath $objDir) {
        Remove-Item -LiteralPath $objDir -Recurse -Force
    }

    # Do NOT name this $args (PowerShell automatic variable).
    $publishArgs = @(
        'publish', $PROJECT,
        '-c', $CONFIG, '-f', $v.Tfm, '-r', $RID,
        '-o', "./publish-$($v.Out)"
    )
    if ($v.Aot) {
        $publishArgs += @(
            '-p:PublishAot=true',
            "-p:UnityAssetTargetFramework=$($v.Tfm)",
            "-p:TargetFrameworks=$($v.Tfm)"
        )
    }

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Annotation 'error' "publish failed for $($v.Name) (exit $LASTEXITCODE)"
        Fail-Exit $LASTEXITCODE
    }
    $built += $v.Name
}

function Test-Aot {
    param([string]$Tfm, [string]$Out)
    $dll = "TinyStudio/obj/$CONFIG/$Tfm/$RID/TinyStudio.dll"
    $exe = "publish-$Out/$EXE"
    $res = "TinyStudio/obj/$CONFIG/$Tfm/$RID/Avalonia/resources"

    Write-Host "--- verify $Out ($Tfm/$RID) ---"
    if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) {
        Write-Annotation 'error' "intermediate assembly not found: $dll"
        Fail-Exit 1
    }
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
        Write-Annotation 'error' 'AOT executable was not produced' $exe
        Fail-Exit 1
    }

    $dllBytes = (Get-Item -LiteralPath $dll).Length
    $exeBytes = (Get-Item -LiteralPath $exe).Length
    Write-Host "intermediate dll: $dllBytes bytes; native exe: $exeBytes bytes"

    if (Test-Path -LiteralPath $res -PathType Leaf) {
        Write-Host "Avalonia/resources: $((Get-Item -LiteralPath $res).Length) bytes"
    }
    else {
        Write-Annotation 'warning' "Avalonia/resources was not generated at $res"
    }

    if (-not (Test-Marker $dll)) {
        Write-Annotation 'error' "compiled Avalonia XAML is missing ($dllBytes bytes) - XAML weaving was skipped, ILC would bake the throwing stub into the exe" $dll
        Fail-Exit 1
    }
    Write-Host 'compiled XAML in intermediate assembly: OK'

    # Absence in the native image is informational only (ILCompiler dependent).
    if (-not (Test-Marker $exe)) {
        Write-Annotation 'notice' 'type-name marker absent from native image (ILCompiler-dependent, not a failure)' $exe
    }
}

if ($Verify) {
    foreach ($v in $Selected) {
        if ($v.Aot) {
            Test-Aot -Tfm $v.Tfm -Out $v.Out
        }
        else {
            Write-Annotation 'notice' "verification skipped for '$($v.Name)' (non-AOT variant)"
        }
    }
}

Write-Host '=== Summary ==='
Write-Host "variants built: $($built -join ', ')"
if ($Verify) {
    Write-Host 'verify: enabled'
}
else {
    Write-Host 'verify: disabled'
}
Write-Host 'result: OK'
Exit-Ok
