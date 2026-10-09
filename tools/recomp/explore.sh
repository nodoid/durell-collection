#!/bin/zsh
# Run a translated game (Durell.Runner arguments) and retranslate until it no
# longer reaches untranslated code.   usage: explore.sh GAME FRAMES [runner args...]
cd "$(dirname $0)/../.."
g=$1; shift
for i in $(seq 1 40); do
  python3 tools/recomp/recomp.py $g | head -1
  dotnet build tools/Durell.Runner -c Release -v q -nologo 2>&1 | grep -E " error " | head -5
  dotnet tools/Durell.Runner/bin/Release/net10.0/Durell.Runner.dll $g "$@" | grep -v "^writes to code"
  [ ${pipestatus[1]} -ne 2 ] && break
done
