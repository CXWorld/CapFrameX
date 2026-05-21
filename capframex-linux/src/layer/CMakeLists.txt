set(LAYER_SOURCES
    layer.c
    swapchain.c
    timing.c
    data_export.c
    ipc_client.c
)

set(LAYER_HEADERS
    layer.h
    swapchain.h
    timing.h
    data_export.h
    ipc_client.h
)

add_library(capframex_layer SHARED ${LAYER_SOURCES} ${LAYER_HEADERS})

target_include_directories(capframex_layer PRIVATE
    ${Vulkan_INCLUDE_DIRS}
    ${CMAKE_SOURCE_DIR}/src/daemon  # For common.h
)

target_link_libraries(capframex_layer PRIVATE
    Vulkan::Vulkan
    Threads::Threads
    rt
)

target_compile_options(capframex_layer PRIVATE
    -Wall -Wextra -Wpedantic
    -fvisibility=hidden
)

target_compile_definitions(capframex_layer PRIVATE
    VK_LAYER_EXPORT=__attribute__\(\(visibility\(\"default\"\)\)\)
)

set_target_properties(capframex_layer PROPERTIES
    OUTPUT_NAME "capframex_layer"
    PREFIX "lib"
)

install(TARGETS capframex_layer
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    COMPONENT layer)
