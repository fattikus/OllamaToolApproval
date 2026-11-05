# OllamaToolApproval: human-in-the-loop tool approval with Ollama + Agent Framework

Companion sample for the blog post **"Human-in-the-Loop Tool Approval in .NET with Ollama"** (Local LLMs in .NET, Part 1 of 3).

"Gozo Pantry" is a small Maltese online shop. Its support assistant can look up orders, refund them and delete customer accounts. The risky tools are wrapped in `ApprovalRequiredAIFunction`, but instead of showing the user raw JSON, every approval request first goes through a **per-tool approval policy**:

| Policy result | What happens |
|---|---|
| `AutoReject(reason)` | Invalid call (already refunded, amount too big, customer has open orders). The user is never asked; the model gets the reason. |
| `AutoApprove` | Trivially safe call (refund of €5 or less). Runs without a prompt. |
| `AskUser(message)` | Everything else. The browser shows a one-line, human-readable question and the streamed turn **pauses** until someone clicks. |

Tools with approval required but no registered policy are **denied** (fail closed).

## Prerequisites

- .NET 10 SDK
- [Ollama](https://ollama.com) running locally with a tool-calling model:

```powershell
ollama pull qwen3:8b
```

Model and endpoint live in `appsettings.json` (`Ollama:Endpoint`, `Ollama:Model`). The approval timeout and the auto-approve refund threshold live under `Approvals`.

## Run

```powershell
dotnet run
```

Open http://localhost:5080 and try the demo prompts (also available as buttons):

1. **Ask:** `Refund order 1042 in full.`
   The policy turns `refund_order { orderId: "7f3a9c1e-…", amountCents: 4250 }` into *"Refund **€42.50** to **Maria Borg** for order #1042 (2× Gozo sea salt, 1× Extra virgin olive oil 500ml, full refund)?"*. Approve it and the same stream carries on.
2. **Auto-reject:** `Refund order 1045 in full, the customer says the kit never arrived.`
   Order #1045 was already refunded. Blocked by the policy without asking anyone.
3. **Auto-approve:** `Refund €3 on order 1043, one jar of tomatoes was cracked.`
   Under the €5 threshold, so it runs straight away.

Bonus: `Delete the customer who placed order 1043.` always asks (red card, never auto-approved), and `Delete the customer who placed order 1046.` is auto-rejected because Anna has an open order.

The data is in memory and resets on restart.

## How it fits together

```
Program.cs                    DI wiring, POST /chat (SSE) and POST /approvals/{id}
Chat/ChatTurnRunner.cs        Runs the agent, intercepts ToolApprovalRequestContent, pauses, resumes
Approvals/IToolApprovalPolicy  + RefundOrderPolicy, DeleteCustomerPolicy
Approvals/ToolApprovalGate.cs  Picks the policy per tool; denies tools without one
Approvals/ApprovalBroker.cs    ConcurrentDictionary<Guid, PendingApproval> + TaskCompletionSource<bool>
Shop/                          In-memory store and the three tools
wwwroot/index.html             Vanilla JS: fetch + ReadableStream, approval card
```

`POST /chat` streams these server-sent events: `text`, `approval_required`, `tool_status`, `done`.
`POST /approvals/{id}` with `{ "approved": true|false }` resolves a pending approval. Approvals are bound to the browser that created them (a cookie stands in for real auth), expire after `Approvals:TimeoutSeconds` (timeout = deny), and are cancelled if the browser disconnects.

## Out of scope (see the rest of the series)

- Model preloading, HttpClient timeouts and cancellation tuning: Part 2.
- Persisting approvals across restarts or multiple instances, queues: Part 3.
