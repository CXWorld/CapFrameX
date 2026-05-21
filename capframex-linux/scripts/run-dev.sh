#!/bin/bash
# Development run script - runs daemon and app without installing

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BUILD_DIR="$PROJECT_ROOT/build"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

# Check if built
if [ ! -f "$BUILD_DIR/bin/capframex-daemon" ]; then
    echo -e "${RED}Error: Build not found. Run ./scripts/build.sh first.${NC}"
    exit 1
fi

# Export layer path for Vulkan
export VK_LAYER_PATH="$PROJECT_ROOT/src/layer:${VK_LAYER_PATH}"
export ENABLE_CAPFRAMEX_LAYER=1

# Create runtime directory
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"

cleanup() {
    echo ""
    echo "Shutting down..."
    if [ ! -z "$DAEMON_PID" ]; then
        kill $DAEMON_PID 2>/dev/null
    fi
    exit 0
}

trap cleanup SIGINT SIGTERM

# Start daemon in background
echo -e "${GREEN}Starting daemon...${NC}"
"$BUILD_DIR/bin/capframex-daemon" -f &
DAEMON_PID=$!

sleep 1

# Check if daemon started
if ! kill -0 $DAEMON_PID 2>/dev/null; then
    echo -e "${RED}Failed to start daemon${NC}"
    exit 1
fi

echo -e "${GREEN}Daemon running (PID: $DAEMON_PID)${NC}"
echo ""

# Start the app
echo -e "${GREEN}Starting application...${NC}"
cd "$PROJECT_ROOT/src/app"
dotnet run --project CapFrameX.App

# Cleanup on exit
cleanup
