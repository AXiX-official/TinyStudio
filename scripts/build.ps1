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
    [switch]$Satori,
    [switch]$NodeData,
    [switch]$List,
    [ValidateSet('all', 'server', 'satori')]
    [string]$GC = 'all',
    [ValidateSet('all',
        'net10', 'net10-aot', 'net10-satori', 'net10-aot-satori', 'net10-nodedata', 'net10-satori-nodedata',
        'net11', 'net11-aot', 'net11-satori', 'net11-aot-satori', 'net11-nodedata', 'net11-satori-nodedata')]
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
Usage: scripts\build.ps1 [-Verify] [-Variant NAME] [-Satori] [-NodeData] [-Help]
       (or scripts\build.bat -Verify -Variant NAME -Satori -NodeData)

Build (and optionally verify) TinyStudio publish variants for win-x64.
Runs `dotnet restore` and sets the CI build env vars itself.

  -Verify            Verify AOT variants contain compiled Avalonia XAML.
  -Variant NAME      Build exactly this variant, on its own. The -Satori / -NodeData / -GC
                     switches only shape what "all" means; a named variant needs none of them.
  -List              Print the variants that would be built, then exit without building.
  -Satori            Also build the Satori GC variants. Satori is a publish-time
                     switch and this app is multi-targeted, so it cannot live in
                     the csproj; the non-AOT shape also needs a self-contained
                     publish to carry the runtime that offers it.
  -NodeData          Also build the JIT variants that parse through the NodeData
                     backend instead of runtime code generation.
  -GC NAME           all (default) | server | satori. Narrows the selection to one
                     garbage collector, so one GC can be built on its own.
  -Help, -h          Show this help and exit.

Variants (default, server GC): net10 | net10-aot | net11 | net11-aot
Added by -Satori:              net10-satori | net10-aot-satori
                               net11-satori | net11-aot-satori
Added by -NodeData:            net10-nodedata | net11-nodedata
Added by both:                 net10-satori-nodedata | net11-satori-nodedata

Common combinations:
  -NodeData                      one GC (server), both TFMs, all three shapes (6)
  -GC satori -Satori -NodeData   the Satori GC alone, both TFMs, all three shapes (6)
  -Satori -NodeData              both GCs, both TFMs, every shape (12)
  -List                          show the resulting set first, build nothing

Output folders keep the existing names for the default variants
(publish-net10, publish-net10-aot, ...) and are suffixed for the extra ones
(publish-net10-satori, publish-net10-aot-satori, publish-net10-nodedata,
publish-net10-satori-nodedata, ...).

Verification applies to AOT variants only; non-AOT variants are skipped
with a notice. Requires the .NET 11 SDK (setup-dotnet 11.0.x in CI).
'@
}

if ($Help) { Show-Usage; Exit-Ok }

