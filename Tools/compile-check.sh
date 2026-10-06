#!/usr/bin/env bash
# Offline compile check for Assets/_Vaanam scripts using Unity's bundled Roslyn.
# Why: the Unity Editor holds a lock on the project while open, so we cannot
# run a second batchmode Unity. Compiling against the editor's own assemblies
# catches 'error CS' problems in seconds without touching the editor.
# Usage: Tools/compile-check.sh        (from Git Bash)
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
U="/c/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor"
OUT="$ROOT/Temp/compile-check"; mkdir -p "$OUT"; rm -f "$OUT"/*.dll
DEFINES="UNITY_6000_3_OR_NEWER;UNITY_6000_0_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_2022_1_OR_NEWER;UNITY_2021_1_OR_NEWER;UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_STANDALONE_WIN;UNITY_STANDALONE;ENABLE_INPUT_SYSTEM;VAANAM_MEDIAPIPE"

common_rsp() {
  echo "-nologo"; echo "-noconfig"; echo "-target:library"; echo "-langversion:9.0"
  echo "-nowarn:CS1701,CS1702,CS0169,CS0649,CS0414"
  echo "-define:$DEFINES"
  for d in "$U/Data/Managed/UnityEngine/"*.dll; do echo "-r:\"$(cygpath -w "$d")\""; done
  for n in Unity.InputSystem Mediapipe.Runtime UnityEngine.UI Unity.TextMeshPro Unity.RenderPipelines.Universal.Runtime Unity.RenderPipelines.Core.Runtime Unity.Mathematics Unity.Collections; do
    f="$ROOT/Library/ScriptAssemblies/$n.dll"; [ -f "$f" ] && echo "-r:\"$(cygpath -w "$f")\""
  done
  echo "-r:\"$(cygpath -w "$U/Data/NetStandard/ref/2.1.0/netstandard.dll")\""
}

# Runtime assembly
{
  common_rsp
  echo "-out:\"$(cygpath -w "$OUT/Vaanam.dll")\""
  find "$ROOT/Assets/_Vaanam" -name "*.cs" -not -path "*/Editor/*" | while read -r f; do echo "\"$(cygpath -w "$f")\""; done
} > "$OUT/runtime.rsp"
"$U/Data/NetCoreRuntime/dotnet.exe" "$U/Data/DotNetSdkRoslyn/csc.dll" "@$(cygpath -w "$OUT/runtime.rsp")" 2>&1 | grep -E "error|warning CS" | sort -u

# Editor assembly
if [ -f "$OUT/Vaanam.dll" ] && find "$ROOT/Assets/_Vaanam" -name "*.cs" -path "*/Editor/*" | grep -q .; then
  {
    common_rsp
    echo "-r:\"$(cygpath -w "$ROOT/Library/ScriptAssemblies/UnityEditor.UI.dll")\""; echo "-r:\"$(cygpath -w "$ROOT/Library/ScriptAssemblies/Unity.RenderPipelines.Universal.Editor.dll")\""; echo "-r:\"$(cygpath -w "$ROOT/Library/ScriptAssemblies/Unity.RenderPipelines.Core.Editor.dll")\""
    echo "-r:\"$(cygpath -w "$OUT/Vaanam.dll")\""
    echo "-out:\"$(cygpath -w "$OUT/Vaanam.Editor.dll")\""
    find "$ROOT/Assets/_Vaanam" -name "*.cs" -path "*/Editor/*" | while read -r f; do echo "\"$(cygpath -w "$f")\""; done
  } > "$OUT/editor.rsp"
  "$U/Data/NetCoreRuntime/dotnet.exe" "$U/Data/DotNetSdkRoslyn/csc.dll" "@$(cygpath -w "$OUT/editor.rsp")" 2>&1 | grep -E "error|warning CS" | sort -u
fi
echo "compile-check: runtime $([ -f "$OUT/Vaanam.dll" ] && echo OK || echo FAILED), editor $([ -f "$OUT/Vaanam.Editor.dll" ] && echo OK || echo '(none/failed)')"
