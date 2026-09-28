#!/usr/bin/env bash
# Shared helpers for Tools/build.sh and Tools/deploy.sh. Sourced, never run directly.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Pin the editor to what the project is serialized with. Opening with another version
# rewrites asset files and produces enormous, unreviewable diffs.
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$REPO_ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
UNITY_APP="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
ANDROID_SDK="/Applications/Unity/Hub/Editor/$UNITY_VERSION/PlaybackEngines/AndroidPlayer/SDK"

# adb ships inside the editor's Android SDK and is not on PATH by default.
if [ -x "$ANDROID_SDK/platform-tools/adb" ]; then
  ADB="$ANDROID_SDK/platform-tools/adb"
else
  ADB="$(command -v adb || true)"
fi

die() { printf '\033[31merror:\033[0m %s\n' "$*" >&2; exit 1; }
info() { printf '\033[36m==>\033[0m %s\n' "$*"; }
ok()   { printf '\033[32mok:\033[0m %s\n' "$*"; }
warn() { printf '\033[33mwarning:\033[0m %s\n' "$*" >&2; }

usage_modules() {
  cat >&2 <<'USAGE'
modules:
  1      Module 1 - play as the mosquito   (Module_1_MainScene)
  2      Module 2 - household vector control (Module_2_MainScene)
  tut2   Module 2 tutorial                 (Tutorial_M2)
USAGE
}

# Map a module key to its scene file. Located by filename rather than a fixed path so
# that folder reorganisations (GameTesting -> Module1, Phettae -> Module2) do not break it.
module_scene() {
  local key="$1" name
  case "$key" in
    1)    name="Module_1_MainScene" ;;
    2)    name="Module_2_MainScene" ;;
    tut2) name="Tutorial_M2" ;;
    *)    usage_modules; die "unknown module '$key'" ;;
  esac

  local matches count
  matches="$(cd "$REPO_ROOT" && find Assets/1.TeamWorkspace -name "$name.unity" -not -path '*/_Recovery/*')"
  count="$(printf '%s' "$matches" | grep -c . || true)"

  [ "$count" -eq 0 ] && die "scene $name.unity not found under Assets/1.TeamWorkspace"
  if [ "$count" -gt 1 ]; then
    warn "several copies of $name.unity found; using the first:"
    printf '%s\n' "$matches" >&2
  fi
  printf '%s\n' "$matches" | head -1
}

module_apk() {
  case "$1" in
    1)    printf 'build/Android/AEDES-Module1.apk\n' ;;
    2)    printf 'build/Android/AEDES-Module2.apk\n' ;;
    tut2) printf 'build/Android/AEDES-TutorialM2.apk\n' ;;
    *)    usage_modules; die "unknown module '$1'" ;;
  esac
}

# libil2cpp.so holds every compiled C# script. A corrupt IL2CPP cache yields a few-KB stub
# while the build still reports success, producing an APK that installs, starts the engine
# and then dies with no game code. Real builds are tens of MB.
IL2CPP_MIN_BYTES=$((20 * 1024 * 1024))

# Echoes libil2cpp.so's size; returns non-zero if it is missing or stub-sized.
apk_il2cpp_size() {
  local apk="$1" size
  size="$(unzip -l "$apk" 2>/dev/null | awk '$4 == "lib/arm64-v8a/libil2cpp.so" { print $1; exit }')"
  [ -z "$size" ] && { echo 0; return 1; }
  printf '%s\n' "$size"
  [ "$size" -ge "$IL2CPP_MIN_BYTES" ]
}

require_unity() {
  [ -x "$UNITY_APP" ] || die "Unity $UNITY_VERSION not found at $UNITY_APP
Install that exact version via Unity Hub - the project is serialized with it."
}

require_adb() {
  [ -n "$ADB" ] && [ -x "$ADB" ] || die "adb not found. Install the Android Build Support module for Unity $UNITY_VERSION."
}
