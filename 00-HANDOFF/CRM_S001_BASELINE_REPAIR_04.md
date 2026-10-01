# KIRION FORGE — CRM SPRINT 001 — BASELINE REPAIR 04

Technical Authority: Kirch Ivan Balite

Status: BOUNDED PRE-SPRINT CHECK-HARNESS REPAIR

This repair is prerequisite baseline work and does **not** count toward Writer A or Writer B's 40 substantive sprint commits.

## Exact failing source

`1f0eeda99625549400ec4f06e39650301ac1ae4b`

## Independent exact-source evidence

Maintainer validation workflow:

`CRM S001 Exact Baseline`

Workflow run:

`36838634353`

The harness explicitly checked out and asserted:

`1f0eeda99625549400ec4f06e39650301ac1ae4b`

Observed result:

- exact SHA assertion: PASS;
- .NET 10 setup: PASS;
- `dotnet restore InterLan.sln`: PASS;
- Release solution build: PASS with 0 warnings / 0 errors;
- aggregate SuiteChecks: FAIL in P1 Identity with exit 134.

Runtime failure:

`System.UnauthorizedAccessException: Invalid credentials.`

Origin:

`src/InterLan.Infrastructure/EnrollmentStore.cs`

Surfaced by:

`tools/InterLan.P1Checks/Program.cs`

## Diagnosis

The P1 harness bootstraps the canonical owner using:

```text
username: kirch
password: correct horse battery staple
```

The harness successfully logs in with those credentials earlier in the same run.

Later, the harness attempts to create two additional owner sessions using stale/nonexistent credentials:

```text
username: owner1
password: owner-password-123
```

Those two stale credential pairs are used for:

- `extraOwnerSession` before the revoke-other-sessions check;
- `logoutSession` before the self-logout check.

Repository search confirms `owner-password-123` is only used in those two P1 check-harness login calls. No corresponding owner bootstrap exists.

Therefore the failure is a check-harness fixture inconsistency, not an `EnrollmentStore.LoginOwnerAsync` authentication defect.

## Authorized repair

Owned file for this repair only:

`tools/InterLan.P1Checks/Program.cs`

Change exactly the two stale owner login calls from:

```csharp
"owner1",
"owner-password-123",
```

to the already-bootstrapped canonical owner credentials:

```csharp
"kirch",
"correct horse battery staple",
```

Apply this replacement only to:

- `extraOwnerSession`;
- `logoutSession`.

Do not modify the initial successful owner login, bad-password negative test, bootstrap data, assertions, session semantics, or production authentication code.

## Required semantic result

- `extraOwnerSession` is a valid second session for the same canonical owner;
- `RevokeOtherSessionsAsync` continues to prove that the retained current session remains valid while the additional owner session is revoked;
- `logoutSession` is a valid owner session that can be revoked by `RevokeOwnSessionAsync`;
- all existing assertion order/messages remain unchanged;
- production authentication behavior remains untouched.

## Prohibited scope

Do not:

- modify `EnrollmentStore.cs`;
- relax credential verification;
- create a second owner;
- change bootstrap credentials;
- alter session/revocation behavior;
- rewrite or refactor P1Checks;
- modify production code;
- modify CRM product code;
- modify Writer A or Writer B branches;
- modify `.github/**`;
- fix unrelated defects;
- merge/promote/deploy;
- count this repair toward either sprint writer's 40 commits.

## Commit contract

Produce exactly one coherent repair commit touching only the two stale login credential pairs in `tools/InterLan.P1Checks/Program.cs`.

Suggested commit message:

`fix(crm-bootstrap): align P1 owner session credentials`

## Validation after repair

If the execution environment has a checkout and .NET 10, run:

```bash
dotnet restore InterLan.sln
dotnet build InterLan.sln --configuration Release --no-restore
dotnet run --project tools/InterLan.SuiteChecks/InterLan.SuiteChecks.csproj --configuration Release --no-build
```

If local execution is unavailable, do not fabricate validation. The Maintainer owns authoritative exact-SHA replay after return.

The Maintainer must retarget the exact-source harness to the returned candidate SHA and assert exact HEAD identity before restore/build/SuiteChecks.

If another inherited failure appears after this repair, stop and route another bounded baseline repair instead of allowing Writer A/B to absorb it.

## Sprint resume rule

Writer A and Writer B remain at 0/40 and paused.

Only an exact source that passes both Release build and aggregate SuiteChecks may become the new common source for the 40 + 40 CRM Sprint 001 fan-out.
