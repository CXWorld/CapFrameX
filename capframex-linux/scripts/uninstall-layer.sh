#!/bin/bash
# Uninstall CapFrameX Vulkan layer

set -e

# System-wide installation paths
SYSTEM_LIB="/usr/lib/libcapframex_layer.so"
SYSTEM_MANIFEST="/usr/share/vulkan/implicit_layer.d/capframex_layer.json"

# User-local installation path
USER_MANIFEST_DIR="$HOME/.local/share/vulkan/implicit_layer.d"

echo "Uninstalling CapFrameX Vulkan layer..."

# Remove system-wide layer library
if [ -f "$SYSTEM_LIB" ]; then
    echo "Removing $SYSTEM_LIB..."
    sudo rm "$SYSTEM_LIB"
else
    echo "System layer library not found at $SYSTEM_LIB (already removed?)"
fi

# Remove system-wide manifest
if [ -f "$SYSTEM_MANIFEST" ]; then
    echo "Removing $SYSTEM_MANIFEST..."
    sudo rm "$SYSTEM_MANIFEST"
else
    echo "System manifest not found at $SYSTEM_MANIFEST (already removed?)"
fi

# Remove user-local manifests (may be multiple from development builds)
if [ -d "$USER_MANIFEST_DIR" ]; then
    for manifest in "$USER_MANIFEST_DIR"/capframex*.json; do
        if [ -f "$manifest" ]; then
            echo "Removing user-local manifest: $manifest"
            rm "$manifest"
        fi
    done
fi

echo ""
echo "Done! CapFrameX Vulkan layer uninstalled."
