# P2 C100 ARCHITECTURE REVIEW

Technical Authority: **Kirch Ivan Balite**

Phase: **P2 — Direct Messaging + Durable Pairing**

Review source head:

`bc56986096f4e40690f8bb35ebfb3d7666363450`

This review is the C100 architecture checkpoint required by the V1 code campaign.

## What materially improved since C075

### Persistence and concurrency

- SQLite runs with WAL mode and a five-second busy timeout.
- Concurrent creation of the same DM is expected to converge on one canonical conversation.
- Concurrent unique sends are exercised.
- Concurrent duplicate client-message IDs are exercised.
- Message insertion now uses database-enforced idempotency resolution rather than relying only on a pre-insert read.
- Reusing an idempotency key with different message content fails closed.

### Authority

- Multiple approved devices can attach to one existing durable user identity.
- Device credential rotation replaces the credential and revokes prior device sessions.
- Device/session revocation aborts active realtime connections.
- Delivered/read receipts are recipient actions rather than sender placeholders.

### Server composition

- Canonical runtime settings have an explicit persisted-settings + override model.
- Kestrel, bootstrap, and discovery consume the resolved runtime settings.
- Server endpoint groups are separated from the entrypoint.
- `InterLanServerHost` can build reusable in-process server instances.
- Desktop has an `OwnerServerRuntime` lifecycle that can own that server instance.

This closes the earlier architectural gap where desktop and canonical server existed only as unrelated executables.

### Verification architecture

- Active verification projects are part of `InterLan.sln`.
- `InterLan.Testing` centralizes repository discovery, HTTPS loopback behavior, port leasing, and child-server lifecycle.
- Both P1 and P2 network smokes use the common server harness.
- Historical database fixtures can stop at a selected migration.
- Migration checks replay P0/P1/P2-era schemas through the current migration set.
- Desktop owner-host checks exercise the embedded server lifecycle.
- `InterLan.SuiteChecks` aggregates deterministic P0–P2 verification.

## Current architecture judgment

The codebase is structurally safer for the remaining phases than it was at C075.

In particular, P3 no longer needs to invent:
- a second realtime revocation model;
- a second process test harness;
- a second configuration authority;
- a separate desktop/server lifecycle;
- another migration runner.

Those shared foundations now exist.

## Remaining P2 risks

C100 does **not** mean P2 is complete.

The next risks are:

1. SQLite tests currently prove moderate concurrency, not sustained write contention or long-running mixed workloads.
2. Rate-limit checks prove partition-key construction, not full HTTP limiter behavior under saturation.
3. Historical migration checks prove schema convergence; they do not yet prove preservation of representative historical data.
4. Browser SignalR may still require query-token transport. Request-start logging is suppressed, but browser credential handling needs an explicit client policy.
5. Runtime settings have canonical resolution, but changing listener-affecting settings while running still needs explicit restart-required semantics.
6. Linux/macOS pairing-state protection still relies on file permissions rather than native secret stores.
7. DM product projections are still thin: unread counts, last-message summaries, richer pagination, and client projections remain.
8. The desktop owner runtime is proven as infrastructure, but the visible desktop UI is still not a usable owner/client product.

## C100 local acceptance

Run:

```powershell
dotnet build .\InterLan.sln -c Release

dotnet run `
  --project .\tools\InterLan.SuiteChecks\InterLan.SuiteChecks.csproj `
  -c Release --no-build
```

C100 passes only if the full build and aggregate deterministic suite pass locally.

If C100 fails, fix the first source defect and replay the aggregate suite before further expansion.
