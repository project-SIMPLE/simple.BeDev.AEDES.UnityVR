#!/usr/bin/env bash
# Build an AEDES module into a sideloadable APK.
#
#   Tools/build.sh <1|2|tut2> [--clean]
#
# The player build IS the APK - Unity writes a signed .apk directly, so there is no
# separate packaging step.

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/aedes-common.sh"

MODULE="${1:-}"
CLEAN=""
[ -z "$MODULE" ] && { echo "usage: Tools/build.sh <1|2|tut2> [--clean]" >&2; usage_modules; exit 1; }
shift
for arg in "$@"; do
  case "$arg" in
    --clean) CLEAN="-clean" ;;
    *) die "unknown option '$arg'" ;;
  esac
done

require_unity
cd "$REPO_ROOT"

SCENE="$(module_scene "$MODULE")"
APK="$(module_apk "$MODULE")"
LOG="Logs/build/module-$MODULE.log"
mkdir -p "$(dirname "$LOG")" "$(dirname "$APK")"

# A stale editor holding the project lock makes the batchmode build fail in confusing ways.
if [ -f "Temp/UnityLockfile" ] && ! pgrep -f "MacOS/Unity .*-projectPath .*$(basename "$REPO_ROOT")" >/dev/null 2>&1; then
  rm -f Temp/UnityLockfile
fi

run_unity() {
  local extra="$1"
  rm -f "$LOG"
  # shellcheck disable=SC2086
  "$UNITY_APP" -batchmode -nographics -quit \
    -projectPath "$REPO_ROOT" \
    -buildTarget Android \
    -executeMethod AedesBuild.BuildAndroid \
    -scenes "$SCENE" \
    -outputPath "$APK" \
    $extra \
    -logFile "$LOG" || true
  # Unity's batchmode exit code is unreliable (it has returned 0 for cancelled dialogs and
  # aborted package resolution), so success is read from the log and the artifact instead.
}

# Building rewrites these two tracked files as a side effect of the embedded XRI package.
revert_build_noise() {
  git checkout -q -- Packages/packages-lock.json 2>/dev/null || true
  git checkout -q -- ./*.slnx 2>/dev/null || true
}

check_result() {
  grep -q '\[AedesBuild\] result=Succeeded' "$LOG" && [ -f "$APK" ]
}

info "Building module $MODULE"
info "  scene:  $SCENE"
info "  output: $APK"
[ -n "$CLEAN" ] && info "  clean build (IL2CPP recompiles from scratch - several minutes)"

run_unity "$CLEAN"
revert_build_noise

if ! check_result; then
  echo >&2
  die "build failed - see $LOG
$(grep -E 'error CS|Error building|cannot be extracted by the YAML Parser' "$LOG" 2>/dev/null | sort -u | head -5)"
fi

# Verify real compiled code made it in, not a stub from a poisoned IL2CPP cache.
IL2CPP_SIZE="$(apk_il2cpp_size "$APK")" || {
  if [ -z "$CLEAN" ]; then
    warn "libil2cpp.so is only ${IL2CPP_SIZE} bytes - the IL2CPP cache is corrupt."
    warn "Rebuilding with --clean automatically."
    run_unity "-clean"
    revert_build_noise
    check_result || die "clean build failed - see $LOG"
    IL2CPP_SIZE="$(apk_il2cpp_size "$APK")" \
      || die "libil2cpp.so is still only ${IL2CPP_SIZE} bytes after a clean build - see $LOG"
  else
    die "libil2cpp.so is only ${IL2CPP_SIZE} bytes after a clean build - see $LOG"
  fi
}

ok "$APK ($(du -h "$APK" | cut -f1)), libil2cpp.so $((IL2CPP_SIZE / 1024 / 1024)) MB"
grep -E '\[AedesBuild\] result=' "$LOG" | tail -1
echo "Deploy it with: Tools/deploy.sh $MODULE"
