set(CALCULATOR_CORE_SOURCES
    CalculatorHistory.cpp
    CalculatorManager.cpp
    ExpressionCommand.cpp
    NumberFormattingUtils.cpp
    UnitConverter.cpp
    CEngine/calc.cpp
    CEngine/CalcInput.cpp
    CEngine/CalcUtils.cpp
    CEngine/History.cpp
    CEngine/Number.cpp
    CEngine/Rational.cpp
    CEngine/RationalMath.cpp
    CEngine/scicomm.cpp
    CEngine/scidisp.cpp
    CEngine/scifunc.cpp
    CEngine/scioper.cpp
    CEngine/sciset.cpp
    Ratpack/basex.cpp
    Ratpack/conv.cpp
    Ratpack/exp.cpp
    Ratpack/fact.cpp
    Ratpack/itrans.cpp
    Ratpack/itransh.cpp
    Ratpack/logic.cpp
    Ratpack/num.cpp
    Ratpack/rat.cpp
    Ratpack/support.cpp
    Ratpack/trans.cpp
    Ratpack/transh.cpp
)

add_library(CalculatorCore STATIC ${CALCULATOR_CORE_SOURCES})
add_library(Calculator::Core ALIAS CalculatorCore)
set_target_properties(CalculatorCore PROPERTIES POSITION_INDEPENDENT_CODE ON)

target_compile_features(CalculatorCore PUBLIC cxx_std_20)
target_include_directories(
    CalculatorCore
    PUBLIC
        ${PROJECT_SOURCE_DIR}/src
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${CMAKE_CURRENT_SOURCE_DIR}/Ratpack
)

if(MSVC)
    target_compile_options(CalculatorCore PRIVATE /utf-8 /permissive-)
else()
    target_compile_options(CalculatorCore PRIVATE -Wall -Wextra -Wpedantic)
endif()
