set(DAEMON_SOURCES
    main.c
    process_monitor.c
    launcher_detect.c
    ipc.c
    config.c
)

set(DAEMON_HEADERS
    process_monitor.h
    launcher_detect.h
    ipc.h
    config.h
    common.h
)

add_executable(capframex-daemon ${DAEMON_SOURCES} ${DAEMON_HEADERS})

target_link_libraries(capframex-daemon PRIVATE
    Threads::Threads
    rt  # For shared memory
)

target_compile_options(capframex-daemon PRIVATE
    -Wall -Wextra -Wpedantic
)

install(TARGETS capframex-daemon
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    COMPONENT daemon)
