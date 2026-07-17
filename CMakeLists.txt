cmake_minimum_required(VERSION 3.24)

project(WindowsCalculatorPortable LANGUAGES CXX)

option(CALCULATOR_BUILD_TESTS "Build portable Calculator core tests" ON)

add_subdirectory(src/CalcManager)
add_subdirectory(src/CalculatorNative)

if(CALCULATOR_BUILD_TESTS)
    enable_testing()
    add_subdirectory(src/PortableCoreTests)
endif()