# Order is load-bearing: within one TFM every non-AOT publish comes first and the
# AOT ones last, because they share the same obj dir (see the notes at the top).
# SatoriVersion is the <Sdk> package version that matches the TFM: the 10.x and 11.x
# Satori SDKs each pull their own runtime pack, and the version cannot be chosen per
# target framework inside the multi-targeted csproj.
$SatoriNet10 = '10.0.15-satori.42242472690.2'
$SatoriNet11 = '11.0.0-satori.42242472725.1'
$AllVariants = @(
    [pscustomobject]@{ Name = 'net10';                  Tfm = 'net10.0'; Aot = $false; Satori = $false; NodeData = $false; Out = 'net10';                  SatoriVersion = '' }
    [pscustomobject]@{ Name = 'net10-satori';           Tfm = 'net10.0'; Aot = $false; Satori = $true;  NodeData = $false; Out = 'net10-satori';           SatoriVersion = $SatoriNet10 }
    [pscustomobject]@{ Name = 'net10-nodedata';         Tfm = 'net10.0'; Aot = $false; Satori = $false; NodeData = $true;  Out = 'net10-nodedata';         SatoriVersion = '' }
    [pscustomobject]@{ Name = 'net10-satori-nodedata';  Tfm = 'net10.0'; Aot = $false; Satori = $true;  NodeData = $true;  Out = 'net10-satori-nodedata';  SatoriVersion = $SatoriNet10 }
    [pscustomobject]@{ Name = 'net10-aot';              Tfm = 'net10.0'; Aot = $true;  Satori = $false; NodeData = $false; Out = 'net10-aot';              SatoriVersion = '' }
    [pscustomobject]@{ Name = 'net10-aot-satori';       Tfm = 'net10.0'; Aot = $true;  Satori = $true;  NodeData = $false; Out = 'net10-aot-satori';       SatoriVersion = $SatoriNet10 }
    [pscustomobject]@{ Name = 'net11';                  Tfm = 'net11.0'; Aot = $false; Satori = $false; NodeData = $false; Out = 'net11';                  SatoriVersion = '' }
    [pscustomobject]@{ Name = 'net11-satori';           Tfm = 'net11.0'; Aot = $false; Satori = $true;  NodeData = $false; Out = 'net11-satori';           SatoriVersion = $SatoriNet11 }
    [pscustomobject]@{ Name = 'net11-nodedata';         Tfm = 'net11.0'; Aot = $false; Satori = $false; NodeData = $true;  Out = 'net11-nodedata';         SatoriVersion = '' }
    [pscustomobject]@{ Name = 'net11-satori-nodedata';  Tfm = 'net11.0'; Aot = $false; Satori = $true;  NodeData = $true;  Out = 'net11-satori-nodedata';  SatoriVersion = $SatoriNet11 }
    [pscustomobject]@{ Name = 'net11-aot';              Tfm = 'net11.0'; Aot = $true;  Satori = $false; NodeData = $false; Out = 'net11-aot';              SatoriVersion = '' }
    [pscustomobject]@{ Name = 'net11-aot-satori';       Tfm = 'net11.0'; Aot = $true;  Satori = $true;  NodeData = $false; Out = 'net11-aot-satori';       SatoriVersion = $SatoriNet11 }
)

# The default set stays exactly the four server-GC variants; the extra ones are opt-in so that
# push/PR builds keep their current shape and duration. -GC narrows the selection to one collector,
# which is what "one GC, both TFMs, all three shapes" needs.
if ($Variant -eq 'all') {
    $Selected = @($AllVariants | Where-Object {
        (-not $_.Satori -or $Satori) -and
        (-not $_.NodeData -or $NodeData) -and
        ($GC -eq 'all' -or ($GC -eq 'satori') -eq [bool]$_.Satori)
    })
}
else {
    # A named variant is the narrowest request there is, so it wins over the switches and -GC: asking for
    # net11-satori should not also require -Satori. The switches only decide what "all" means.
    $Selected = @($AllVariants | Where-Object { $_.Name -eq $Variant })
    if ($Selected.Count -eq 0) {
        Write-Annotation 'error' "unknown variant '$Variant'"
        Fail-Exit 2
    }
}

