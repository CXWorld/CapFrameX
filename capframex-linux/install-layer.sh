#!/bin/bash
# Install CapFrameX Vulkan layer system-wide (required for Proton games)

set -e

LAYER_SO="/home/capframex/Code/CapFrameX/capframex-linux/build/lib/libcapframex_layer.so"
INSTALL_DIR="/usr/lib/capframex"
MANIFEST_DIR="/usr/share/vulkan/implicit_layer.d"

echo "Installing CapFrameX Vulkan layer..."

sudo mkdir -p "$INSTALL_DIR"
sudo cp "$LAYER_SO" "$INSTALL_DIR/"

sudo mkdir -p "$MANIFEST_DIR"
sudo tee "$MANIFEST_DIR/capframex_layer.json" > /dev/null << 'MANIFEST'
{
    "file_format_version": "1.0.0",
    "layer": {
        "name": "VK_LAYER_capframex_capture",
        "type": "GLOBAL",
        "library_path": "/usr/lib/capframex/libcapframex_layer.so",
        "api_version": "1.3.0",
        "implementation_version": "1",
        "description": "CapFrameX Frametime Capture Layer",
        "disable_environment": {
            "DISABLE_CAPFRAMEX_LAYER": "1"
        }
    }
}
MANIFEST

echo "✓ Layer installed to $INSTALL_DIR"
echo "✓ Manifest installed to $MANIFEST_DIR"
echo ""
echo "Done! Layer will auto-load for all Vulkan games including Proton."
