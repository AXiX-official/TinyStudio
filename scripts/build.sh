#!/usr/bin/env bash
# Build (and optionally verify) TinyStudio publish variants for linux-x64.
# Shared by CI (.github/workflows/build-linux.yml) and local builds.
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

set -euo pipefail

CONFIG="Release"
PROJECT="TinyStudio/TinyStudio.csproj"
RID="linux-x64"
EXE="TinyStudio"

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO"

export MSBUILDDISABLENODEREUSE=1
export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1

# GitHub Actions annotations only when actually running in CI; plain text locally.
_ann() {
    local level="$1" file="$2" msg="$3"
    if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
        if [[ -n "$file" ]]; then
            echo "::$level file=$file::$msg"
        else
            echo "::$level::$msg"
        fi
    else
        case "$level" in
            error)   echo "ERROR: $msg" ;;
            warning) echo "WARNING: $msg" ;;
            notice)  echo "NOTE: $msg" ;;
        esac
    fi
}
log_info()   { echo "$1"; }
log_error()  { _ann error   "${2:-}" "$1"; }
log_warn()   { _ann warning "${2:-}" "$1"; }
log_notice() { _ann notice  "${2:-}" "$1"; }

# One variant takes minutes (AOT especially), so the banner has to stand out when scrolling back.
# GitHub renders ANSI in its log even though stdout there is not a tty.
if [[ -t 1 || "${GITHUB_ACTIONS:-}" == "true" ]]; then
    C_VARIANT=$'\033[1;33m'
    C_RESET=$'\033[0m'
else
    C_VARIANT=''
    C_RESET=''
fi

usage() {
    cat <<'EOF'
Usage: bash scripts/build.sh [options]

Build (and optionally verify) TinyStudio publish variants for linux-x64.
Runs `dotnet restore` and sets the CI build env vars itself.

Options:
  --verify             Verify AOT variants contain compiled Avalonia XAML.
  --variant, -v NAME   Build exactly this variant, on its own. The --satori / --nodedata /
                       --gc options only shape what "all" means; a named variant needs none of them.
  --satori             Also build the Satori GC variants. Satori is a publish-time
                       switch and this app is multi-targeted, so it cannot live in
                       the csproj; the non-AOT shape also needs a self-contained
                       publish to carry the runtime that offers it.
  --nodedata           Also build the JIT variants that parse through the NodeData
                       backend instead of runtime code generation.
  --list               Print the variants that would be built, then exit without building.
  --gc NAME            all (default) | server | satori. Narrows the selection to one
                       garbage collector, so one GC can be built on its own.
  -h, --help           Show this help and exit.

Variants (linux-x64):
  all                  net11 + net11-aot (default)
  net11                plain .NET 11 publish
  net11-aot            native AOT .NET 11 publish
  (+ --satori)         net11-satori, net11-aot-satori
  (+ --nodedata)       net11-nodedata
  (+ both)             net11-satori-nodedata

Common combinations:
  --nodedata                         one GC (server), all three shapes (3)
  --gc satori --satori --nodedata    the Satori GC alone, all three shapes (3)
  --satori --nodedata                both GCs, every shape (6)
  --list                             show the resulting set first, build nothing

Output folders keep the existing names for the default variants (publish-net11,
publish-net11-aot) and are suffixed for the extra ones (publish-net11-satori,
publish-net11-aot-satori, publish-net11-nodedata).

Notes:
  * net10 / net10-aot are Windows-only and are rejected here.
  * Verification applies to AOT variants only; others are skipped with a notice.
  * Requires the .NET 11 SDK (setup-dotnet 11.0.x in CI).
EOF
}

VERIFY=0
VARIANT="all"
SATORI=0
NODEDATA=0
LIST=0
GC="all"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --verify) VERIFY=1; shift ;;
        --satori) SATORI=1; shift ;;
        --nodedata) NODEDATA=1; shift ;;
        --list) LIST=1; shift ;;
        --gc)
            if [[ $# -lt 2 ]]; then
                log_error "--gc needs a value (all, server or satori)"
                usage
                exit 2
            fi
            GC="$2"
            shift 2
            ;;
        --variant|-v)
            if [[ $# -lt 2 ]]; then
                log_error "--variant needs a value"
                usage
                exit 2
            fi
            VARIANT="$2"
            shift 2
            ;;
        -h|--help) usage; exit 0 ;;
        *)
            log_error "unknown argument: $1"
            usage
            exit 2
            ;;
    esac
done

# Variant table: Name|Tfm|Aot|Satori|NodeData|Out|SatoriVersion
# Order is load-bearing: within one TFM every non-AOT publish comes first and the
# AOT ones last, because they share the same obj dir (see the notes at the top).
# The Satori version is the <Sdk> package that matches the TFM; it cannot be chosen
# per target framework inside a multi-targeted csproj, so the publish loop swaps it in.
SATORI_NET11="11.0.0-satori.42242472725.1"
VARIANTS=(
    "net11|net11.0|0|0|0|net11|"
    "net11-satori|net11.0|0|1|0|net11-satori|$SATORI_NET11"
    "net11-nodedata|net11.0|0|0|1|net11-nodedata|"
    "net11-satori-nodedata|net11.0|0|1|1|net11-satori-nodedata|$SATORI_NET11"
    "net11-aot|net11.0|1|0|0|net11-aot|"
    "net11-aot-satori|net11.0|1|1|0|net11-aot-satori|$SATORI_NET11"
)
case "$GC" in
    all|server|satori) ;;
    *)
        log_error "unknown --gc value '$GC' (expected: all, server, satori)"
        usage
        exit 2
        ;;
