using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace OllamaToolApproval.Approvals;

public enum ApprovalOutcome { Approved, Denied, TimedOut }

public enum ResolveResult { Resolved, NotFound, AlreadyResolved }

public sealed class PendingApproval(Guid id, string ownerId)
{
    public Guid Id { get; } = id;
    public string OwnerId { get; } = ownerId;

    // Continuations run on the pool, not inline inside the /approvals request that calls TrySetResult.
    public TaskCompletionSource<bool> Decision { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>Parks a streamed agent turn until the owning user clicks Approve or Deny.</summary>
public sealed class ApprovalBroker(IOptions<ApprovalOptions> options)
{
    private readonly ConcurrentDictionary<Guid, PendingApproval> _pending = new();

    public TimeSpan Timeout => TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

    public PendingApproval Open(string ownerId)
    {
        var pending = new PendingApproval(Guid.NewGuid(), ownerId);
        _pending[pending.Id] = pending;
        return pending;
    }

    public async Task<ApprovalOutcome> WaitAsync(PendingApproval pending, CancellationToken requestAborted)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        timeout.CancelAfter(Timeout);
        try
        {
            return await pending.Decision.Task.WaitAsync(timeout.Token) ? ApprovalOutcome.Approved : ApprovalOutcome.Denied;
        }
        catch (OperationCanceledException) when (!requestAborted.IsCancellationRequested)
        {
            return ApprovalOutcome.TimedOut;
        }
        finally
        {
            _pending.TryRemove(pending.Id, out _);
        }
    }

    public ResolveResult TryResolve(Guid id, string ownerId, bool approved)
    {
        // Someone else's approval looks exactly like a missing one, so IDs can't be probed.
        if (!_pending.TryGetValue(id, out var pending) || !string.Equals(pending.OwnerId, ownerId, StringComparison.Ordinal))
            return ResolveResult.NotFound;

        return pending.Decision.TrySetResult(approved) ? ResolveResult.Resolved : ResolveResult.AlreadyResolved;
    }
}
