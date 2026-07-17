add_executable(CalculatorCoreSmokeTests CalculatorCoreSmokeTests.cpp)
target_link_libraries(CalculatorCoreSmokeTests PRIVATE Calculator::Core)

add_test(NAME CalculatorCoreSmokeTests COMMAND CalculatorCoreSmokeTests)

add_executable(CalculatorNativeSmokeTests CalculatorNativeSmokeTests.cpp)
target_link_libraries(CalculatorNativeSmokeTests PRIVATE Calculator::Native Calculator::Core)
target_compile_definitions(
    CalculatorNativeSmokeTests
    PRIVATE CALCULATOR_SOURCE_DIR="${PROJECT_SOURCE_DIR}"
)

add_test(NAME CalculatorNativeSmokeTests COMMAND CalculatorNativeSmokeTests)
