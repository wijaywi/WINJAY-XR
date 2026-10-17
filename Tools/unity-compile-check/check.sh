#!/usr/bin/env bash
# Offline compile check for the Unity layer.
#
# Compiles every script under Unity/WinjayXR/Assets with the same Roslyn the
# dotnet SDK ships, against the REAL Unity reference assemblies installed at
# D:\Unity\6000.0.42f1 (UnityEngine*/UnityEditor* modules, netstandard 2.1
# profile) plus Tools/unity-compile-check/Stubs.cs for the package-only types
# (InputSystem / Meta XR SDK / OpenXR / XR Management) that cannot be
# downloaded without opening the project in the GUI once.
#
# Two passes:
#   1. Editor shape: everything + UNITY_EDITOR defined (what the editor compiles).
#   2. Build shape:  runtime scripts only, no editor defines (what ships), so
#                    #if !UNITY_EDITOR device branches are compiled too.
#
# This catches C# syntax and API-shape errors without a Unity license.
set -e

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
ASSETS="$ROOT/Unity/WinjayXR/Assets"
HERE="$ROOT/Tools/unity-compile-check"
UNITY_EDITOR_UNIX="${UNITY_EDITOR_DIR:-/d/Unity/6000.0.42f1/Editor}"
OUT="${OUT:-$HERE/check.dll}"
OUT2="${OUT2:-$HERE/check-noeditor.dll}"
RSP="$HERE/check.rsp"
RSP2="$HERE/check-noeditor.rsp"

# MSYS does not rewrite paths inside a response file, so all entries there
# must be Windows-style. winpath() converts /d/x -> D:/x.
winpath() { echo "$1" | sed 's|^/\([a-zA-Z]\)/|\1:/|'; }

if [ ! -d "$UNITY_EDITOR_UNIX/Data/Managed" ]; then
  echo "Unity editor not found at $UNITY_EDITOR_UNIX (set UNITY_EDITOR_DIR)" >&2
  exit 2
fi

SDK_VER="$(dotnet --list-sdks | tail -1 | cut -d' ' -f1)"
SDK_ROOT="$(dotnet --list-sdks | tail -1 | sed 's/.*\[\(.*\)\]/\1/')"
SDK_ROOT_UNIX="$(echo "$SDK_ROOT" | sed 's|\\|/|g; s|^\([a-zA-Z]\):|/\L\1|')"
CSC="$SDK_ROOT_UNIX/$SDK_VER/Roslyn/bincore/csc.dll"

if [ ! -f "$CSC" ]; then
  echo "csc.dll not found at $CSC" >&2
  exit 2
fi

# build_rsp <out-dll> <defines> <skip-editor-folder:yes|no> <rsp-path>
# Prints the number of source files included.
build_rsp() {
  local out="$1" defines="$2" skipEditor="$3" rsp="$4" count=0
  {
    echo "-nologo"
    echo "-nostdlib"
    echo "-target:library"
    echo "-langversion:9.0"
    echo "-nullable:disable"
    echo "-out:\"$(winpath "$out")\""
    echo "-define:$defines"
    echo "-r:\"$(winpath "$UNITY_EDITOR_UNIX/Data/NetStandard/ref/2.1.0/netstandard.dll")\""
    for dll in "$UNITY_EDITOR_UNIX"/Data/Managed/UnityEngine/*.dll; do
      echo "-r:\"$(winpath "$dll")\""
    done
    while IFS= read -r cs; do
      if [ "$skipEditor" = "yes" ] && [[ "$cs" == */Assets/Scripts/Editor/* ]]; then
        continue
      fi
      echo "\"$(winpath "$cs")\""
      count=$((count + 1))
    done < <(find "$ASSETS" -name "*.cs" | sort)
    echo "\"$(winpath "$HERE/Stubs.cs")\""
  } > "$rsp"
  echo "$count"
}

COUNT="$(build_rsp "$OUT" "UNITY_6000_0;UNITY_2021_2_OR_NEWER;UNITY_STANDALONE;UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_64" "no" "$RSP")"
echo "Compiling $COUNT Winjay scripts (plus stubs) against $UNITY_EDITOR_UNIX ..."
dotnet "$CSC" "@$(winpath "$RSP")"
echo "COMPILE OK (editor shape) -> $OUT"

COUNT2="$(build_rsp "$OUT2" "UNITY_6000_0;UNITY_2021_2_OR_NEWER;UNITY_STANDALONE;UNITY_64" "yes" "$RSP2")"
echo "Compiling $COUNT2 runtime scripts without UNITY_EDITOR (build shape) ..."
dotnet "$CSC" "@$(winpath "$RSP2")"
echo "COMPILE OK (build shape) -> $OUT2"
