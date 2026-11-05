using System.Collections.Concurrent;
using Microsoft.Agents.AI;

namespace OllamaToolApproval.Chat;

public sealed class Conversation(AgentSession session)
{
    public AgentSession Session { get; } = session;

    // One turn at a time per conversation; the session's history isn't safe for concurrent runs.
    public SemaphoreSlim TurnLock { get; } = new(1, 1);
}

/// <summary>In-memory, single-instance conversation store keyed by owner. See Part 3 for persisting across restarts.</summary>
public sealed class ConversationStore(AIAgent agent)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<Conversation>>> _conversations = new();

    public Task<Conversation> GetAsync(string ownerId) =>
        _conversations.GetOrAdd(ownerId, _ => new(CreateAsync)).Value;

    private async Task<Conversation> CreateAsync() => new(await agent.CreateSessionAsync());
}
