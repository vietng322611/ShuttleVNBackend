namespace ShuttleVNBackend.Application.UseCases.Assistant;

public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";

    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxToolRounds { get; set; } = 3;
    public int MaxToolCallsPerRound { get; set; } = 4;
    public int MaxMessages { get; set; } = 20;
    public int MaxMessageLength { get; set; } = 2000;
    public int MaxReplyChars { get; set; } = 4000;
    public int MaxTokens { get; set; } = 800;
    public double Temperature { get; set; } = 0.3;
    public int AvailabilityHorizonDays { get; set; } = 30;

    /// <summary>
    ///     Raw JSON merged into every request payload, e.g.
    ///     <c>{"reasoning_effort":"none"}</c> or <c>{"enable_thinking":false}</c>.
    ///     Used to disable provider-specific reasoning because tool calling is
    ///     unsupported while reasoning is active on some models.
    /// </summary>
    public string ExtraBody { get; set; } = "{}";
}