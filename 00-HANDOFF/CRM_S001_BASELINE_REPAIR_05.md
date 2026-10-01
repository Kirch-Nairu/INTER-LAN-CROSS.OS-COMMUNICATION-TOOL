# KIRION FORGE — CRM SPRINT 001 — BASELINE REPAIR 05

Technical Authority: Kirch Ivan Balite

Status: BOUNDED PRE-SPRINT REALTIME SMOKE-HARNESS REPAIR

This repair is prerequisite baseline work and does **not** count toward Writer A or Writer B's 40 substantive sprint commits.

## Exact failing source

`9b1b2e75671d8271f3dc0e5e571f20114c66f2b4`

## Independent exact-source evidence

Maintainer validation workflow:

`CRM S001 Exact Baseline`

Workflow run:

`36839681925`

The harness explicitly checked out and asserted:

`9b1b2e75671d8271f3dc0e5e571f20114c66f2b4`

Observed result:

- exact SHA assertion: PASS;
- .NET 10 setup: PASS;
- `dotnet restore InterLan.sln`: PASS;
- Release solution build: PASS with 0 warnings / 0 errors;
- P0 Foundation: PASS;
- P1 Identity: PASS;
- P1 Network: PASS;
- P2 Core: PASS;
- aggregate SuiteChecks: FAIL in P2 Realtime.

Failure:

`FAIL realtime ticket replay was accepted`

## Diagnosis

The server-side ticket implementation is already single-use:

`RealtimeTicketStore.Consume(...)` hashes the presented ticket and atomically removes it from the concurrent dictionary with `TryRemove`. A second consume therefore returns `null`.

`ChatHub.OnConnectedAsync()` resolves the ticket through `RealtimeTicketStore.Consume(...)`. When a replayed ticket resolves to `null`, the hub calls `Context.Abort()` and returns before registering the connection or adding it to authenticated groups.

The smoke harness currently treats this as its sole replay-rejection criterion:

```csharp
try
{
    await replayHub.StartAsync();
}
catch
{
    ticketReplayRejected = true;
}
```

That criterion is too strict for SignalR connection timing. `StartAsync()` may complete the transport/handshake before the server-side `OnConnectedAsync()` abort becomes observable to the client. Therefore a returned `StartAsync()` does not prove that the replayed ticket produced a usable authenticated hub connection.

The correct security property is that a replayed ticket cannot establish a usable authenticated hub session.

## Authorized repair

Owned file for this repair only:

`tools/InterLan.P2RealtimeSmoke/Program.cs`

In the existing replay block, preserve the current `StartAsync()` attempt. If it returns, immediately prove whether the connection is actually usable by invoking the existing `ChatHub.Ping` method.

Change only the replay `try` body from:

```csharp
try
{
    await replayHub.StartAsync();
}
catch
{
    ticketReplayRejected = true;
}
```

to:

```csharp
try
{
    await replayHub.StartAsync();
    await replayHub.InvokeAsync("Ping");
}
catch
{
    ticketReplayRejected = true;
}
```

No production behavior is authorized to change.

## Required semantic result

- if replay `StartAsync()` itself fails, replay is rejected;
- if `StartAsync()` returns but the server immediately aborts the unauthenticated replay connection, `Ping` fails and replay is rejected;
- if the replayed ticket somehow produces a genuinely usable authenticated hub connection, `Ping` succeeds and the existing `FAIL realtime ticket replay was accepted` path still fires;
- the first-use ticket success path remains unchanged;
- the test still proves the ticket is single-use and bearer-free at the usable-session boundary.

## Prohibited scope

Do not:

- modify `RealtimeTicketStore.cs`;
- modify `ChatHub.cs`;
- weaken or change ticket consumption semantics;
- change ticket lifetime;
- add bearer fallback to the ticket path;
- alter unrelated realtime tests;
- modify production code;
- modify CRM product code;
- modify Writer A or Writer B branches;
- modify `.github/**`;
- fix unrelated defects;
- merge/promote/deploy;
- count this repair toward either sprint writer's 40 commits.

## Commit contract

Produce exactly one coherent repair commit touching only the replay assertion in `tools/InterLan.P2RealtimeSmoke/Program.cs`.

Suggested commit message:

`fix(crm-bootstrap): verify realtime replay usability`

## Validation after repair

If the execution environment has a checkout and .NET 10, run:

```bash
dotnet restore InterLan.sln
dotnet build InterLan.sln --configuration Release --no-restore
dotnet run --project tools/InterLan.SuiteChecks/InterLan.SuiteChecks.csproj --configuration Release --no-build
```

If local execution is unavailable, do not fabricate validation. The Maintainer owns authoritative exact-SHA replay after return.

The Maintainer must retarget the exact-source harness to the returned candidate SHA and assert exact HEAD identity before restore/build/SuiteChecks.

If another inherited failure appears after this repair, stop and route another bounded baseline repair rather than allowing Writer A/B to absorb it.

## Sprint resume rule

Writer A and Writer B remain at 0/40 and paused.

Only an exact source that passes both Release build and aggregate SuiteChecks may become the new common source for the 40 + 40 CRM Sprint 001 fan-out.
