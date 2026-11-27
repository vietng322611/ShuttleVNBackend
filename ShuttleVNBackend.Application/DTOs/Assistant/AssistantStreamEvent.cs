namespace ShuttleVNBackend.Application.DTOs.Assistant;

public abstract record AssistantStreamEvent
{
    public sealed record ToolStarted(string Name) : AssistantStreamEvent;

    public sealed record TextDelta(string Text) : AssistantStreamEvent;

    public sealed record Done(string Reply) : AssistantStreamEvent;
}