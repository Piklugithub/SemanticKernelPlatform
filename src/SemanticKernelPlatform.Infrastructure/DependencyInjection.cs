
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using SemanticKernelPlatform.Application.Interfaces;
using SemanticKernelPlatform.Application.Plugins;
using SemanticKernelPlatform.Infrastructure.Services;
using SemanticKernelPlatform.Plugins;
using SemanticKernelPlatform.Plugins.Analysis;
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

        // Map the interface to the same scoped instance.
        services.AddScoped<IApiResponseContext, ApiResponseContext>();

        // API response processor.
        services.AddSingleton<ApiResponseProcessor>();
        services.AddSingleton<UserSearchProcessor>();
        services.AddSingleton<TicketCountProcessor>();
        services.AddSingleton<TicketAnalysisProcessor>();


        // Plugins.
        services.AddScoped<PlatformInfoPlugin>();
        services.AddHttpClient<HttpApiPlugin>();

        // Plugin registry.
        services.AddScoped<IPluginRegistry, PluginRegistry>();

        services.AddHttpClient<OllamaToolCallingClient>(client =>
        {
            client.BaseAddress = new Uri(
                endpoint.TrimEnd('/') + "/");

            client.Timeout = TimeSpan.FromSeconds(60);
        });

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

            // Load the external prompt.
            var promptPath = Path.Combine(
                AppContext.BaseDirectory,
                "Prompts",
                "TicketAnalysis.txt");

            if (!File.Exists(promptPath))
            {
                throw new FileNotFoundException(
                    "Ticket analysis prompt file was not found.",
                    promptPath);
            }

            var promptTemplate = File.ReadAllText(promptPath);

            // Register the prompt-based plugin.
            kernel.Plugins.Add(
                TicketAnalysisPlugin.Create(promptTemplate));

            return kernel;
        });
        services.AddScoped<IIntentExtractionService, IntentExtractionService>();
        services.AddSingleton< IChatHistoryStore,InMemoryChatHistoryStore>();
        services.AddSingleton<IConversationDataStore,InMemoryConversationDataStore>();

        // Chat service.
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}
