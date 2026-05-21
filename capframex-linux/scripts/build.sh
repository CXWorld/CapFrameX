#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BUILD_DIR="$PROJECT_ROOT/build"
BUILD_TYPE="${BUILD_TYPE:-Release}"

echo "==================================="
echo "CapFrameX Linux Build Script"
echo "==================================="
echo "Project root: $PROJECT_ROOT"
echo "Build type: $BUILD_TYPE"
echo ""

# Check dependencies
check_deps() {
    echo "Checking dependencies..."

    local missing=()

    command -v cmake >/dev/null 2>&1 || missing+=("cmake")
    command -v make >/dev/null 2>&1 || missing+=("make")
    command -v dotnet >/dev/null 2>&1 || missing+=("dotnet")

    if ! pkg-config --exists vulkan 2>/dev/null; then
        missing+=("libvulkan-dev")
    fi

    if [ ${#missing[@]} -ne 0 ]; then
        echo "Missing dependencies: ${missing[*]}"
        echo ""
        echo "Install with:"
        echo "  sudo apt install build-essential cmake libvulkan-dev"
        echo "  # Install .NET 8 SDK from https://dotnet.microsoft.com"
        exit 1
    fi

    echo "All dependencies found."
    echo ""
}

# Build native components (daemon and layer)
build_native() {
    echo "Building native components..."

    mkdir -p "$BUILD_DIR"
    cd "$BUILD_DIR"

    cmake "$PROJECT_ROOT" \
        -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
        -DBUILD_DAEMON=ON \
        -DBUILD_LAYER=ON

    make -j$(nproc)

    echo "Native build complete."
    echo "  Daemon: $BUILD_DIR/bin/capframex-daemon"
    echo "  Layer:  $BUILD_DIR/lib/libcapframex_layer.so"
    echo ""
}

# Build .NET application
build_dotnet() {
    echo "Building .NET application..."

    cd "$PROJECT_ROOT/src/app"

    dotnet restore
    dotnet build -c "$BUILD_TYPE"

    echo ".NET build complete."
    echo "  App: $PROJECT_ROOT/src/app/CapFrameX.App/bin/$BUILD_TYPE/net8.0/"
    echo ""
}

# Publish .NET application (self-contained)
publish_dotnet() {
    echo "Publishing .NET application..."

    cd "$PROJECT_ROOT/src/app"

    dotnet publish CapFrameX.App/CapFrameX.App.csproj \
        -c "$BUILD_TYPE" \
        -r linux-x64 \
        --self-contained true \
        -o "$BUILD_DIR/publish"

    echo "Publish complete."
    echo "  Published to: $BUILD_DIR/publish/"
    echo ""
}

# Main build
main() {
    check_deps
    build_native
    build_dotnet

    if [ "$1" == "--publish" ]; then
        publish_dotnet
    fi

    echo "==================================="
    echo "Build complete!"
    echo "==================================="
    echo ""
    echo "To install:"
    echo "  sudo ./scripts/install.sh"
    echo ""
    echo "To run without installing:"
    echo "  ./build/bin/capframex-daemon &"
    echo "  dotnet run --project src/app/CapFrameX.App"
}

main "$@"
