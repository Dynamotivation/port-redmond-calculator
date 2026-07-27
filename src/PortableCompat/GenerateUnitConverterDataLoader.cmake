find_package(Git REQUIRED)

set(CALCULATOR_GENERATED_ROOT "${CMAKE_BINARY_DIR}/generated/portable-upstream")
set(CALCULATOR_GENERATED_DATALOADER_DIR
    "${CALCULATOR_GENERATED_ROOT}/src/CalcViewModel/DataLoaders")
set(CALCULATOR_GENERATED_COMMON_DIR
    "${CALCULATOR_GENERATED_ROOT}/src/CalcViewModel/Common")

file(REMOVE_RECURSE "${CALCULATOR_GENERATED_ROOT}")
file(MAKE_DIRECTORY
    "${CALCULATOR_GENERATED_DATALOADER_DIR}"
    "${CALCULATOR_GENERATED_COMMON_DIR}")

foreach(source_name
    UnitConverterDataConstants.h
    UnitConverterDataLoader.cpp
    UnitConverterDataLoader.h)
    configure_file(
        "${PROJECT_SOURCE_DIR}/src/CalcViewModel/DataLoaders/${source_name}"
        "${CALCULATOR_GENERATED_DATALOADER_DIR}/${source_name}"
        COPYONLY)
endforeach()

configure_file(
    "${PROJECT_SOURCE_DIR}/src/CalcViewModel/Common/PortableNavCategory.h"
    "${CALCULATOR_GENERATED_COMMON_DIR}/PortableNavCategory.h"
    COPYONLY)

execute_process(
    COMMAND
        "${GIT_EXECUTABLE}" apply
        --unsafe-paths
        "--directory=${CALCULATOR_GENERATED_ROOT}"
        --whitespace=nowarn
        "${PROJECT_SOURCE_DIR}/src/PortableCompat/patches/UnitConverterDataLoader.portable.patch"
    WORKING_DIRECTORY "${PROJECT_SOURCE_DIR}"
    RESULT_VARIABLE patch_result
    OUTPUT_VARIABLE patch_output
    ERROR_VARIABLE patch_error)

if(NOT patch_result EQUAL 0)
    message(FATAL_ERROR
        "The portable UnitConverterDataLoader overlay no longer applies to the "
        "pristine Microsoft source. Upstream changed the protected conversion "
        "code; review and update the portability adapter instead of editing the "
        "Microsoft files.\n${patch_output}${patch_error}")
endif()

set(CALCULATOR_GENERATED_ROOT "${CALCULATOR_GENERATED_ROOT}")
set(CALCULATOR_GENERATED_DATALOADER
    "${CALCULATOR_GENERATED_DATALOADER_DIR}/UnitConverterDataLoader.cpp")
