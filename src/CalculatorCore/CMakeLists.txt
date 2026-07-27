include("${PROJECT_SOURCE_DIR}/src/PortableCompat/GenerateUnitConverterDataLoader.cmake")

set(MSCALC_MANAGER_DIR "${MICROSOFT_CALCULATOR_ROOT}/src/CalcManager")

set(CALCULATOR_CORE_SOURCES
    ${MSCALC_MANAGER_DIR}/CalculatorHistory.cpp
    ${MSCALC_MANAGER_DIR}/CalculatorManager.cpp
    ${MSCALC_MANAGER_DIR}/ExpressionCommand.cpp
    ${MSCALC_MANAGER_DIR}/NumberFormattingUtils.cpp
    ${MSCALC_MANAGER_DIR}/UnitConverter.cpp
    ${CALCULATOR_GENERATED_DATALOADER}
    ${MSCALC_MANAGER_DIR}/CEngine/calc.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/CalcInput.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/CalcUtils.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/History.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/Number.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/Rational.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/RationalMath.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/scicomm.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/scidisp.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/scifunc.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/scioper.cpp
    ${MSCALC_MANAGER_DIR}/CEngine/sciset.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/basex.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/conv.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/exp.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/fact.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/itrans.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/itransh.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/logic.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/num.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/rat.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/support.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/trans.cpp
    ${MSCALC_MANAGER_DIR}/Ratpack/transh.cpp
)

add_library(CalculatorCore STATIC ${CALCULATOR_CORE_SOURCES})
add_library(Calculator::Core ALIAS CalculatorCore)
set_target_properties(CalculatorCore PROPERTIES POSITION_INDEPENDENT_CODE ON)

target_compile_features(CalculatorCore PUBLIC cxx_std_20)
target_include_directories(
    CalculatorCore
    BEFORE
    PUBLIC
        ${CALCULATOR_GENERATED_ROOT}/src
        ${PROJECT_SOURCE_DIR}/src/PortableCompat/include
)
if(NOT WIN32)
    target_include_directories(
        CalculatorCore
        BEFORE
        PUBLIC
            ${PROJECT_SOURCE_DIR}/src/PortableCompat/include/nonwindows
    )
endif()
target_include_directories(
    CalculatorCore
    PUBLIC
        ${MICROSOFT_CALCULATOR_ROOT}/src
        ${MSCALC_MANAGER_DIR}
        ${MSCALC_MANAGER_DIR}/Ratpack
)

if(MSVC)
    target_compile_options(CalculatorCore PRIVATE /utf-8 /permissive-)
else()
    target_compile_options(CalculatorCore PRIVATE -Wall -Wextra -Wpedantic)
endif()
