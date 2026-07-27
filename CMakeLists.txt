cmake_minimum_required(VERSION 3.24)

project(RedmondCalculator LANGUAGES CXX)

option(CALCULATOR_BUILD_TESTS "Build portable Calculator core tests" ON)
set(
    MICROSOFT_CALCULATOR_ROOT
    "${PROJECT_SOURCE_DIR}/upstream/windows-calculator"
    CACHE PATH
    "Path to the pristine Microsoft Calculator source submodule")

if(NOT EXISTS "${MICROSOFT_CALCULATOR_ROOT}/src/CalcManager/CalculatorManager.cpp")
    message(FATAL_ERROR
        "Microsoft Calculator submodule is unavailable. Run "
        "'git submodule update --init --recursive'.")
endif()

add_subdirectory(src/CalculatorCore)
add_subdirectory(src/CalculatorNative)

if(CALCULATOR_BUILD_TESTS)
    enable_testing()
    add_subdirectory(src/PortableCoreTests)

    find_package(Git REQUIRED)
    add_test(
        NAME UpstreamPristineSources
        COMMAND
            "${PROJECT_SOURCE_DIR}/scripts/verify-upstream-pristine.sh"
            "${MICROSOFT_CALCULATOR_ROOT}")
endif()
