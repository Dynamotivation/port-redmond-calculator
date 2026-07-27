set(graphing_contract_files
    "220d8ccf07c9c1dcb535f954cca915d021f2169fbdea093cc01caac1e47dfdec|Common.h"
    "9b42aea6b3aab4fd05e4566e41df4011350abf8bc42066c2b7455f145e515e72|GraphingEnums.h"
    "c0632c9f85f2099bf04cb797d7dae31f478cb84a255955287a913cd8d162f620|IBitmap.h"
    "1b3cda5048174a49c887fe5a769b7d1c158299664333468e7c29c8ba88c66c9b|IEquation.h"
    "605bc89524b9eaf66a029567d685b42c3b02ae76a8435e180bbf36c2030e2874|IEquationOptions.h"
    "b9935398513bc34f4e1a806bfcc7a26e9b9ef1c4306c8cb827ee386ed1fb13e7|IGraph.h"
    "6c67682ca31d0acaf95dbbfc183e489468598e3a2e90805188668e308ef7abe9|IGraphAnalyzer.h"
    "1349d385598e7ce19f1617d4e2100d70cb091d5ca4d5e7a0aa314c702a0c81e4|IGraphRenderer.h"
    "84c3652a2088d471857036715580689e563dd4be2c8adb4fedcceebff9575bcc|IGraphingOptions.h"
    "4d15504ba100a0a41e617090c845e817e54f0741b9ee9ea3b1e535baa269038b|IMathSolver.h")

foreach(contract_entry IN LISTS graphing_contract_files)
    string(REPLACE "|" ";" contract_parts "${contract_entry}")
    list(GET contract_parts 0 expected_hash)
    list(GET contract_parts 1 contract_name)
    set(contract_path
        "${MICROSOFT_CALCULATOR_ROOT}/src/GraphingInterfaces/${contract_name}")
    # Hash normalized text so Git's checkout line-ending policy cannot make the
    # same Microsoft contract pass on one platform and fail on another.
    file(READ "${contract_path}" contract_contents)
    string(REPLACE "\r\n" "\n" contract_contents "${contract_contents}")
    string(SHA256 actual_hash "${contract_contents}")
    if(NOT actual_hash STREQUAL expected_hash)
        message(FATAL_ERROR
            "Microsoft graphing contract ${contract_name} changed. Review every "
            "new or changed signature against the replacement backend, update "
            "the managed/native adapter, and only then refresh the contract hash.")
    endif()
endforeach()

message(STATUS "Microsoft graphing interface contract is unchanged")
