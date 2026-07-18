#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "Restoring and building..."
dotnet restore
dotnet build -c Debug

PACKAGE_DIR="$HOME/.nuget/packages/microsoft.playwright"
VERSION="$(ls -1 "$PACKAGE_DIR" 2>/dev/null | sort -V | tail -1 || true)"
if [[ -z "${VERSION}" ]]; then
  echo "Microsoft.Playwright package not found under $PACKAGE_DIR"
  exit 1
fi

ARCH="$(uname -m)"
case "$(uname -s)-${ARCH}" in
  Darwin-arm64) NODE_DIR="darwin-arm64" ;;
  Darwin-*) NODE_DIR="darwin-x64" ;;
  Linux-aarch64|Linux-arm64) NODE_DIR="linux-arm64" ;;
  Linux-*) NODE_DIR="linux-x64" ;;
  *) echo "Unsupported platform"; exit 1 ;;
esac

NODE="${PACKAGE_DIR}/${VERSION}/.playwright/node/${NODE_DIR}/node"
CLI="${PACKAGE_DIR}/${VERSION}/.playwright/package/cli.js"

if [[ ! -x "$NODE" || ! -f "$CLI" ]]; then
  echo "Playwright driver not found at $NODE"
  exit 1
fi

echo "Installing Chromium via Playwright driver..."
"$NODE" "$CLI" install chromium

mkdir -p data archive logs data/tmp
echo "Setup complete."
echo "Next: dotnet run --project src/InstagramStoryArchiver.Worker -- login"
