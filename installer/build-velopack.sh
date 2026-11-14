#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-}"
RUNTIME="${2:-osx-arm64}" # osx-arm64, osx-x64, linux-x64
CONFIGURATION="Release"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/src/NovelTranslator.App/NovelTranslator.App.csproj"
if [ -z "$VERSION" ]; then
  VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT_PATH" | head -n 1)
fi
PUBLISH_DIR="$ROOT_DIR/publish/$RUNTIME"
RELEASES_DIR="$SCRIPT_DIR/Releases/$RUNTIME"

echo "=== 1. Publish แบบ Self-Contained ($RUNTIME) ==="
dotnet publish "$PROJECT_PATH" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained true \
  -o "$PUBLISH_DIR"

if ! command -v vpk >/dev/null 2>&1; then
  echo "ไม่พบคำสั่ง 'vpk' (Velopack CLI) กรุณาติดตั้งด้วย: dotnet tool install -g vpk" >&2
  exit 1
fi

echo "=== 2. สร้าง Installer ด้วย Velopack (v$VERSION) ==="
mkdir -p "$RELEASES_DIR"

vpk pack \
  --packId "NovelTranslator" \
  --packTitle "NovelTranslator" \
  --packVersion "$VERSION" \
  --packDir "$PUBLISH_DIR" \
  --mainExe "NovelTranslator.App" \
  --outputDir "$RELEASES_DIR"

echo "สร้าง Installer สำเร็จ! ไฟล์อยู่ที่: $RELEASES_DIR"