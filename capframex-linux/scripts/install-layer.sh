#!/bin/bash
# Install CapFrameX Vulkan layer globally for Proton/Steam compatibility

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_LIB="$PROJECT_DIR/build/lib/libcapframex_layer.so"
INSTALL_LIB="/usr/lib/libcapframex_layer.so"
MANIFEST_DIR="/usr/share/vulkan/implicit_layer.d"
MANIFEST_FILE="$MANIFEST_DIR/capframex_layer.json"

echo "Installing CapFrameX Vulkan layer globally..."

# Check if layer exists
if [ ! -f "$BUILD_LIB" ]; then
    echo "Error: Layer not found at $BUILD_LIB"
    echo "Please build the project first: cd build && cmake --build ."
    exit 1
fi

# Install layer library
echo "Copying layer to /usr/lib/..."
sudo cp "$BUILD_LIB" "$INSTALL_LIB"

# Install layer manifest
echo "Creating manifest at $MANIFEST_FILE..."
sudo mkdir -p "$MANIFEST_DIR"
sudo tee "$MANIFEST_FILE" > /dev/null << 'MANIFEST'
{
    "file_format_version": "1.0.0",
    "layer": {
        "name": "VK_LAYER_capframex_capture",
        "type": "GLOBAL",
        "library_path": "/usr/lib/libcapframex_layer.so",
        "api_version": "1.3.0",
        "implementation_version": "1",
        "description": "CapFrameX Frametime Capture Layer",
        "disable_environment": {
            "DISABLE_CAPFRAMEX_LAYER": "1"
        }
    }
}
MANIFEST

echo ""
echo "Done! Layer installed globally."
echo "  Library: $INSTALL_LIB"
echo "  Manifest: $MANIFEST_FILE"
echo ""
echo "For Steam/Proton games, use launch options:"
echo "  PRESSURE_VESSEL_FILESYSTEMS_RW=/tmp %command%"
