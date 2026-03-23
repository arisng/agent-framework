# Attachment durability and the AGUIDojo persistence model

## Summary

AGUIDojo originally treated uploaded message attachments as process memory, not durable application data. That design made uploads work during a live server session but fail after an `AGUIDojoServer` restart. The user-visible message still rendered after refresh because the browser had persisted the message text locally, including the embedded attachment marker, but the server no longer had the binary blob behind `/api/files/{id}`.

The current fix keeps the existing `/api/files` contract and the existing `MultimodalAttachmentAgent` marker format, but changes the backing store from an in-memory dictionary to a SQLite-backed attachment table in `ChatSessionsDbContext`.

## What the defect looked like

Before the fix:

- the client uploaded an image to `POST /api/files`
- the server returned a durable-looking attachment id
- the client embedded that id into the message text as `<!-- file:{id}:{fileName}:{contentType} -->`
- the browser persisted the message text in local storage / IndexedDB
- `AGUIDojoServer` kept the actual file bytes only in `InMemoryFileStorageService`

That meant two different persistence stories existed at the same time:

- **message markers survived refresh** because the client persisted them
- **attachment bytes disappeared on restart** because the server never durably stored them

After a restart, the same `<img src="/api/files/{id}">` URL still existed in the hydrated UI, but the server no longer found the file and returned the placeholder SVG instead.

## Why markers survived while blobs did not

This bug came from a boundary mismatch:

- the **UI cache** persisted message text
- the **server** owned binary attachment retrieval
- the **attachment id** crossed that boundary
- the **binary bytes** did not

So the browser could truthfully remember that a message referenced attachment `abc123`, while the server had already forgotten what `abc123` meant.

This is different from ordinary text-message hydration bugs. The chat text was not corrupted. The identifier was still valid as a piece of message content. What failed was the server-side lookup behind that identifier.

## Persistence model introduced by the fix

The fix adds a durable `ChatAttachments` table to the server database with:

- attachment id
- file name
- content type
- binary payload
- file size
- upload timestamp
- expiration timestamp

`DatabaseFileStorageService` now stores and reads attachments from that table, so `/api/files/{id}` survives ordinary server restarts as long as the SQLite database file survives.

The server still preserves a bounded retention model instead of unbounded growth:

- attachments receive a server-side expiration timestamp
- expired rows are deleted during periodic upload cleanup
- expired rows are also deleted on read if encountered later

This keeps cleanup explicit and observable without swallowing storage failures.

## Backward-compatibility detail

`AGUIDojoServer` still bootstraps the sample database with `EnsureCreated`, but `EnsureCreated` does not add new tables to an already-existing SQLite database. That matters for issue 2 because many local runs already have an older `aguidojo-sessions.db` file.

To handle that safely, startup now runs a small schema initializer that:

1. calls `EnsureCreated` for fresh databases
2. executes `CREATE TABLE IF NOT EXISTS` and `CREATE INDEX IF NOT EXISTS` for `ChatAttachments`

That keeps existing sample databases usable without requiring the user to delete their DB file first.

## Resulting behavior

With the durable attachment store in place:

- existing message markers still work
- `MultimodalAttachmentAgent` still resolves markers the same way
- `/api/files/{id}` still serves the same contract
- restarting only `AGUIDojoServer` no longer breaks recently uploaded attachments

The important architectural lesson is that attachment ids are part of durable chat state only when the binary content behind those ids is durable too.
