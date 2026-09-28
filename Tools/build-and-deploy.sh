#!/usr/bin/env bash
# Build a module and put it on the attached headset in one command.
#
#   Tools/build-and-deploy.sh <1|2|tut2> [--clean]

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODULE="${1:-}"
[ -z "$MODULE" ] && { echo "usage: Tools/build-and-deploy.sh <1|2|tut2> [--clean]" >&2; exit 1; }
shift
"$HERE/build.sh" "$MODULE" "$@" && "$HERE/deploy.sh" "$MODULE"
