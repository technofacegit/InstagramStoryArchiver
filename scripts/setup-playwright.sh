#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "Restoring and building..."
dotnet restore
dotnet build -c Debug

PLAYWRIGHT_PS1="src/InstagramStoryArchiver.Infrastructure/bin/Debug/net8.0/playwright.ps1"
if [[ -f "$PLAYWRIGHT_PS1" ]]; then
  echo "Installing Chromium via Playwright script..."
  if command -v pwsh >/dev/null 2>&1; then
    pwsh "$PLAYWRIGHT_PS1" install chromium
  else
    echo "pwsh not found. Trying Microsoft.Playwright.CLI..."
    dotnet tool install --global Microsoft.Playwright.CLI || true
    export PATH="$PATH:$HOME/.dotnet/tools"
    playwright install chromium
  fi
else
  echo "playwright.ps1 not found after build. Install CLI manually:"
  echo "  dotnet tool install --global Microsoft.Playwright.CLI && playwright install chromium"
  exit 1
fi

mkdir -p data archive logs data/tmp
echo "Setup complete."
echo "Next: dotnet run --project src/InstagramStoryArchiver.Worker -- login"
