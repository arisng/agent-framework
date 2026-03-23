# Copilot CLI public repo grounding for AGUIDojo

## Executive summary

The public [`github/copilot-cli`](https://github.com/github/copilot-cli) repository is **not** a normal source-available application repo. Its top-level surface is a release/distribution repo built around `README.md`, `install.sh`, `changelog.md`, and GitHub automation under `.github/`, with no public `src/`, `packages/`, or equivalent implementation tree exposed in the root listing.[^1] That means the most concrete implementation-adjacent evidence for overlapping AGUIDojo features comes from three places: release packaging (`install.sh` and `winget.yml`), operational/support artifacts (issue templates and issue workflows), and a very detailed `changelog.md` that records feature rollouts and storage/config paths over time.[^2][^3][^4][^5]

This note complements [Copilot CLI patterns relevant to AGUIDojo](./copilot-cli-session-context-and-instruction-patterns.md): that earlier pass distilled public docs and product-surface patterns, while this pass grounds the overlapping signals in artifacts directly visible from the public repo.

Even with that limitation, the repo is still valuable. It shows that Copilot CLI treats sessions as durable first-class objects with resume/undo/restart/reporting flows, model selection as a session-scoped concern with entitlement/policy-aware UI, long-running context management as checkpointed background compaction, and custom instructions as a multi-source composition system that eventually moved away from priority-based fallbacks toward combined instruction sets.[^5][^6][^7][^8][^9] Those are directly relevant design signals for AGUIDojo, even though the public repo does not expose the exact serialization code or prompt-construction code paths.

The strongest AGUIDojo implication is this: **borrow Copilot CLI's product behaviors, not its hidden internals**. The public repo gives enough evidence to justify server-owned durable chat sessions, requested-versus-effective model metadata, background compaction with checkpoints, explicit support/debug artifacts, and visible instruction-source diagnostics. It does **not** give enough public code to justify copying undocumented data models or assuming exact merge/compaction algorithms.[^5][^10][^11]

## Architecture and visibility overview

### What the public repo actually exposes

The public repo exposes a distribution/control-plane surface rather than the main runtime source tree.[^1] The observable architecture looks like this:

```text
┌────────────────────────────────────────────────────────────┐
│ Public github/copilot-cli repo                            │
│                                                            │
│  README.md      -> public product surface / supported UX   │
│  install.sh     -> release artifact download + checksum    │
│  changelog.md   -> feature rollout history / behavior      │
│  .github/*      -> issue templates, support ops, packaging │
└────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────┐
│ GitHub Releases / package distribution                     │
│                                                            │
│  copilot-<platform>-<arch>.tar.gz                          │
│  WinGet release assets for win32-x64 / win32-arm64         │
│  SHA256SUMS.txt                                            │
└────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────┐
│ Installed CLI binary                                       │
│                                                            │
│  Session commands, model picker, compaction, instructions  │
│  are visible mostly through changelog + support metadata   │
└────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────┐
│ Local runtime state under ~/.copilot/*                     │
│                                                            │
│  logs / session files / package cache / instructions       │
│  are referenced in support and changelog artifacts         │
└────────────────────────────────────────────────────────────┘
```

This matters because it changes how much we can safely infer. Packaging and operational boundaries are directly grounded. Detailed runtime algorithms are not.[^1][^3][^4][^11][^12]

## Key repo / artifact summary

| Repository / artifact | What it directly proves | Why it matters to AGUIDojo |
|---|---|---|
| [`github/copilot-cli`](https://github.com/github/copilot-cli) root | The public repo is packaging- and operations-focused, not a full public source tree.[^1] | We should treat changelog and support artifacts as the strongest public evidence, not assume hidden implementation details. |
| `README.md` | The product surface is terminal-native, GitHub-integrated, MCP-extensible, and approval-gated.[^2] | Confirms the product-level interaction model AGUIDojo is overlapping with. |
| `install.sh` | Linux/macOS installs come from GitHub release tarballs plus checksum validation.[^3] | Shows the repo is a distribution entrypoint rather than the implementation home. |
| `.github/workflows/winget.yml` | Windows packaging is release-asset driven as well.[^4] | Reinforces that release automation, not build/source publication, is what the repo exposes. |
| `changelog.md` | The changelog records concrete feature rollout history for sessions, models, compaction, instructions, hooks, and permissions.[^5][^6][^8][^9] | This is the best public implementation-adjacent evidence for design and evolution. |
| `.github/ISSUE_TEMPLATE/bug_report.yml` + support workflow | The support model expects logs, CLI flags, environment details, and even session files from disk.[^11][^12] | Strong signal that session state and logs are real support artifacts, not purely ephemeral UI state. |

## Public repo surface: what is grounded vs. what is missing

### Directly grounded in the public repo

1. **Packaging and release topology**  
   The install path is release-driven: `install.sh` downloads `copilot-${PLATFORM}-${ARCH}.tar.gz`, fetches `SHA256SUMS.txt`, validates checksums when possible, and installs the `copilot` binary into `$PREFIX/bin`.[^3] The Windows release path is also release-asset driven through the WinGet workflow, which submits `win32-x64.zip` and `win32-arm64.zip` assets from GitHub releases.[^4]

2. **Product surface area**  
   The README explicitly positions the CLI as terminal-native, GitHub-integrated, agentic, MCP-powered, and approval-gated, which matches the high-level overlap with AGUIDojo's chat/tool orchestration story.[^2]

3. **Operational state and support expectations**  
   The bug-report template asks for `--log-level`, `--log-file`, OS, CPU, terminal emulator, and shell details, while the issue automation explicitly asks users for files from `~/.copilot/logs` and `~/.copilot/history-session-state` when debugging reproduction issues.[^11][^12]

4. **Feature evolution history**  
   The changelog is unusually detailed and names specific session, model, compaction, instruction, hook, and permission behaviors across releases.[^5][^6][^7][^8][^9]

### Not exposed in the public repo

1. **No public runtime source tree**  
   The public root listing does not expose the main CLI runtime implementation in a normal `src/`, `packages/`, or equivalent tree.[^1]

2. **No public serialization or compaction code**  
   There is no directly inspectable public code for session file schemas, checkpoint formats, model picker data structures, instruction merge logic, or compaction algorithms in this repo surface.[^1][^5]

3. **No public prompt-construction pipeline**  
   The repo gives evidence that instruction files, hooks, and permissions exist, but not the exact runtime order in which they are applied inside the binary.[^8][^9][^10]

For AGUIDojo, that means we can use this repo to ground **product behavior and design boundaries**, but not to clone the exact implementation.[^1][^5]

## Session lifecycle and durable artifacts

The public repo strongly supports the conclusion that Copilot CLI treats sessions as durable user objects, not as throwaway request IDs. In the recent changelog alone, the product adds multiple concurrent sessions, preserves session history across `/quit`, `Ctrl+C`, and restart, accepts task IDs in `--resume`, fixes corruption on old sessions, and supports `/undo`/rewind-style flows.[^5][^6][^7][^13][^14] Older entries add `/resume`, AI-generated session names, remote-session loading, usage persistence to `events.jsonl`, and SDK support for loading existing sessions.[^15][^16][^17][^18][^19]

The operational metadata reinforces that these sessions are durable files on disk. The bug-report template asks for CLI logs captured via `--log-level` and `--log-file`, and the issue-automation workflow asks users to attach session files from `~/.copilot/history-session-state` when reproduction requires deeper debugging.[^11][^12] The changelog also references `~/.copilot/pkg` for auto-update state and `events.jsonl` for session usage persistence, which implies a broader local state model rather than a single monolithic history blob.[^17][^20]

### What this means for AGUIDojo

- **Confirmed transferable pattern:** session identity, session recovery, and support/debug artifacts should be treated as first-class product concerns, not hidden runtime details.[^5][^11][^12]
- **Good AGUIDojo analogue:** a server-owned `chatSessionId` plus durable audit/support artifacts is a better match than treating AG-UI `threadId` or model runtime IDs as the business identity.[^5][^12]
- **Important caution:** the public repo proves the *existence* of durable session files, but not their schema. AGUIDojo should not infer Copilot CLI's exact storage model from the repo.[^1][^12]

## Model picker and model switching

The public repo shows a surprisingly mature model-management surface. The changelog documents that the model picker is entitlement- and policy-aware, eventually grouping models into **Available**, **Blocked/Disabled**, and **Upgrade** tabs, and ensuring that Pro/trial users see all models they are entitled to use.[^5][^7] Earlier changes add fuzzy search in the model picker, a `/models` alias, clearer `/model` error messages, the ability to enable disabled models directly, and a two-column picker layout with clearer multipliers/availability signaling.[^16][^21][^22][^23]

The public history also shows that model selection is not just a startup default. The changelog records that ACP server mode can change models during a session, that model switching from Codex to Opus preserves conversation history correctly, that chat history handling between model families was improved, and that subagents can fall back to the session model when a default model is blocked by policy.[^21][^24][^25][^26]

### What this means for AGUIDojo

- **Confirmed transferable pattern:** separate the session's **requested/preferred model** from the **effective model that actually served a turn**. Copilot CLI's public behavior clearly distinguishes user choice, entitlement/policy filtering, and actual serving behavior.[^5][^21][^23]
- **Good AGUIDojo analogue:** store `preferredModelId` on the session, `effectiveModelId` on the turn or audit event, and keep provider/deployment routing server-side.
- **What not to copy directly:** Copilot's exact entitlement and upgrade-tab UX is product-specific. AGUIDojo likely needs a much thinner model catalog than Copilot's public picker surface.[^5][^7]

## Context window management and compaction

The public changelog gives a coherent picture of how Copilot CLI handles long-running context. It adds `/context` to visualize token usage, introduces auto-compaction at the **95% token limit** together with `/compact`, and later explicitly states that auto-compaction runs in the background without blocking the conversation.[^22][^27][^28] Over subsequent releases, the changelog says the SDK supports "infinite sessions" through automatic compaction checkpoints, background compaction preserves tool-call sequences correctly, extended thinking is preserved after compaction, `/compact` queues messages safely, compaction timeline entries and checkpoint-summary hints were simplified, and transient events are evicted after compaction to reduce memory growth.[^16][^18][^24][^25][^29]

Those details matter because they show that Copilot CLI is not simply truncating a transcript. The public behavior points to a **checkpointed compaction model** with durability and UX affordances around it: users can visualize context, manually compact, preserve reasoning/tool-call semantics, and keep the session running rather than starting over.[^16][^22][^27][^28]

### What this means for AGUIDojo

- **Confirmed transferable pattern:** long-running sessions should use background compaction plus explicit checkpoints/summaries, not silent destructive trimming.[^16][^24][^27]
- **Good AGUIDojo analogue:** compact the *invocation context* while leaving the canonical branching conversation intact, and preserve compaction checkpoints/audit events as explainability artifacts.
- **What not to copy directly:** Copilot's public **95%** trigger is probably too late for AGUIDojo because AGUIDojo mixes tool outputs, approvals, and richer session artifacts into the same interaction model. The repo supports the *checkpoint pattern*, not a requirement to use the same threshold.[^27][^29]

## Custom instructions, hooks, trust boundaries, and prompt-safety surfaces

The public repo shows that Copilot CLI's instruction model kept evolving. It added `/instructions`, later upgraded the instructions picker into a full-screen alt-screen view, and shows individual instruction file names — including `[external]` labels — in the picker.[^10][^26][^30] On the configuration side, the changelog records support for user-level instruction files under `~/.copilot/instructions/*.instructions.md`, custom instruction directories via `COPILOT_CUSTOM_INSTRUCTIONS_DIRS`, case-insensitive custom instruction recognition, and `applyTo` frontmatter accepting both string and array values.[^8][^26][^30][^31]

The most important design signal is behavioral: Copilot CLI explicitly changed from **priority-based fallbacks** to **combining all custom instruction files**, and later added deduplication for identical model instruction files to save context.[^16][^31] That is highly relevant to AGUIDojo because it proves the product team chose composition over single-winner precedence, then had to add visibility and deduplication to keep that system manageable.[^10][^16]

The repo also exposes several trust-boundary decisions that overlap with prompt-injection safety:

- workspace MCP servers from `.mcp.json`, `.vscode/mcp.json`, and `devcontainer.json` are loaded **only after folder trust is confirmed**[^5]
- repo-level hooks are also gated on trust[^6]
- URL permission controls were added for common shell/web access[^22]
- `web_fetch` rejects `file://` URLs[^28]
- UNC/network paths are blocked to avoid credential leakage[^9]
- additional shell-expansion guardrails were added for malicious substitution patterns[^32]
- hooks can ask for confirmation or deny/modify tool execution before it runs[^9][^10][^14]

### What this means for AGUIDojo

- **Confirmed transferable pattern:** if AGUIDojo ever adds instruction files, prompt augmentation, or workspace/project-level guidance, it should make active instruction sources visible and auditable.[^10][^31]
- **Confirmed transferable pattern:** untrusted workspace content should be gated before it can affect executed behavior. Copilot CLI does not blindly load workspace MCP/hook behavior before trust confirmation.[^5][^6]
- **Likely better AGUIDojo choice:** keep instruction composition deterministic even if Copilot CLI currently combines files. Copilot's repo shows the product moving toward combination, but AGUIDojo may want stronger precedence rules because it is a sample application with server-owned policy, not a general-purpose coding assistant.[^10][^31]

## Concrete AGUIDojo design implications

### 1. Treat session state as a supportable artifact, not only a UX convenience

The public repo expects bug reports to include logs and sometimes session files, and it persists additional session usage data to `events.jsonl`.[^11][^12][^17] AGUIDojo should take the same lesson even if it uses a server database instead of local files: durable session state is not only for resume, it is also a support/debug surface.

**Actionable AGUIDojo implication:** plan for explicit server-side audit/export surfaces around sessions, not just chat history hydration.

### 2. Separate requested model, effective model, and policy/availability state

The public changelog makes it clear that model selection is filtered by plan/policy and that switching models must preserve history across families.[^5][^7][^21][^25] AGUIDojo should therefore avoid a single `ModelId` field that tries to mean both "what the user asked for" and "what the server actually ran."

**Actionable AGUIDojo implication:** keep `preferredModelId` on the session, `effectiveModelId` per assistant turn or audit event, and optionally support a future `auto` pseudo-model as a server policy.

### 3. Prefer checkpointed compaction over destructive trimming

The public repo's compaction story is richer than "drop old messages": it adds `/context`, background auto-compaction, checkpoint summaries, preserved tool-call sequences, and preserved extended thinking.[^16][^24][^27][^28][^29]

**Actionable AGUIDojo implication:** compaction should produce explicit durable summaries/checkpoints tied to a canonical conversation graph, not mutate the business history into a flattened summarized transcript.

### 4. Show instruction sources if instructions ever become a feature

Copilot CLI had to add `/instructions`, picker views, external labels, apply-to parsing flexibility, custom instruction dir handling, and deduplication once instruction composition got more complex.[^8][^10][^26][^30][^31]

**Actionable AGUIDojo implication:** if AGUIDojo later supports instruction layers (for example repo policy, workflow policy, entity policy, user/session preference), make the active sources observable in UI or diagnostics from day one.

### 5. Keep trust boundaries explicit

The public repo shows multiple examples of "load only after trust", "ask before tool execution", "block dangerous paths/URLs", and "add extra shell-expansion guardrails."[^5][^6][^9][^10][^22][^28][^32]

**Actionable AGUIDojo implication:** uploaded repos, fetched URLs, user-authored instructions, and any server-executed automation should live behind explicit trust/approval boundaries rather than being treated as harmless prompt text.

## Bottom line for AGUIDojo

Looking at the actual public repo does **not** give us private runtime source code we can copy. What it does give us is something almost as useful for product design: a concrete record of which session/model/context/instruction behaviors GitHub had to harden, expose, and operationalize in the CLI over time.[^3][^5][^11] For AGUIDojo, the right move is to copy the **design patterns** that are repeatedly reinforced by the repo — durable sessions, requested/effective model separation, background checkpointed compaction, visible instruction sources, and explicit trust gates — while resisting the temptation to guess undocumented internal data structures from a packaging repo.[^1][^5][^10]

## Confidence assessment

### High confidence

- The public `github/copilot-cli` repo is distribution/operations focused rather than a full public source repo.[^1][^3][^4]
- The repo gives strong public evidence for durable session concepts, model-picker maturity, background compaction, instruction composition, and trust/permission gating through changelog and support artifacts.[^5][^8][^9][^10][^11][^12]
- Those behaviors are directly relevant to AGUIDojo design, especially for session identity, requested-versus-effective model metadata, checkpointed compaction, and instruction visibility.[^5][^10][^16][^27]

### Medium confidence

- Exact internal schemas for session files, compaction checkpoints, and instruction merge order are **not** publicly visible in this repo, so any AGUIDojo data-model parallels beyond the public behaviors would be inference rather than verification.[^1][^5]
- The issue workflow references `~/.copilot/history-session-state`, while newer docs and runtime surfaces may use different wording for session directories; that suggests path evolution or support-text lag, but the public repo does not expose enough code to fully resolve the discrepancy.[^12]

### Low confidence / intentionally avoided inference

- I did **not** infer Copilot CLI's private prompt-building pipeline, checkpoint schema, or serialized session object model from the public changelog alone.
- I did **not** assume that AGUIDojo should copy Copilot's exact 95% compaction threshold, entitlement UX, or instruction-combination policy just because the public repo shows those product choices.[^5][^27][^31]

## Footnotes

[^1]: [`github/copilot-cli`](https://github.com/github/copilot-cli) repository root listing at commit `4b16981bb5bed352da62524a45412285b225669c` exposes only `.github/`, `README.md`, `changelog.md`, `install.sh`, and `LICENSE.md` in the top-level tree.
[^2]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `README.md:5-20` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^3]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `install.sh:39-117` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^4]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `.github/workflows/winget.yml:1-43` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^5]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 1.0.10 - 2026-03-20` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^6]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 1.0.8 - 2026-03-18` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^7]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 1.0.7 - 2026-03-17` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^8]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 1.0.6 - 2026-03-16` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^9]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 1.0.5 - 2026-03-13` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^10]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 1.0.4 - 2026-03-11` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^11]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `.github/ISSUE_TEMPLATE/bug_report.yml:1-33` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^12]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `.github/workflows/unable-to-reproduce-comment.yml:1-34` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^13]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.422 - 2026-03-05` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^14]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.399 - 2026-01-29` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^15]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.386 - 2026-01-19` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^16]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.394 - 2026-01-24` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^17]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.421 - 2026-03-03` and `## 0.0.422 - 2026-03-05` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^18]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.410 - 2026-02-14` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^19]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.419 - 2026-02-27` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^20]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.421 - 2026-03-03` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^21]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.400 - 2026-01-30` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^22]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.372 - 2025-12-19` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^23]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.370 - 2025-12-18` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^24]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` sections `## 0.0.390 - 2026-01-22` and `## 0.0.385 - 2026-01-19` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^25]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` sections `## 0.0.401 - 2026-02-03` and `## 0.0.385 - 2026-01-19` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^26]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` sections `## 0.0.412 - 2026-02-19` and `## 0.0.407 - 2026-02-11` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^27]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.374 - 2026-01-02` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^28]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.380 - 2026-01-13` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^29]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` sections `## 0.0.386 - 2026-01-19`, `## 0.0.394 - 2026-01-24`, `## 0.0.399 - 2026-01-29`, and `## 0.0.410 - 2026-02-14` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^30]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` sections `## 0.0.407 - 2026-02-11`, `## 0.0.412 - 2026-02-19`, and `## 1.0.4 - 2026-03-11` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^31]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` sections `## 0.0.385 - 2026-01-19`, `## 0.0.394 - 2026-01-24`, `## 0.0.412 - 2026-02-19`, and `## 1.0.6 - 2026-03-16` (commit `4b16981bb5bed352da62524a45412285b225669c`).
[^32]: [`github/copilot-cli`](https://github.com/github/copilot-cli) `changelog.md` section `## 0.0.423 - 2026-03-06` (commit `4b16981bb5bed352da62524a45412285b225669c`).
