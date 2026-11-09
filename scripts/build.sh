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

usage() {
    cat <<'EOF'
Usage: bash scripts/build.sh [options]

Build (and optionally verify) TinyStudio publish variants for linux-x64.
Runs `dotnet restore` and sets the CI build env vars itself.

Options:
  --verify             Verify AOT variants contain compiled Avalonia XAML.
  --variant, -v NAME   Build only one variant (default: all).
  -h, --help           Show this help and exit.

Variants (linux-x64):
  all         net11 + net11-aot (default)
  net11       plain .NET 11 publish
  net11-aot   native AOT .NET 11 publish

Notes:
  * net10 / net10-aot are Windows-only and are rejected here.
  * Verification applies to AOT variants only; others are skipped with a notice.
  * Requires the .NET 11 SDK (setup-dotnet 11.0.x in CI).
EOF
}

VERIFY=0
VARIANT="all"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --verify) VERIFY=1; shift ;;
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

# Variant table: Name|Tfm|Aot|Out
# Order is load-bearing: plain before AOT for each TFM. net11 and net11-aot
# share the same obj dir, so the AOT publish must be the last one for its TFM.
VARIANTS=(
    "net11|net11.0|0|net11"
    "net11-aot|net11.0|1|net11-aot"
)
case "$VARIANT" in
    all)       SELECTED=("${VARIANTS[@]}") ;;
    net11)     SELECTED=("${VARIANTS[0]}") ;;
    net11-aot) SELECTED=("${VARIANTS[1]}") ;;
    net10|net10-aot)
        log_error "variant '$VARIANT' is Windows-only; Linux supports: all, net11, net11-aot"
        exit 2
        ;;
    *)
        log_error "unknown variant '$VARIANT'"
        usage
        exit 2
        ;;
esac

log_info ">>> Restoring"
dotnet restore "$PROJECT" || {
    rc=$?
    log_error "restore failed (exit $rc)"
    exit "$rc"
}

BUILT=""
for rec in "${SELECTED[@]}"; do
    IFS='|' read -r name tfm aot out <<< "$rec"
    log_info ">>> Building $name (tfm=$tfm, aot=$aot, rid=$RID)"

    objDir="TinyStudio/obj/$CONFIG/$tfm/$RID"
    rm -rf "$objDir"

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

    dotnet "${publishArgs[@]}" || {
        rc=$?
        log_error "publish failed for $name (exit $rc)"
        exit "$rc"
    }
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
        IFS='|' read -r name tfm aot out <<< "$rec"
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
