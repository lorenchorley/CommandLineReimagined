#!/bin/bash
# SessionStart hook for Claude Code on the web.
#
# The solution targets net7.0 (and net7.0-windows for the WPF host), but no .NET
# SDK ships in the base image and Microsoft's CDN (builds.dotnet.microsoft.com)
# is blocked by the network policy. Ubuntu's own archive carries dotnet-sdk-8.0,
# which builds net7.0 fine -- it pulls the net7.0 reference packs from nuget.org.
set -euo pipefail

# Local machines are left alone; developers manage their own SDK there.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

SUDO=""
if [ "$(id -u)" -ne 0 ]; then
  SUDO="sudo"
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Installing .NET SDK 8 from the Ubuntu archive..."
  $SUDO apt-get update -qq || true
  $SUDO env DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-8.0
fi

echo "dotnet $(dotnet --version) ready"

# Only the .NET 8 runtime is available, and the default roll-forward policy will
# not cross a major version -- without this every `dotnet test` run aborts with
# "You must install or update .NET to run this application ... version '7.0.0'".
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo 'export DOTNET_ROLL_FORWARD=Major'
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
  } >> "$CLAUDE_ENV_FILE"
fi

export DOTNET_ROLL_FORWARD=Major
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