esac
SELECTED=()
if [[ "$VARIANT" == "all" ]]; then
    for rec in "${VARIANTS[@]}"; do
        IFS='|' read -r name tfm aot satori nodedata out satori_version <<< "$rec"
        if [[ "$satori" == "1" && "$SATORI" != "1" ]]; then continue; fi
        if [[ "$nodedata" == "1" && "$NODEDATA" != "1" ]]; then continue; fi
        if [[ "$GC" == "satori" && "$satori" != "1" ]]; then continue; fi
        if [[ "$GC" == "server" && "$satori" == "1" ]]; then continue; fi
        SELECTED+=("$rec")
    done
else
    # A named variant is the narrowest request there is, so it wins over the switches and --gc: asking for
    # net11-satori should not also require --satori. The switches only decide what "all" means.
    for rec in "${VARIANTS[@]}"; do
        IFS='|' read -r name tfm aot satori nodedata out satori_version <<< "$rec"
        if [[ "$name" == "$VARIANT" ]]; then SELECTED+=("$rec"); fi
    done
fi
if [[ ${#SELECTED[@]} -eq 0 ]]; then
    log_error "unknown variant '$VARIANT'"
    usage
    exit 2
fi

if [[ "$LIST" == "1" ]]; then
    echo "variants selected: ${#SELECTED[@]}"
    for rec in "${SELECTED[@]}"; do
        IFS='|' read -r name tfm aot satori nodedata out satori_version <<< "$rec"
        printf '  %-24s tfm=%-8s aot=%s satori=%s nodedata=%s -> publish-%s\n' \
            "$name" "$tfm" "$aot" "$satori" "$nodedata" "$out"
    done
    exit 0
fi

log_info ">>> Restoring"
dotnet restore "$PROJECT" || {
    rc=$?
    log_error "restore failed (exit $rc)"
    exit "$rc"
}

BUILT=""
# Tracks which backend the library was last compiled for. UnityAssetNodeData changes what
# UnityAsset.NET compiles (the analyzer output, the Roslyn package reference, DefineConstants),
# and an incremental build does not notice that switch, so the library's obj is dropped whenever
# the effective backend changes between variants.
LAST_NODE_DATA=""
for rec in "${SELECTED[@]}"; do
    IFS='|' read -r name tfm aot satori nodedata out satori_version <<< "$rec"
    echo '------------------------------------------------------------------------'
    printf '%s>>> Building %s (tfm=%s, aot=%s, satori=%s, nodedata=%s, rid=%s)%s\n' \
        "$C_VARIANT" "$name" "$tfm" "$aot" "$satori" "$nodedata" "$RID" "$C_RESET"

    # PublishAot implies UnityAssetNodeData, so the AOT variants count as the node backend too.
    if [[ "$nodedata" == "1" || "$aot" == "1" ]]; then effective_node_data=1; else effective_node_data=0; fi
    if [[ "$LAST_NODE_DATA" != "$effective_node_data" ]]; then
        rm -rf "UnityAsset.NET/UnityAsset.NET/obj"
        LAST_NODE_DATA="$effective_node_data"
    fi

    objDir="TinyStudio/obj/$CONFIG/$tfm/$RID"
    rm -rf "$objDir"

    # publish does not clean the output folder: a framework-dependent publish over a previous
    # self-contained one leaves a stale runtime behind and the folder no longer describes the variant.
    rm -rf "./publish-$out"

    publishArgs=(
        publish "$PROJECT"
        -c "$CONFIG" -f "$tfm" -r "$RID"
        -o "./publish-$out"
    )
    if [[ "$aot" == "1" ]]; then
        publishArgs+=(
            -p:PublishAot=true
            "-p:UnityAssetTargetFramework=$tfm"
            "-p:TargetFrameworks=$tfm"
        )
    fi
    if [[ "$satori" == "1" ]]; then
        # Satori swaps the runtime pack, so the publish has to be self-contained. Its <Sdk> version is
        # swapped in below, because an <Sdk> version cannot be conditioned per target framework.
        publishArgs+=(-p:SelfContained=true -p:UseSatoriGC=true)
    else
        # The project references the Satori SDK unconditionally (an <Sdk> element cannot be conditioned per TFM),
        # and it defaults to enabled. Left alone, every server-GC build would swap the runtime pack, every AOT
        # build would compile with the Satori ILC toolchain, and each one would carry the experimental warning.
        publishArgs+=(-p:UseSatoriGC=false)
    fi
    if [[ "$nodedata" == "1" ]]; then
        # Same binary, but the parser goes through the node backend instead of runtime code generation.
        publishArgs+=(-p:UnityAssetNodeData=true)
    fi

    csproj_backup=""
    if [[ "$satori" == "1" ]]; then
        csproj_backup="$(mktemp)"
        cp "$PROJECT" "$csproj_backup"
        # The project deliberately does not carry the element (an <Sdk> cannot be conditioned per TFM, and leaving
        # one in would also change the server-GC builds). Inject it here; the block below takes it back out.
        if grep -qE '<Sdk Name="PublishWithSatoriGC" Version="[^"]*" />' "$PROJECT"; then
            sed -i -E "s#<Sdk Name=\"PublishWithSatoriGC\" Version=\"[^\"]*\" />#<Sdk Name=\"PublishWithSatoriGC\" Version=\"$satori_version\" />#" "$PROJECT"
        else
            sed -i -E "0,/^[[:space:]]*<Project Sdk=\"Microsoft\.NET\.Sdk\">[[:space:]]*$/s##&\n    <Sdk Name=\"PublishWithSatoriGC\" Version=\"$satori_version\" />#" "$PROJECT"
        fi
        if ! grep -q 'PublishWithSatoriGC' "$PROJECT"; then
            cp "$csproj_backup" "$PROJECT"
            rm -f "$csproj_backup"
            log_error "could not inject the Satori <Sdk> element into $PROJECT"
            exit 1
        fi
    fi

    rc=0
    dotnet "${publishArgs[@]}" || rc=$?

    # Always put the project back, including when the publish failed.
    if [[ -n "$csproj_backup" ]]; then
        cp "$csproj_backup" "$PROJECT"
        rm -f "$csproj_backup"
    fi

    if [[ "$rc" -ne 0 ]]; then
        log_error "publish failed for $name (exit $rc)"
        exit "$rc"
    fi
    BUILT+=" $name"
done

check_aot() {
    local tfm="$1" out="$2" rid="$3"
    local dll="TinyStudio/obj/$CONFIG/$tfm/$rid/TinyStudio.dll"
    local exe="publish-$out/$EXE"
    local res="TinyStudio/obj/$CONFIG/$tfm/$rid/Avalonia/resources"
    local dll_sz exe_sz

    log_info "--- verify $out ($tfm/$rid) ---"
    if [[ ! -f "$dll" ]]; then
        log_error "intermediate assembly not found: $dll"
        exit 1
    fi
    if [[ ! -f "$exe" ]]; then
        log_error "AOT executable was not produced" "$exe"
        exit 1
    fi

    dll_sz="$(stat -c %s "$dll")"
    exe_sz="$(stat -c %s "$exe")"
    log_info "intermediate dll: $dll_sz bytes; native exe: $exe_sz bytes"

    if [[ -f "$res" ]]; then
        log_info "Avalonia/resources: $(stat -c %s "$res") bytes"
    else
        log_warn "Avalonia/resources was not generated at $res"
    fi

    if ! grep -a -q 'CompiledAvaloniaXaml' "$dll"; then
        log_error "compiled Avalonia XAML is missing ($dll_sz bytes) - XAML weaving was skipped, ILC would bake the throwing stub into the exe" "$dll"
        exit 1
    fi
    log_info "compiled XAML in intermediate assembly: OK"

    # Absence in the native image is informational only (ILCompiler dependent).
    if ! grep -a -q 'CompiledAvaloniaXaml' "$exe"; then
        log_notice "type-name marker absent from native image (ILCompiler-dependent, not a failure)" "$exe"
    fi
}

if [[ "$VERIFY" == "1" ]]; then
    for rec in "${SELECTED[@]}"; do
        IFS='|' read -r name tfm aot satori nodedata out satori_version <<< "$rec"
        if [[ "$aot" == "1" ]]; then
            check_aot "$tfm" "$out" "$RID"
        else
            log_notice "verification skipped for '$name' (non-AOT variant)"
        fi
    done
fi

log_info "=== Summary ==="
log_info "variants built:$BUILT"
if [[ "$VERIFY" == "1" ]]; then
    log_info "verify: enabled"
else
    log_info "verify: disabled"
fi
log_info "result: OK"
