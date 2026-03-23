# Copilot CLI patterns relevant to AGUIDojo

## Executive summary

This pass looked at public GitHub Copilot CLI, GitHub Docs, and VS Code Copilot documentation to extract product patterns that overlap with AGUIDojo's session, model, context, and instruction design. The goal is not to copy Copilot CLI wholesale. The goal is to adopt the parts that fit AGUIDojo's sample scope and to reject the parts that would create avoidable drift or ambiguity.

The main conclusions are:

- Treat the chat session as a first-class durable object with resume semantics, but keep the business `chatSessionId` separate from transport/runtime IDs such as AG-UI `threadId`, `runId`, `ConversationId`, and workflow IDs.
- Let the client keep blank local drafts before first send, but create the canonical server session on the first persisted `/chat` turn rather than on tab creation.
- Persist both `preferredModelId` on the session and `effectiveModelId` on assistant turns or audit events so server routing stays explicit.
- Keep canonical full-branch history in server storage and compact only the model-invocation context, not the business record itself.
- Use model-aware compaction earlier than Copilot CLI's public "~95% of token limit" behavior; AGUIDojo needs more headroom because tool calls, approvals, and rich artifacts are part of the same session story.
- If AGUIDojo later introduces instructions, choose deterministic merge rules and show active instruction sources. Copilot's public docs explicitly warn that conflicting instructions can be nondeterministic today.

## Current Copilot patterns worth noting

### Session lifecycle and identity

- Copilot CLI treats sessions as durable user objects: it supports `/resume`, `/session`, `/rewind`, `/share`, `/context`, `/usage`, and `/compact`.
- The key architectural lesson is not the local storage path or CLI-only UX. The key lesson is that session identity, recovery, and history management are product features rather than hidden runtime details.
- For AGUIDojo, that argues for an opaque `chatSessionId` that survives model switches, compaction, reloads, and future cross-device resume.
- `ConversationId`, `threadId`, `runId`, Durable Task IDs, and workflow instance IDs should remain correlation/runtime fields only.

### Model selection

- Copilot CLI exposes manual model selection (`/model`), and GitHub Docs now describe `Auto` model selection as a first-class option in Copilot Chat and Copilot coding agent.
- Public docs describe Auto as availability/health/policy driven today, with task-aware routing as future direction rather than the current contract.
- The docs also distinguish the model selected by the user from the model that actually served the response.
- That separation maps cleanly to AGUIDojo:
  - persist `preferredModelId` on the session
  - record `effectiveModelId` on assistant turns or audit events
  - keep provider/deployment routing server-side
- A future AGUIDojo `auto` option can be a sample-local policy choice, but it is not a prerequisite for the first picker milestone.

### Context windows and auto compaction

- Copilot CLI and VS Code both expose context usage and support manual compaction.
- Public docs say compaction happens automatically as the context window fills, and the CLI docs describe background auto-compaction when usage approaches about 95% of the token limit.
- That confirms the product pattern AGUIDojo wants: long-lived or "infinite" sessions come from context compaction plus durable session identity, not from pretending the model has unbounded memory.
- AGUIDojo should copy the pattern, not the exact threshold. Because AGUIDojo mixes tool calls, approvals, structured artifact state, and server-owned business links, it needs more reserve than a generic assistant shell.
- Recommended AGUIDojo rule: compute a safe input budget per model, subtract output and system/tool reserves, and compact earlier, roughly in the 75-85% range depending on model tier.
- Compaction should be inspectable. Keep checkpoints or audit events for model switches and compaction summaries, but keep the canonical branching conversation intact.

### Custom instructions, glob rules, and prompt-safety lessons

