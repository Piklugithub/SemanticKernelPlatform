
using Microsoft.SemanticKernel;
using SemanticKernelPlatform.Application.Plugins;
using SemanticKernelPlatform.Plugins.Http;
using SemanticKernelPlatform.Plugins.Platform;
using SemanticKernelPlatform.Plugins.Analysis;

namespace SemanticKernelPlatform.Plugins;

public sealed class PluginRegistry : IPluginRegistry
{
    private readonly IReadOnlyCollection<PluginDefinition> _plugins;

    public PluginRegistry(
        PlatformInfoPlugin platformInfoPlugin,
        HttpApiPlugin httpApiPlugin)
    {
        _plugins = new List<PluginDefinition>
        {
            new("Platform", platformInfoPlugin),
            new("HttpApi", httpApiPlugin)
        };
    }

    public IReadOnlyCollection<PluginDefinition> GetPlugins()
    {
        return _plugins;
    }

    public static KernelPlugin CreateTicketAnalysisPlugin(
        string promptTemplate)
    {
        return TicketAnalysisPlugin.Create(promptTemplate);
    }
}
