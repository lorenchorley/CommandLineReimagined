#!/bin/bash
# SessionStart hook for Claude Code on the web.
#
# The solution targets net10.0 (and net10.0-windows for the WPF host), but no .NET
# SDK ships in the base image and Microsoft's CDN (builds.dotnet.microsoft.com)
# is blocked by the network policy. Ubuntu's own archive carries dotnet-sdk-10.0,
# which is what global.json asks for.
set -euo pipefail

# Local machines are left alone; developers manage their own SDK there.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

SUDO=""
if [ "$(id -u)" -ne 0 ]; then
  SUDO="sudo"
fi

# `command -v dotnet` is not enough: the image may carry an older SDK than
# global.json accepts, and then every build fails with a version message instead.
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  echo "Installing .NET SDK 10 from the Ubuntu archive..."
  $SUDO apt-get update -qq || true
  $SUDO env DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0
fi

echo "dotnet $(dotnet --version) ready"

# The SDK and the target framework match now, so no roll-forward is needed.
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
  } >> "$CLAUDE_ENV_FILE"
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# Warm the NuGet cache so the first build of the session is fast. Application.csproj
# is skipped: WPF needs Microsoft.NET.Sdk.WindowsDesktop, which does not exist off
# Windows, so the project cannot even be evaluated here.
cd "${CLAUDE_PROJECT_DIR:-$(dirname "$0")/../..}"
for proj in $(find . -name '*.csproj' -not -path './.git/*' -not -name 'Application.csproj' | sort); do
  dotnet restore "$proj" --verbosity quiet || echo "warning: restore failed for $proj"
done

echo "Setup complete."
