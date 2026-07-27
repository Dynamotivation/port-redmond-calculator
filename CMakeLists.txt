cmake_minimum_required(VERSION 3.24)

project(WindowsCalculatorPortable LANGUAGES CXX)

option(CALCULATOR_BUILD_TESTS "Build portable Calculator core tests" ON)
set(CALCULATOR_UPSTREAM_REF "upstream/main" CACHE STRING
    "Git ref used to verify that protected Microsoft source files are pristine")

add_subdirectory(src/CalcManager)
add_subdirectory(src/CalculatorNative)

if(CALCULATOR_BUILD_TESTS)
    enable_testing()
    add_subdirectory(src/PortableCoreTests)

    find_package(Git REQUIRED)
    if(NOT EXISTS "${PROJECT_SOURCE_DIR}/.git")
        message(FATAL_ERROR
            "A Git checkout is required when CALCULATOR_BUILD_TESTS is enabled "
            "because protected Microsoft sources must be verified")
    endif()
    execute_process(
        COMMAND "${GIT_EXECUTABLE}" rev-parse --verify --quiet
            "${CALCULATOR_UPSTREAM_REF}^{commit}"
        WORKING_DIRECTORY "${PROJECT_SOURCE_DIR}"
        RESULT_VARIABLE upstream_ref_result
        OUTPUT_QUIET
        ERROR_QUIET)
    if(NOT upstream_ref_result EQUAL 0)
        message(FATAL_ERROR
            "Cannot verify protected Microsoft sources because "
            "${CALCULATOR_UPSTREAM_REF} is unavailable. Fetch that upstream "
            "ref or configure CALCULATOR_UPSTREAM_REF to the reviewed commit.")
    endif()

    add_test(
        NAME UpstreamPristineSources
        COMMAND
            "${PROJECT_SOURCE_DIR}/scripts/verify-upstream-pristine.sh"
            "${CALCULATOR_UPSTREAM_REF}")
endif()
