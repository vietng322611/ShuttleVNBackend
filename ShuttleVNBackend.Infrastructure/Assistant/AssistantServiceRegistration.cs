using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShuttleVNBackend.Application.Interfaces.Assistant;
using ShuttleVNBackend.Application.UseCases.Assistant;
using ShuttleVNBackend.Application.UseCases.Assistant.Services;
using ShuttleVNBackend.Application.UseCases.Assistant.Tools;

namespace ShuttleVNBackend.Infrastructure.Assistant;

public static class AssistantServiceRegistration
{
    public static IServiceCollection AddAssistant(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AssistantOptions>()
            .Bind(configuration.GetSection(AssistantOptions.SectionName))
            .Validate(options => options.TimeoutSeconds is >= 1 and <= 300,
                "Assistant:TimeoutSeconds must be between 1 and 300.")
            .Validate(options => options.MaxToolRounds is >= 0 and <= 10,
                "Assistant:MaxToolRounds must be between 0 and 10.")
            .Validate(options => options.MaxToolCallsPerRound is >= 1 and <= 16,
                "Assistant:MaxToolCallsPerRound must be between 1 and 16.")
            .Validate(options => options.MaxMessages is >= 1 and <= 100,
                "Assistant:MaxMessages must be between 1 and 100.")
            .Validate(options => options.MaxMessageLength is >= 1 and <= 10000,
                "Assistant:MaxMessageLength must be between 1 and 10000.")
            .Validate(options => options.MaxReplyChars is >= 100 and <= 20000,
                "Assistant:MaxReplyChars must be between 100 and 20000.")
            .Validate(options => options.MaxTokens is >= 1 and <= 8000,
                "Assistant:MaxTokens must be between 1 and 8000.")
            .Validate(options => options.AvailabilityHorizonDays is >= 1 and <= 365,
                "Assistant:AvailabilityHorizonDays must be between 1 and 365.")
            .Validate(IsJsonObject, "Assistant:ExtraBody must be a JSON object such as {\"reasoning_effort\":\"none\"}.")
            .ValidateOnStart();

        services.AddHttpClient<IChatCompletionClient, OpenAiCompatibleChatClient>();
        services.AddScoped<AssistantChatService>();
        services.AddScoped<IAssistantTool, ListCourtsTool>();
        services.AddScoped<IAssistantTool, CourtAvailabilityTool>();

        return services;
    }

    private static bool IsJsonObject(AssistantOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ExtraBody))
        {
            return true;
        }

        try
        {
            return System.Text.Json.Nodes.JsonNode.Parse(options.ExtraBody)
                is System.Text.Json.Nodes.JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
