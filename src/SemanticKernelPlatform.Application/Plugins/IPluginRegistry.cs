
namespace SemanticKernelPlatform.Application.Plugins;

public interface IPluginRegistry
{
    IReadOnlyCollection<PluginDefinition> GetPlugins();
}
