
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using SemanticKernelPlatform.Application.Interfaces;
using SemanticKernelPlatform.Application.Plugins;
using SemanticKernelPlatform.Infrastructure.Services;
using SemanticKernelPlatform.Plugins;
using SemanticKernelPlatform.Plugins.Http;
using SemanticKernelPlatform.Plugins.Platform;

namespace SemanticKernelPlatform.Infrastructure;

#pragma warning disable SKEXP0070

public static class DependencyInjection
{
    public static IServiceCollection AddAiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["AI:Provider"];

        if (!string.Equals(
                provider,
                "Ollama",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported AI provider: '{provider}'.");
        }

        var endpoint = configuration["AI:Ollama:Endpoint"]
            ?? throw new InvalidOperationException(
                "AI:Ollama:Endpoint is not configured.");

        var modelId = configuration["AI:Ollama:ModelId"]
            ?? throw new InvalidOperationException(
                "AI:Ollama:ModelId is not configured.");

        // Request-scoped API response storage.
        services.AddScoped<ApiResponseContext>();

        // Map the interface to the same scoped instance.
        services.AddScoped<IApiResponseContext>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ApiResponseContext>());

        // API response processor.
        services.AddSingleton<ApiResponseProcessor>();

        // Plugins.
        services.AddSingleton<PlatformInfoPlugin>();
        services.AddHttpClient<HttpApiPlugin>();

        // Plugin registry.
        services.AddScoped<IPluginRegistry, PluginRegistry>();

        // Create a separate Kernel for each request scope.
        services.AddScoped<Kernel>(serviceProvider =>
        {
            var kernelBuilder = Kernel.CreateBuilder();

            kernelBuilder.AddOllamaChatCompletion(
                modelId: modelId,
                endpoint: new Uri(endpoint));

            var kernel = kernelBuilder.Build();

            var pluginRegistry =
                serviceProvider.GetRequiredService<IPluginRegistry>();

            foreach (var plugin in pluginRegistry.GetPlugins())
            {
                kernel.Plugins.AddFromObject(
                    plugin.Instance,
                    plugin.Name);
            }

            return kernel;
        });

        // Chat service.
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}