- Copilot CLI supports `.github/copilot-instructions.md`, `.github/instructions/**/*.instructions.md`, `AGENTS.md`, and local user-level instructions.
- Official CLI docs say repository-wide and path-specific instructions are combined when both match, and they warn that conflicting instructions are nondeterministic.
- VS Code docs also say that when multiple instruction files are present they are combined, and no specific order is guaranteed.
- Official GitHub docs document glob-based `applyTo` rules, comma-separated pattern support, and `excludeAgent` for narrowing which agent consumes a file.
- There is also a public ambiguity worth noting:
  - GitHub's repository custom instructions docs describe the nearest `AGENTS.md` in the directory tree as taking precedence for GitHub.com agents.
  - Copilot CLI custom-instruction docs describe repository-root `AGENTS.md` as primary plus additional `AGENTS.md` files from the current working directory or configured directories.
- That difference is exactly why AGUIDojo should not rely on emergent precedence if it ever adds instructions. It should define one deterministic rule.

### Tool and trust boundaries

- Copilot CLI asks users to trust directories, approve tool execution, and confirm external URL access before pulling web content into agent context.
- The overlapping lesson for AGUIDojo is that uploaded files, fetched URLs, and user-authored instruction content should be treated as untrusted context rather than as policy.
- Approval and audit should sit around those boundaries, especially if AGUIDojo later adds instruction files, knowledge imports, or external fetch tools.

## Recommended AGUIDojo decisions from this overlap analysis

1. **Session creation**
   - Keep blank chat drafts local until first send.
   - Create the canonical server session on the first persisted `/chat` turn.
   - Do not require a separate explicit "create empty session" round trip for the default chat UX.

2. **Thin session API**
   - Start with `list`, `get`, and `archive` for server-owned sessions.
   - Treat first-turn implicit creation on `/chat` as the default creation path.
   - Add an explicit create endpoint only if a later workflow truly needs server-owned empty drafts.

3. **Model metadata**
   - Persist `preferredModelId` on the session.
   - Record `effectiveModelId` on assistant turns, audit events, or both.
   - Keep deployment/provider selection server-side and sample-scoped.

4. **Context management**
   - Keep the canonical branch history in server storage.
   - Let compaction build model-invocation context and summaries without rewriting the business record.
   - Store material compaction checkpoints or audit facts so "infinite session" behavior is explainable.

5. **Compaction thresholds**
   - Do not copy a last-minute ~95% threshold.
   - Use safer model-tiered budgets after subtracting output reserve and system/tool reserve.
   - A practical starting point is approximately 85% for large-window models, 80% for medium-window models, and 75% for smaller-window models.

6. **Instruction layering**
   - If AGUIDojo later adds instruction support, merge instruction sources deterministically and show which sources are active.
   - Never let fetched or user-authored content override server policy/system instructions through accidental precedence.

## What AGUIDojo should not copy directly

- **Local-only sessions as source of truth.** Copilot CLI's local session durability is useful for a terminal tool, but AGUIDojo is explicitly moving toward server-owned session identity and cross-device recovery.
- **Copilot-specific entitlement or policy UX.** Auto model selection, plan-specific model availability, and org policies are product-level concerns that AGUIDojo does not need to reproduce in its first picker milestone.
- **Nondeterministic instruction composition.** Public Copilot docs explicitly warn that conflicting instructions can yield unpredictable results. AGUIDojo should make merge behavior explicit if it ever adds instructions.
- **Late compaction.** AGUIDojo should preserve more reserve than the generic CLI flow because tool-result-rich sessions are more likely to spike token usage unexpectedly.

## Sources

- GitHub Docs: [About GitHub Copilot CLI](https://docs.github.com/en/copilot/concepts/agents/about-copilot-cli)
- GitHub Docs: [Using GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/use-copilot-cli)
- GitHub Docs: [Adding custom instructions for GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/copilot-cli/add-custom-instructions)
- GitHub Docs: [About Copilot auto model selection](https://docs.github.com/en/copilot/concepts/auto-model-selection)
- GitHub Docs: [Adding repository custom instructions for GitHub Copilot](https://docs.github.com/en/copilot/how-tos/configure-custom-instructions/add-repository-instructions)
- VS Code Docs: [Use custom instructions in VS Code](https://code.visualstudio.com/docs/copilot/customization/custom-instructions)
- VS Code Docs: [Manage context for AI](https://code.visualstudio.com/docs/copilot/chat/copilot-chat-context)
