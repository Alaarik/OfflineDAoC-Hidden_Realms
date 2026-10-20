#!/bin/bash
# ==============================================================================
# Offline DAoC: Hidden Realms - Client Launcher (macOS via Wine)
# ==============================================================================

cd "$(dirname "$0")/playable-v0.35b/runtime/client-opendaoc/app" || exit 1

ACCOUNT="${1:-testadmin}"
PASSWORD="${2:-testpass}"

echo "=========================================================="
echo " Launching Offline DAoC: Hidden Realms Client             "
echo " Account:  $ACCOUNT"
echo " Server:   127.0.0.1:10300"
echo "=========================================================="
echo "Note: AutoAccountCreation is enabled. If this account does"
echo "not exist yet, it will be automatically registered."
echo ""

export DYLD_FALLBACK_LIBRARY_PATH="/Users/kyleborst/Applications/DAoC.app/Contents/Frameworks"
export WINEPREFIX="/Users/kyleborst/Applications/DAoC.app/Contents/SharedSupport/prefix"
WINE_BIN="/Users/kyleborst/Applications/DAoC.app/Contents/SharedSupport/wine/bin/wine64"

"$WINE_BIN" connect.exe "game.dll" 127.0.0.1:10300 "$ACCOUNT" "$PASSWORD"
