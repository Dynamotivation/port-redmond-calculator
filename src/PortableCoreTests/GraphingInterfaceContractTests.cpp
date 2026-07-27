#include "GraphingPlatformCompat.h"
#include "GraphingInterfaces/IBitmap.h"
#include "GraphingInterfaces/IEquation.h"
#include "GraphingInterfaces/IEquationOptions.h"
#include "GraphingInterfaces/IGraph.h"
#include "GraphingInterfaces/IGraphAnalyzer.h"
#include "GraphingInterfaces/IGraphRenderer.h"
#include "GraphingInterfaces/IGraphingOptions.h"
#include "GraphingInterfaces/IMathSolver.h"

#include <memory>
#include <string>
#include <type_traits>

namespace
{
    using ParseInputSignature =
        std::unique_ptr<Graphing::IExpression> (Graphing::IMathSolver::*)(
            const std::wstring&, int&, int&);
    using ErrorSignature =
        void (Graphing::IMathSolver::*)(HRESULT, int&, int&);
    using SerializeSignature =
        std::wstring (Graphing::IMathSolver::*)(const Graphing::IExpression*);
    using AnalyzeSignature =
        Graphing::IGraphFunctionAnalysisData (Graphing::IMathSolver::*)(
            const Graphing::Analyzer::IGraphAnalyzer*);

    static_assert(std::is_same_v<
        decltype(&Graphing::IMathSolver::ParseInput),
        ParseInputSignature>);
    static_assert(std::is_same_v<
        decltype(&Graphing::IMathSolver::HRErrorToErrorInfo),
        ErrorSignature>);
    static_assert(std::is_same_v<
        decltype(&Graphing::IMathSolver::Serialize),
        SerializeSignature>);
    static_assert(std::is_same_v<
        decltype(&Graphing::IMathSolver::Analyze),
        AnalyzeSignature>);

    static_assert(std::is_same_v<
        decltype(&Graphing::IGraph::GetInitializationError),
        HRESULT (Graphing::IGraph::*)() const>);
    static_assert(std::is_same_v<
        decltype(&Graphing::Renderer::IGraphRenderer::GetDisplayRanges),
        HRESULT (Graphing::Renderer::IGraphRenderer::*)(
            double&, double&, double&, double&)>);
    static_assert(std::is_same_v<
        decltype(&Graphing::Analyzer::IGraphAnalyzer::PerformFunctionAnalysis),
        HRESULT (Graphing::Analyzer::IGraphAnalyzer::*)(
            Graphing::Analyzer::NativeAnalysisType)>);
}

int main()
{
    return 0;
}
