add_executable(CalculatorCoreSmokeTests CalculatorCoreSmokeTests.cpp)
target_link_libraries(CalculatorCoreSmokeTests PRIVATE Calculator::Core)

add_test(NAME CalculatorCoreSmokeTests COMMAND CalculatorCoreSmokeTests)

add_executable(UnitConverterPortableTests UnitConverterPortableTests.cpp)
target_link_libraries(UnitConverterPortableTests PRIVATE Calculator::Core)

add_test(NAME UnitConverterPortableTests COMMAND UnitConverterPortableTests)

add_executable(
    UnitConverterDataLoaderPortableTests
    UnitConverterDataLoaderPortableTests.cpp
    ../CalculatorNative/Utf8.cpp
)
target_link_libraries(UnitConverterDataLoaderPortableTests PRIVATE Calculator::Core)
target_compile_definitions(
    UnitConverterDataLoaderPortableTests
    PRIVATE CALCULATOR_SOURCE_DIR="${PROJECT_SOURCE_DIR}"
)

add_test(NAME UnitConverterDataLoaderPortableTests COMMAND UnitConverterDataLoaderPortableTests)

add_executable(CalculatorNativeSmokeTests CalculatorNativeSmokeTests.cpp)
target_link_libraries(CalculatorNativeSmokeTests PRIVATE Calculator::Native Calculator::Core)
target_compile_definitions(
    CalculatorNativeSmokeTests
    PRIVATE CALCULATOR_SOURCE_DIR="${PROJECT_SOURCE_DIR}"
)

add_test(NAME CalculatorNativeSmokeTests COMMAND CalculatorNativeSmokeTests)

add_executable(
    GraphingInterfaceContractTests
    GraphingInterfaceContractTests.cpp
)
target_compile_features(GraphingInterfaceContractTests PRIVATE cxx_std_20)
target_include_directories(
    GraphingInterfaceContractTests
    PRIVATE
        ${PROJECT_SOURCE_DIR}/src
        ${PROJECT_SOURCE_DIR}/src/PortableCompat/include
)

add_test(NAME GraphingInterfaceContractTests COMMAND GraphingInterfaceContractTests)
add_test(
    NAME GraphingInterfaceHash
    COMMAND
        ${CMAKE_COMMAND}
        -DCALCULATOR_SOURCE_DIR=${PROJECT_SOURCE_DIR}
        -P ${PROJECT_SOURCE_DIR}/src/PortableCompat/VerifyGraphingContract.cmake
)
