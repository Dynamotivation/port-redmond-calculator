add_library(
    CalculatorNative
    SHARED
    CalculatorNative.cpp
    Utf8.cpp
)
add_library(Calculator::Native ALIAS CalculatorNative)

target_compile_features(CalculatorNative PUBLIC cxx_std_20)
target_link_libraries(CalculatorNative PRIVATE Calculator::Core)
target_include_directories(
    CalculatorNative
    PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/include
    PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}
)

target_compile_definitions(CalculatorNative PRIVATE CALCULATOR_NATIVE_BUILD)
set_target_properties(
    CalculatorNative
    PROPERTIES
        CXX_VISIBILITY_PRESET hidden
        VISIBILITY_INLINES_HIDDEN YES
        OUTPUT_NAME calculator_engine
)

if(MSVC)
    target_compile_options(CalculatorNative PRIVATE /utf-8 /permissive-)
else()
    target_compile_options(CalculatorNative PRIVATE -Wall -Wextra -Wpedantic)
endif()
