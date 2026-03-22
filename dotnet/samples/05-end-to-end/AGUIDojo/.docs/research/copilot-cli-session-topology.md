# Research: Copilot CLI session topology

This note explains the observable storage and support topology behind Copilot CLI sessions. It complements [copilot-cli-session-state-schema.md](copilot-cli-session-state-schema.md) (artifact and schema inventory), [copilot-cli-public-repo-grounding.md](copilot-cli-public-repo-grounding.md) (public evidence), and [copilot-cli-session-context-and-instruction-patterns.md](copilot-cli-session-context-and-instruction-patterns.md) (product-pattern implications).

The goal here is not to reverse-engineer private internals. It is to describe the safest topology model supported by the evidence we already have, and to extract the parts that are actually useful for AGUIDojo.

## Evidence posture

This note uses three evidence labels:

- **[official]** — explicitly documented in GitHub Copilot CLI docs
- **[empirical]** — directly observed in the companion schema note's file and SQLite inspection
- **[inferred]** — a topology conclusion drawn from official and empirical facts; useful, but not a confirmed private implementation contract

The key official anchor is that GitHub Docs describe `session-store.db` as a local SQLite session store whose contents are **"a subset of the full data stored in the session files"**. Docs also expose `--continue`, `--resume`, `/session ...`, and `/chronicle reindex`. The companion schema note fills in the directly observed file layout under `~/.copilot/` and `~/.copilot/session-state/<uuid>/`.

## Topology at a glance

```text
~/.copilot/
├── session-store.db              # global session catalog / index [official + empirical]
├── logs/                         # process and global logs [empirical]
└── session-state/
    └── <session-id>/             # per-session workspace / artifact bundle [empirical]
        ├── workspace.yaml
        ├── events.jsonl
        ├── checkpoints/
        ├── files/
        ├── research/
        ├── rewind-snapshots/
        └── ... lock / metadata / optional session.db
```

The safest reading is a two-layer persistence model:

1. a **global catalog/index** optimized for lookup, reporting, and cross-session search
2. a **per-session workspace** containing the richer artifacts, event history, checkpoints, and recovery material for one session

Because the session store is officially described as a **subset** of the session files, and because `/chronicle reindex` is an official rebuild path, the SQLite catalog looks best understood as a query-friendly projection over richer session-scoped state rather than the only durable record. **[official + inferred]**

## Layer 1: session catalog / index

Observed `session-store.db` tables include `sessions`, `turns`, `checkpoints`, `session_files`, `session_refs`, and `search_index`. **[empirical]**

This layer appears to answer questions such as:

- Which sessions exist?
- What are their names, directories, timestamps, and high-level summaries?
- What turns, checkpoints, touched files, and cross-references are searchable across sessions?
- What cross-session report or lookup surfaces can be built without opening every raw session directory?

That makes the catalog layer good at:

- **resume and listing flows** such as `--continue` and `--resume` **[official + inferred]**
- **cross-session reporting** such as `/chronicle ...` **[official + inferred]**
- **fast search and indexing** over normalized session facts **[empirical + inferred]**
- **storing a thinner, queryable subset** of richer session data **[official]**

What this layer does **not** safely tell us:

- It does not define the full-fidelity session archive. The docs explicitly say it is only a subset. **[official]**
- It does not prove the complete internal event or compaction model. Richer state exists outside the DB. **[empirical]**
- It does not by itself prove exact rebuild order, transaction boundaries, or ingestion mechanics. **[inferred; exact details remain unconfirmed]**

## Layer 2: per-session workspace / artifact bundle

Each session has its own directory under `session-state/<uuid>/`. The companion schema note observed the following important contents. **[empirical]**

| Artifact | Apparent role |
|---|---|
| `workspace.yaml` | Session metadata envelope: identity, directory, repo, branch, summary, created/updated timestamps |
| `events.jsonl` | Append-only event stream for lifecycle, conversation, tools, hooks, and subagents |
| `checkpoints/` | Durable summaries used to re-establish or compact session context |
| `files/` | Session-scoped file artifacts |
| `research/` | Session-scoped research outputs |
| `rewind-snapshots/` | Point-in-time recovery data and backups |
| `vscode.metadata.json`, lock file, optional `session.db` | Integration/runtime bookkeeping; exact semantics are not fully confirmed |

This layer appears to answer different questions:

- What actually happened inside this one session?
- What support/debug artifacts exist for this session?
- What durable summaries or rewind points were produced?
- What outputs belong to the session beyond a plain transcript?

A useful way to think about the per-session artifacts is:

- **`workspace.yaml` says what the session is.**
- **`events.jsonl` says what happened and when.**
- **`checkpoints/` says what condensed state should survive resume or compaction.**
- **`files/`, `research/`, and `rewind-snapshots/` hold the outputs and recovery material produced around the session.**

That distinction matters for AGUIDojo because a durable session is not just a message list. It is also an audit trail, a support bundle, and a place where derived artifacts accumulate.

