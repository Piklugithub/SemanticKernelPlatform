
using SemanticKernelPlatform.Application.Interfaces;
using SemanticKernelPlatform.Infrastructure;
using SemanticKernelPlatform.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Semantic Kernel and the selected AI provider.
builder.Services.AddAiInfrastructure(builder.Configuration);

// Register application services.
builder.Services.AddScoped<IChatService, ChatService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    Application = "Semantic Kernel Platform",
    Status = "Running"
}));

app.Run();
