#!/bin/bash
# ==============================================================================
# Offline DAoC: Hidden Realms - Server Launcher (macOS Apple Silicon / Intel)
# ==============================================================================

cd "$(dirname "$0")/playable-v0.35b/runtime/server" || exit 1

echo "=========================================================="
echo " Starting Offline DAoC: Hidden Realms Server (macOS)     "
echo " .NET Runtime: ~/.dotnet-x64/dotnet (Rosetta 2 / SQLite)  "
echo " Listening on: 127.0.0.1:10300                            "
echo "=========================================================="
echo ""

/Users/kyleborst/.dotnet-x64/dotnet CoreServer.dll --start -config=./config/serverconfig.xml
