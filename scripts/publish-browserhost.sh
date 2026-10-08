#!/usr/bin/env bash
set -euo pipefail
CONFIGURATION="${1:-Release}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/BrowserHost/SmartphoneMyTube.BrowserHost.csproj"
OUT="$ROOT/browserhost"

for rid in win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64; do
  echo "Publishing BrowserHost for $rid"
  dotnet publish "$PROJECT" -c "$CONFIGURATION" -r "$rid" --self-contained true -o "$OUT/$rid"
done

echo "NOTE: CEF native runtime/helper packaging still needs to be validated per platform."
