# KIRION FORGE — CRM SPRINT 001 — BASELINE REPAIR 03

Technical Authority: Kirch Ivan Balite

Status: BOUNDED PRE-SPRINT SOURCE REPAIR

This repair is infrastructure prerequisite work. It does **not** count toward Writer A or Writer B's 40 substantive sprint commits.

## Exact failing source

`ca9291bab5d5d94d8eb79f213023530bc05929af`

## Independent exact-source evidence

Maintainer validation workflow:

`CRM S001 Exact Baseline`

Workflow run:

`36838029123`

The harness explicitly checked out and asserted:

`ca9291bab5d5d94d8eb79f213023530bc05929af`

Observed result:

- exact SHA assertion: PASS;
- .NET 10 setup: PASS;
- `dotnet restore InterLan.sln`: PASS;
- Release solution build: FAIL;
- aggregate SuiteChecks: SKIPPED because build prerequisite failed.

Compiler defect:

`tools/InterLan.P2Checks/Program.cs(12,1): CS0165`

Reason: the top-level program invokes the local `Check(...)` function before the captured local variable `failures` has been definitely assigned. The declaration `var failures = new List<string>();` currently appears after the initial `Check(...)` calls.

## Authorized repair

Owned file for this repair only:

`tools/InterLan.P2Checks/Program.cs`

Move the existing declaration:

```csharp
var failures = new List<string>();
```

so it appears before the first invocation of `Check(...)`.

Do not alter the declaration contents, `Check(...)` behavior, assertion messages, rate-limit checks, or any later P2 check logic.

Required semantic result:

- `failures` is definitely assigned before any `Check(...)` invocation;
- all existing assertions run in their current order;
- failures continue to be accumulated by the same local function;
- no behavioral change beyond making the existing check harness compile.

## Prohibited scope

Do not:

- rewrite or refactor the P2 check harness;
- alter assertion semantics or messages;
- modify production code;
- modify CRM product code;
- modify Writer A or Writer B branches;
- modify `.github/**`;
- repair unrelated warnings or defects;
- merge/promote/deploy;
- count this repair toward either sprint writer's 40 commits.

## Commit contract

Produce exactly one coherent repair commit containing only the declaration relocation.

Suggested commit message:

`fix(crm-bootstrap): initialize P2 check failures before use`

## Validation after repair

If the execution environment has the repository checkout and .NET 10, run:

```bash
dotnet restore InterLan.sln
dotnet build InterLan.sln --configuration Release --no-restore
dotnet run --project tools/InterLan.SuiteChecks/InterLan.SuiteChecks.csproj --configuration Release --no-build
```

If local execution is unavailable, do not fabricate validation. The Maintainer owns authoritative exact-SHA replay after return.

A new exact repair candidate SHA must be produced, then the Maintainer exact-source harness must be retargeted to that SHA and must assert `git rev-parse HEAD` equals the exact candidate before restore/build/SuiteChecks.

If another pre-existing baseline defect appears, stop and route another bounded baseline repair rather than allowing A/B to absorb it.

## Sprint resume rule

Writer A and Writer B remain at 0/40 and paused.

Only after an exact source passes Release build and aggregate SuiteChecks may both writer branches be re-fanned from the same proven-green SHA and their 40-commit counters begin.