## Catalog data versus session-scoped artifacts

The two layers appear complementary rather than redundant.

| Need | Best-fit layer | Why |
|---|---|---|
| Find or resume a recent session | Catalog/index | Cross-session lookup is cheap and centralized |
| Search turns or references across many sessions | Catalog/index | Normalized tables and FTS are built for this |
| Inspect one session's detailed timeline | Session workspace | `events.jsonl` preserves a finer-grained chronology |
| Understand compaction or rewind state | Session workspace | Checkpoints and rewind artifacts live with the session |
| Inspect outputs created during the work | Session workspace | Files, research, and backups are session-local |
| Recover from a stale or broken index | Both | `/chronicle reindex` implies replay from session files back into the catalog |

The topology therefore looks like **index plus artifact bundle**, not **DB versus transcript copy**. The catalog is optimized for global navigation and search. The workspace is optimized for fidelity, explainability, and recovery. **[inferred]**

## How support and debug surfaces emerge from this topology

The support story appears to fall naturally out of the split.

1. **Fast operator-facing lookup**  
   Resume and chronicle-style flows need a cross-session catalog so the CLI can find relevant sessions without scanning every file on every interaction. **[official + inferred]**

2. **Deep per-session inspection**  
   `/session` surfaces map cleanly onto session-local artifacts such as checkpoints, plan state, files, workspace paths, and event/log locations. **[official + empirical]**

3. **Rebuild and repair**  
   `/chronicle reindex` is especially revealing: it makes sense in a topology where raw session artifacts exist independently of the searchable index and can be replayed into it. **[official + inferred]**

4. **Support attachments and repro bundles**  
   The public repo's support artifacts ask users for logs and, in some cases, session files from disk. That is consistent with a system where session state is a durable diagnostic surface rather than transient UI memory. **[official + empirical]**

5. **Explainable long-running sessions**  
   Checkpoints and event streams make compaction, rewind, resume, and tool activity inspectable. That is a stronger operational model than a single opaque "conversation blob." **[empirical + inferred]**

## What is directly confirmed versus inferred

### Directly documented or directly observed

- `session-store.db` exists and is officially described as structured session data that is a **subset** of the full data in session files. **[official]**
- `--continue`, `--resume`, `/session ...`, and `/chronicle reindex` are official product surfaces. **[official]**
- A per-session directory exists under `~/.copilot/session-state/<uuid>/` with observed files such as `workspace.yaml`, `events.jsonl`, and `checkpoints/`. **[empirical]**
- The session store contains normalized tables for sessions, turns, checkpoints, touched files, refs, and a search index. **[empirical]**
- Logs and support flows treat local session state as something users can inspect and attach. **[official + empirical]**

### Inferred, and intentionally kept tentative

- The catalog is best understood as a derived or replayable index over richer session-scoped state. **[inferred]**
- The per-session workspace is likely closer to the durable raw record for a session than the SQLite store is. **[inferred]**
- `/session` likely reads from a mixture of workspace metadata and session-local artifacts, while `/chronicle` likely leans on the global catalog. **[inferred]**
- The exact ingestion pipeline, ordering guarantees, and semantics of the optional `session.db` are not publicly confirmed.

In other words: the topology is well supported, but the private mechanics behind it are not.

## AGUIDojo implications

The durable lesson for AGUIDojo is structural, not literal.

1. **Use a layered session model.**  
   Keep a query-friendly session catalog separate from richer session-scoped artifacts or audit streams.

2. **Treat events and checkpoints as first-class data.**  
   Do not collapse everything into a single flattened transcript table if you want supportable rewind, compaction, or explainability.

3. **Design for rebuildable support surfaces.**  
   A good session system should support both quick list/search views and deep per-session export/debug inspection.

4. **Keep durable business identity separate from runtime correlation IDs.**  
   This aligns with the companion AGUIDojo note: the durable session object should survive model switches, compaction, and transport/runtime churn.

5. **Prefer topology lessons over file-name mimicry.**  
   The useful pattern is "catalog plus artifact bundle," not "copy `~/.copilot/` exactly."

## Non-goals for AGUIDojo

This note does **not** support the following moves:

- copying Copilot CLI's exact directory names, file names, or local-first storage model
- treating every observed artifact (`session.db`, lock files, IDE metadata) as a required AGUIDojo concept
- assuming the observed event schema or checkpoint format is a public contract
- inferring private prompt-building, compaction, or indexing algorithms beyond what the evidence actually supports

## Sources

- [copilot-cli-session-state-schema.md](copilot-cli-session-state-schema.md)
- [copilot-cli-public-repo-grounding.md](copilot-cli-public-repo-grounding.md)
- [copilot-cli-session-context-and-instruction-patterns.md](copilot-cli-session-context-and-instruction-patterns.md)
- GitHub Docs: [About GitHub Copilot CLI](https://docs.github.com/en/copilot/concepts/agents/about-copilot-cli)
- GitHub Docs: [Using GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/use-copilot-cli)