if ($List) {
    Write-Host "variants selected: $($Selected.Count)"
    foreach ($v in $Selected) {
        Write-Host ("  {0,-24} tfm={1,-8} aot={2,-5} satori={3,-5} nodedata={4,-5} -> publish-{5}" -f `
            $v.Name, $v.Tfm, $v.Aot, $v.Satori, $v.NodeData, $v.Out)
    }
    Exit-Ok
}

Write-Host '>>> Restoring'
& dotnet restore $PROJECT
if ($LASTEXITCODE -ne 0) {
    Write-Annotation 'error' "restore failed (exit $LASTEXITCODE)"
    Fail-Exit $LASTEXITCODE
}

$built = @()
# Tracks which backend the library was last compiled for. UnityAssetNodeData changes what
# UnityAsset.NET compiles (the analyzer output, the Roslyn package reference, DefineConstants),
# and an incremental build does not notice that switch, so the library's obj is dropped whenever
# the effective backend changes between variants.
$lastNodeData = $null
foreach ($v in $Selected) {
    Write-Host ('-' * 72) -ForegroundColor DarkGray
    Write-Host ">>> Building $($v.Name) (tfm=$($v.Tfm), aot=$($v.Aot), satori=$($v.Satori), nodedata=$($v.NodeData), rid=$RID)" -ForegroundColor Yellow

    # PublishAot implies UnityAssetNodeData, so the AOT variants count as the node backend too.
    $effectiveNodeData = [bool]$v.NodeData -or [bool]$v.Aot
    if ($null -eq $lastNodeData -or $lastNodeData -ne $effectiveNodeData) {
        $libraryObj = Join-Path $REPO 'UnityAsset.NET/UnityAsset.NET/obj'
        if (Test-Path -LiteralPath $libraryObj) {
            Remove-Item -LiteralPath $libraryObj -Recurse -Force
        }
        $lastNodeData = $effectiveNodeData
    }

    $objDir = Join-Path $REPO "TinyStudio/obj/$CONFIG/$($v.Tfm)/$RID"
    if (Test-Path -LiteralPath $objDir) {
        Remove-Item -LiteralPath $objDir -Recurse -Force
    }

    # publish does not clean the output folder: a framework-dependent publish over a previous
    # self-contained one leaves a stale runtime behind and the folder no longer describes the variant.
    $outDir = Join-Path $REPO "publish-$($v.Out)"
    if (Test-Path -LiteralPath $outDir) {
        Remove-Item -LiteralPath $outDir -Recurse -Force
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
    if ($v.Satori) {
        # Satori swaps the runtime pack, so the publish has to be self-contained. The SDK version itself is
        # switched in around the publish below, because an <Sdk> version cannot be conditioned per TFM.
        $publishArgs += @(
            '-p:SelfContained=true',
            '-p:UseSatoriGC=true'
        )
    }
    else {
        # The project references the Satori SDK unconditionally (an <Sdk> element cannot be conditioned per TFM),
        # and it defaults to enabled. Left alone, every server-GC build would swap the runtime pack, every AOT
        # build would compile with the Satori ILC toolchain, and each one would carry the experimental warning.
        $publishArgs += @('-p:UseSatoriGC=false')
    }
    if ($v.NodeData) {
        # Same binary, but the parser goes through the node backend instead of runtime code generation.
        $publishArgs += @('-p:UnityAssetNodeData=true')
    }

    $csprojPath = Join-Path $REPO 'TinyStudio/TinyStudio.csproj'
    $csprojOriginal = $null
    if ($v.Satori) {
        $csprojOriginal = [System.IO.File]::ReadAllText($csprojPath)
        $element = '<Sdk Name="PublishWithSatoriGC" Version="' + $v.SatoriVersion + '" />'

        # The project deliberately does not carry the element (an <Sdk> cannot be conditioned per TFM, and leaving
        # one in would also change the server-GC builds). The publish injects it and the finally block takes it back
        # out; an element that is somehow already active is re-versioned instead.
        $activePattern = '<Sdk Name="PublishWithSatoriGC" Version="[^"]*" />'
        if ([System.Text.RegularExpressions.Regex]::IsMatch($csprojOriginal, $activePattern)) {
            $swapped = [System.Text.RegularExpressions.Regex]::Replace($csprojOriginal, $activePattern, $element)
        }
        else {
            $swapped = [System.Text.RegularExpressions.Regex]::Replace(
                $csprojOriginal,
                '(?m)^([ \t]*<Project Sdk="Microsoft\.NET\.Sdk">[ \t]*\r?\n)',
                '$1    ' + $element + "`r`n")
            if ($swapped -eq $csprojOriginal) {
                Write-Annotation 'error' 'could not inject the Satori <Sdk> element: TinyStudio.csproj has no <Project Sdk="Microsoft.NET.Sdk"> header'
                Fail-Exit 1
            }
        }
        [System.IO.File]::WriteAllText($csprojPath, $swapped)
    }

    try {
        & dotnet @publishArgs
        $publishExit = $LASTEXITCODE
    }
    finally {
        # Always put the project back, including when the publish fails or is interrupted.
        if ($null -ne $csprojOriginal) {
            [System.IO.File]::WriteAllText($csprojPath, $csprojOriginal)
        }
    }

    if ($publishExit -ne 0) {
        Write-Annotation 'error' "publish failed for $($v.Name) (exit $publishExit)"
        Fail-Exit $publishExit
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
