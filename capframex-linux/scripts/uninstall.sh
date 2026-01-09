#!/bin/bash
set -e

PREFIX="${PREFIX:-/usr/local}"
BINDIR="$PREFIX/bin"
LIBDIR="$PREFIX/lib"
DATADIR="$PREFIX/share"

echo "==================================="
echo "CapFrameX Linux Uninstall Script"
echo "==================================="

# Check for root
if [ "$EUID" -ne 0 ]; then
    echo "This script requires root privileges."
    echo "Run with: sudo ./scripts/uninstall.sh"
    exit 1
fi

# Stop service
echo "Stopping service..."
systemctl --user stop capframex-daemon 2>/dev/null || true
systemctl --user disable capframex-daemon 2>/dev/null || true

# Remove files
echo "Removing files..."

rm -f "$BINDIR/capframex-daemon"
rm -f "$BINDIR/capframex"
rm -f "$LIBDIR/libcapframex_layer.so"
rm -f "$DATADIR/vulkan/implicit_layer.d/capframex_layer.json"
rm -f /usr/lib/systemd/user/capframex-daemon.service
rm -f "$DATADIR/applications/capframex.desktop"
rm -rf "$PREFIX/lib/capframex"

echo ""
echo "==================================="
echo "Uninstallation complete!"
echo "==================================="
