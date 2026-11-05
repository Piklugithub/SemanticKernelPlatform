
using Microsoft.SemanticKernel;

namespace SemanticKernelPlatform.Plugins.Analysis;

public static class TicketAnalysisPlugin
{
    public static KernelPlugin Create(string promptTemplate)
    {
        var analyzeTicketsFunction =
            KernelFunctionFactory.CreateFromPrompt(
                promptTemplate,
                functionName: "AnalyzeTickets",
                description:
                    "Analyzes ticket information and provides business recommendations.");

        return KernelPluginFactory.CreateFromFunctions(
            pluginName: "TicketAnalysis",
            description: "Provides AI-powered ticket analysis.",
            functions: new[] { analyzeTicketsFunction });
    }
}
