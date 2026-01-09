#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BUILD_DIR="$PROJECT_ROOT/build"

PREFIX="${PREFIX:-/usr/local}"
BINDIR="$PREFIX/bin"
LIBDIR="$PREFIX/lib"
DATADIR="$PREFIX/share"

echo "==================================="
echo "CapFrameX Linux Install Script"
echo "==================================="
echo "Install prefix: $PREFIX"
echo ""

# Check if build exists
if [ ! -f "$BUILD_DIR/bin/capframex-daemon" ]; then
    echo "Error: Build not found. Run ./scripts/build.sh first."
    exit 1
fi

# Check for root
if [ "$EUID" -ne 0 ]; then
    echo "This script requires root privileges."
    echo "Run with: sudo ./scripts/install.sh"
    exit 1
fi

# Install daemon
echo "Installing daemon..."
install -Dm755 "$BUILD_DIR/bin/capframex-daemon" "$BINDIR/capframex-daemon"

# Install Vulkan layer
echo "Installing Vulkan layer..."
install -Dm755 "$BUILD_DIR/lib/libcapframex_layer.so" "$LIBDIR/libcapframex_layer.so"

# Install layer manifest
echo "Installing layer manifest..."
mkdir -p "$DATADIR/vulkan/implicit_layer.d"

# Update library path in manifest
sed "s|/usr/lib/libcapframex_layer.so|$LIBDIR/libcapframex_layer.so|g" \
    "$PROJECT_ROOT/src/layer/capframex_layer.json" \
    > "$DATADIR/vulkan/implicit_layer.d/capframex_layer.json"

# Install systemd service
echo "Installing systemd service..."
mkdir -p /usr/lib/systemd/user
install -Dm644 "$PROJECT_ROOT/scripts/capframex-daemon.service" \
    /usr/lib/systemd/user/capframex-daemon.service

# Install .NET app (if published)
if [ -d "$BUILD_DIR/publish" ]; then
    echo "Installing application..."
    mkdir -p "$PREFIX/lib/capframex"
    cp -r "$BUILD_DIR/publish/"* "$PREFIX/lib/capframex/"

    # Create launcher script
    cat > "$BINDIR/capframex" << 'EOF'
#!/bin/bash
exec /usr/local/lib/capframex/CapFrameX.App "$@"
EOF
    chmod +x "$BINDIR/capframex"

    # Create desktop entry
    mkdir -p "$DATADIR/applications"
    cat > "$DATADIR/applications/capframex.desktop" << EOF
[Desktop Entry]
Name=CapFrameX
Comment=Frametime Capture and Analysis
Exec=$BINDIR/capframex
Icon=capframex
Terminal=false
Type=Application
Categories=Utility;System;
EOF
fi

echo ""
echo "==================================="
echo "Installation complete!"
echo "==================================="
echo ""
echo "To enable the daemon service:"
echo "  systemctl --user daemon-reload"
echo "  systemctl --user enable --now capframex-daemon"
echo ""
echo "To run the application:"
echo "  capframex"
echo ""
