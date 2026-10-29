
using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SemanticKernelPlatform.Plugins.Platform;

public sealed class PlatformInfoPlugin
{
    [KernelFunction("GetPlatformInfo")]
    [Description("Returns information about the AI platform and its available integration capabilities.")]
    public string GetPlatformInfo()
    {
        return """
            This is a reusable AI platform.
            It can be extended with approved plugins for inventory,
            ticket management, monitoring, and other business APIs.
            """;
    }
}
