#!/usr/bin/env bash
# Builds the frontend and publishes self-contained single-file zips: scripts/publish.sh [rid ...]
# Default RIDs: win-x64 linux-x64 linux-arm64. Output: artifacts/kestry-<rid>.zip
# aria2 is never bundled: Windows downloads it on first run, Linux uses the package manager.
set -euo pipefail
cd "$(dirname "$0")/.."

(cd src/kestry-web && npm ci && npm run build)

rids=("$@")
[ ${#rids[@]} -eq 0 ] && rids=(win-x64 linux-x64 linux-arm64)
mkdir -p artifacts
for rid in "${rids[@]}"; do
  out="artifacts/publish/$rid"
  rm -rf "$out"
  dotnet publish src/Kestry.Server -c Release -r "$rid" --self-contained \
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o "$out"
  rm -f "$out"/*.pdb
  (cd "$out" && zip -qr "../../kestry-$rid.zip" .)
  echo "artifacts/kestry-$rid.zip"
done
