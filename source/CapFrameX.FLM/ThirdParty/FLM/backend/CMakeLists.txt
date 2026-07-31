#=============================================================================
# Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
#  @author AMD Developer Tools Team
#  @file CMakeLists.txt
#  @brief  FLM Backend Lib CMakeLists file.
#=============================================================================

add_library(flm_backend STATIC)

include(${PROJECT_SOURCE_DIR}/cmake/common.cmake)

set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /wd4996") # AMF warnings

set(FLM_SOURCE_SDK
    flm.h
    flm_utils.h
    flm_utils.cpp
    flm_user_interface.h
    flm_user_interface.cpp
    flm_timer.h
    flm_timer.cpp
    flm_keyboard.h
    flm_keyboard.cpp
    flm_mouse.h
    flm_mouse.cpp
    flm_capture_context.h
    flm_capture_context.cpp
    flm_capture_amf.h
    flm_capture_amf.cpp
    flm_capture_dxgi.h
    flm_capture_dxgi.cpp
    flm_pipeline.h
    flm_pipeline.cpp
)

set(FLM_SOURCE_INI
    ${PROJECT_SOURCE_DIR}/external/ini/simpleIni.h
)

set(FLM_SOURCE_AMF_COMMON
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/AMFFactory.h
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/AMFFactory.cpp
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/AMFSTL.h
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/AMFSTL.cpp
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/CurrentTimeImpl.h
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/CurrentTimeImpl.cpp
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/DataStreamFactory.cpp
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/PropertyStorageExImpl.h
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/PropertyStorageExImpl.cpp
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/Thread.h
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/Thread.cpp
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/TraceAdapter.h
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/TraceAdapter.cpp
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/DataStreamFile.h
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/DataStreamFile.cpp
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/DataStreamMemory.h
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/DataStreamMemory.cpp
    ${PROJECT_SOURCE_DIR}/external/amf/public/common/Windows/ThreadWindows.cpp
)

set(FLM_SOURCE_AMF_COMPONENTS
    ${PROJECT_SOURCE_DIR}/external/amf/public/src/components/DisplayCapture/DDAPISource.h
    ${PROJECT_SOURCE_DIR}/external/amf/public/src/components/DisplayCapture/DDAPISource.cpp
    ${PROJECT_SOURCE_DIR}/external/amf/public/src/components/DisplayCapture/DisplayCaptureImpl.h
    ${PROJECT_SOURCE_DIR}/external/amf/public/src/components/DisplayCapture/DisplayCaptureImpl.cpp
)


set(FLM_CONFIG_INI
    ${PROJECT_SOURCE_DIR}/source/flm_backend/flm.ini
)

source_group("external/amf/common"      FILES ${FLM_SOURCE_AMF_COMMON})
source_group("external/amf/components"  FILES ${FLM_SOURCE_AMF_COMPONENTS})
source_group("external/ini"             FILES ${FLM_SOURCE_INI})
source_group("shaders"                  FILES ${FLM_SHADERS})
source_group("ini_data"                 FILES ${FLM_CONFIG_INI})
source_group("source"                   FILES ${FLM_SOURCE_SDK})

target_sources( flm_backend PRIVATE  
    ${FLM_SOURCE_SDK}
    ${FLM_SOURCE_AMF_COMMON}
    ${FLM_SOURCE_AMF_COMPONENTS}
    ${FLM_SOURCE_INI}
    ${FLM_CONFIG_INI}
)

target_include_directories(flm_backend PUBLIC

    # SDK
    ${PROJECT_SOURCE_DIR}/source/flm_backend

    # External Clones
    ${PROJECT_SOURCE_DIR}/external
    ${PROJECT_SOURCE_DIR}/external/ini
    ${PROJECT_SOURCE_DIR}/external/amf
)

# copy json
copyCommand("${FLM_CONFIG_INI}"      "${CMAKE_RUNTIME_OUTPUT_DIRECTORY}/$<$<CONFIG:Debug>:debug>$<$<CONFIG:Release>:release>/")

set_target_properties(flm_backend PROPERTIES 
    FOLDER "libs"
    OUTPUT_NAME "flm_backend$<$<CONFIG:Debug>:d>"
)

