cmake_minimum_required(VERSION 3.16)
project(capframex-linux VERSION 1.0.0 LANGUAGES C CXX)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Build options
option(BUILD_DAEMON "Build the game detection daemon" ON)
option(BUILD_LAYER "Build the Vulkan capture layer" ON)
option(BUILD_TESTS "Build tests" OFF)

# Find required packages
find_package(Vulkan REQUIRED)
find_package(Threads REQUIRED)

# Output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Add subdirectories
if(BUILD_DAEMON)
    add_subdirectory(src/daemon)
endif()

if(BUILD_LAYER)
    add_subdirectory(src/layer)
endif()

if(BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests)
endif()

# Installation
include(GNUInstallDirs)

# Install systemd service
install(FILES scripts/capframex-daemon.service
    DESTINATION ${CMAKE_INSTALL_LIBDIR}/systemd/user
    COMPONENT daemon)

# Install Vulkan layer manifest
install(FILES src/layer/capframex_layer.json
    DESTINATION ${CMAKE_INSTALL_DATAROOTDIR}/vulkan/implicit_layer.d
    COMPONENT layer)
