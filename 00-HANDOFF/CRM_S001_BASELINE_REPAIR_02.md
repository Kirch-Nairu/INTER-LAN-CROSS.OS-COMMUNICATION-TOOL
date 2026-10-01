# KIRION FORGE — CRM SPRINT 001 — BASELINE REPAIR 02

Technical Authority: Kirch Ivan Balite

Status: BOUNDED PRE-SPRINT SOURCE REPAIR

This repair is infrastructure prerequisite work. It does **not** count toward Writer A or Writer B's 40 substantive sprint commits.

## Exact failing source

`b0bed39e4f1b7bfeb17a65d85383362e74f65b35`

## Independent exact-source evidence

Maintainer validation workflow:

`CRM S001 Exact Baseline`

Workflow run:

`36804002006`

The harness explicitly checked out and asserted:

`b0bed39e4f1b7bfeb17a65d85383362e74f65b35`

Observed result:

- exact SHA assertion: PASS;
- .NET 10 setup: PASS;
- `dotnet restore InterLan.sln`: PASS;
- Release solution build: FAIL;
- aggregate SuiteChecks: SKIPPED because build prerequisite failed.

Compiler defect:

`src/InterLan.Infrastructure/ChatStore.cs(456,26): CS0173`

Reason: `var mutedUntil = condition ? requestedMute : null;` has no target type allowing the compiler to choose between non-nullable `DateTimeOffset` and `null`.

## Authorized repair

Owned file for this repair only:

`src/InterLan.Infrastructure/ChatStore.cs`

Replace the local declaration in `UpdateDirectConversationPreferenceAsync` from targetless `var` inference to an explicit nullable value type.

Required semantic result:

```csharp
DateTimeOffset? mutedUntil = request.MutedUntilUtc is { } requestedMute && requestedMute > now
    ? requestedMute
    : null;
```

No other production behavior is authorized to change.

## Prohibited scope

Do not:

- refactor `ChatStore`;
- redesign direct-message preferences;
- change database schema;
- modify CRM product code;
- modify Writer A or Writer B branches;
- modify unrelated P2 behavior;
- merge/promote/deploy;
- count this repair toward either sprint writer's 40 commits.

## Required validation after repair

A new exact repair candidate SHA must be produced, then the Maintainer exact-source harness must be retargeted to that SHA and run:

```bash
dotnet restore InterLan.sln
dotnet build InterLan.sln --configuration Release --no-restore
dotnet run --project tools/InterLan.SuiteChecks/InterLan.SuiteChecks.csproj --configuration Release --no-build
```

The harness must first assert `git rev-parse HEAD` equals the exact repair candidate SHA.

If another pre-existing baseline defect appears, stop and route another bounded baseline repair rather than allowing A/B to absorb unrelated defects.

## Sprint resume rule

Writer A and Writer B remain at 0/40 and paused.

Only after a repaired exact source passes Release build and aggregate SuiteChecks may both writer branches be re-fanned from the same proven-green SHA and their 40-commit counters begin.
