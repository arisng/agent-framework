# Simulated ownership, workflow, and subject links

AGUIDojo now makes the ownership boundary explicit without pretending the sample already has a real business module or production identity system.

## What the seam carries

The durable chat-session row already stores thin contextual links that are useful for routing and correlation:

- simulated `OwnerId`
- simulated `TenantId`
- business-subject link fields (`SubjectModule`, `SubjectEntityType`, `SubjectEntityId`)
- workflow/runtime correlation links (`WorkflowInstanceId`, `RuntimeInstanceId`)
- AG-UI thread correlation and preferred-model metadata

`ChatSessionMiddleware` now places that data into a request-scoped `ChatSessionRoutingContext` inside `HttpContext.Items` after it ensures the server-owned session row.

That means downstream tools, agents, and services can resolve the active chat-session context from the current request instead of reparsing forwarded headers or inventing parallel ownership state.

## Where those links surface

The same thin links are now visible on the server-owned inspection surfaces:

- session catalog/detail reads can expose owner, tenant, subject, workflow, runtime, and preferred-model correlation metadata
- the request-scoped routing seam ensures those values flow through the live `/chat` turn
- hydration and cross-browser recovery can keep the same correlation facts without depending on the original browser tab

That is enough realism for the sample: the links survive reload and can be inspected, but they remain routing/correlation facts rather than a fake business module.

## Why this is the right boundary

AGUIDojo is still a chat sample.

It is **not** shipping:

- a real Todo/business subsystem
- production authentication or tenant onboarding
- a rollout where chat persistence becomes the owner of business records

The chat session only carries links and simulated caller context. Business state remains owned by subject/application services. If a future Todo module exists, that module should own Todo records and authorization decisions; the chat session should only carry the current owner/tenant/subject/workflow references needed to route work toward that module.

## How downstream code should use it

Request-scoped code can use `IHttpContextAccessor` plus `ChatSessionHttpContextItems.TryGetRoutingContext(...)` (or `TryGetSessionId(...)` for legacy consumers) to access the active session seam.

The intended pattern is:

1. `POST /chat` arrives with AG-UI `threadId` and optional forwarded props.
2. `ChatSessionMiddleware` ensures the session row, while `ChatSessionService` fills simulated owner/tenant defaults only when the session does not already have explicit values and reuses any persisted subject/workflow links already on that session.
3. The middleware stores the resulting `ChatSessionRoutingContext` in `HttpContext.Items`.
4. Downstream tools/services read that request-scoped seam and decide whether to call subject/application services.

## Important constraints

- `OwnerId` and `TenantId` are still simulated sample values.
- `WorkflowInstanceId`, `RuntimeInstanceId`, `threadId`, and other runtime IDs are correlation links only.
- Chat persistence is not the business source of truth.
- This seam prepares honest ownership routing for future modules; it does not replace those modules.
