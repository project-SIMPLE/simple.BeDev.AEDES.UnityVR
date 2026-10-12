#!/usr/bin/env bash
# Install an already-built AEDES module onto an attached Quest and launch it.
#
#   Tools/deploy.sh <1|2|tut2> [--no-launch] [--wait <seconds>]
#
# Every module installs under the same application id, so only one can be on the headset
# at a time - deploying module 2 replaces module 1.

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/aedes-common.sh"

APP_ID="com.unity.template.vr"
ACTIVITY="$APP_ID/com.unity3d.player.UnityPlayerGameActivity"

MODULE="${1:-}"
LAUNCH=1
WAIT_SECS=180
[ -z "$MODULE" ] && { echo "usage: Tools/deploy.sh <1|2|tut2> [--no-launch] [--wait <seconds>]" >&2; usage_modules; exit 1; }
shift
while [ $# -gt 0 ]; do
  case "$1" in
    --no-launch) LAUNCH=0 ;;
    --wait) shift; WAIT_SECS="${1:-180}" ;;
    *) die "unknown option '$1'" ;;
  esac
  shift
done

require_adb
cd "$REPO_ROOT"
APK="$(module_apk "$MODULE")"
[ -f "$APK" ] || die "no APK at $APK - run: Tools/build.sh $MODULE"

# Refuse to ship a stub. Installing one wastes a test cycle: it installs and starts the
# engine, then dies silently because none of the C# is present.
IL2CPP_SIZE="$(apk_il2cpp_size "$APK")" \
  || die "$APK contains a ${IL2CPP_SIZE}-byte libil2cpp.so (no compiled game code).
Rebuild with: Tools/build.sh $MODULE --clean"

# An empty device list means no ADB interface at all, which is developer mode being off
# rather than a cable fault. "unauthorized" means the on-headset prompt is still pending.
STATE="$("$ADB" devices | awk 'NR>1 && NF>=2 { print $2; exit }')"
case "${STATE:-none}" in
  device) ;;
  unauthorized)
    die "headset is connected but unauthorized.
Put it on and accept 'Allow USB debugging', ticking 'Always allow from this computer'." ;;
  none)
    die "no headset detected over adb.
  - Developer mode must be ON (Meta Horizon phone app -> Devices -> Headset settings).
  - Use a data-capable USB-C cable.
  - Check the headset is awake, then re-run." ;;
  *) die "headset in unexpected adb state '$STATE'" ;;
esac

info "Installing $APK ($(du -h "$APK" | cut -f1))"
"$ADB" install -r "$APK" >/dev/null || die "adb install failed"
ok "installed: $("$ADB" shell dumpsys package "$APP_ID" 2>/dev/null | sed -n 's/.*lastUpdateTime=//p' | head -1)"

[ "$LAUNCH" -eq 0 ] && { echo "Skipping launch (--no-launch)."; exit 0; }

# Horizon OS refuses to start an app while the headset is asleep, and caches the launch
# when a system dialog is up, so retry rather than failing on the first attempt.
info "Launching (put the headset on if it is asleep; waiting up to ${WAIT_SECS}s)"
deadline=$(( $(date +%s) + WAIT_SECS ))
pid=""
while [ "$(date +%s)" -lt "$deadline" ]; do
  if [ "$("$ADB" shell dumpsys power 2>/dev/null | grep -o 'mWakefulness=[A-Za-z]*' | head -1)" = "mWakefulness=Awake" ]; then
    "$ADB" shell am start -n "$ACTIVITY" >/dev/null 2>&1 || true
    sleep 6
    pid="$("$ADB" shell pidof "$APP_ID" 2>/dev/null | tr -d '\r')"
    [ -n "$pid" ] && break
  fi
  sleep 5
done

if [ -z "$pid" ]; then
  warn "not running after ${WAIT_SECS}s. It is installed - launch it from the headset's"
  warn "app library under 'Unknown Sources' (it is named 'SIMPLE Template')."
  "$ADB" logcat -d -t 400 2>/dev/null | grep -iE 'Launch is blocked|Rejecting start' | tail -2 >&2 || true
  exit 1
fi

ok "running (pid $pid)"
CRASHES="$("$ADB" logcat -d -t 600 2>/dev/null \
  | grep -iE 'FATAL|AndroidRuntime:|NullReferenceException|MissingReference' \
  | grep -viE 'RuntimeInit|boot image|lock profiling|main entry|VM exiting' | tail -5 || true)"
if [ -n "$CRASHES" ]; then
  warn "errors in logcat:"
  printf '%s\n' "$CRASHES" >&2
else
  ok "no crashes or exceptions in logcat"
fi
