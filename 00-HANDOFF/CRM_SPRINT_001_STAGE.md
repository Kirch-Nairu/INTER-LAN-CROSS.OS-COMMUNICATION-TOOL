# KIRION FORGE — CRM SPRINT 001 — TWO-WRITER STAGE

Technical Authority: Kirch Ivan Balite

Status: **PAUSED — BASELINE SOURCE FAILURE**

Rebaseline authority branch:

`KIRCH-INTERLAN-DEVELOPER-OPS-CRM-REBASELINE`

Current attempted common source:

`b0bed39e4f1b7bfeb17a65d85383362e74f65b35`

The first bootstrap defect in `src/InterLan.Domain/MessageTextPolicy.cs` was repaired on this source candidate. Both assigned sprint writers correctly stopped at their mandatory pre-mutation gate because their local execution environments could not provide the required .NET/runtime proof.

Maintainer then established an independent exact-source GitHub Actions harness on:

`KIRCH-INTERLAN-CRM-S001-BASELINE-VALIDATION`

Workflow:

`CRM S001 Exact Baseline`

Run:

`36804002006`

The harness explicitly checked out and asserted the exact source SHA `b0bed39e4f1b7bfeb17a65d85383362e74f65b35`, installed .NET 10, restored the solution successfully, and then reproduced a second pre-existing compiler defect:

`src/InterLan.Infrastructure/ChatStore.cs(456,26): CS0173`

The failing declaration uses targetless `var` inference for a conditional expression whose branches are non-nullable `DateTimeOffset` and `null`.

Aggregate SuiteChecks were skipped because the Release build prerequisite failed.

Therefore `b0bed39...` is **not** a green Sprint 001 source and must not be used for CRM implementation.

The second bounded prerequisite repair is governed by:

`00-HANDOFF/CRM_S001_BASELINE_REPAIR_02.md`

No bootstrap repair counts toward either writer's 40 sprint commits.

## Writer A — Authority Core

Handoff:

`00-HANDOFF/CRM_SPRINT_001_WRITER_A_AUTHORITY_CORE.md`

Branch:

`KIRCH-INTERLAN-CRM-S001-A-AUTHORITY-CORE`

Current branch remains exactly:

`b0bed39e4f1b7bfeb17a65d85383362e74f65b35`

Sprint commits: **0 / 40**

State: **PAUSED / CLEAN**

Primary ownership after resume: CRM domain authority, workflow/state policy, persistence, candidate/SHA evidence, declarative JSON work-package import, server contracts/endpoints and focused executable proof.

Decision log:

`docs/decisions/CRM_SPRINT_001_A_DECISIONS.md`

## Writer B — Linux Runtime / Native Control

Handoff:

`00-HANDOFF/CRM_SPRINT_001_WRITER_B_LINUX_RUNTIME.md`

Branch:

`KIRCH-INTERLAN-CRM-S001-B-LINUX-RUNTIME`

Current branch remains exactly:

`b0bed39e4f1b7bfeb17a65d85383362e74f65b35`

Sprint commits: **0 / 40**

State: **PAUSED / CLEAN**

Primary ownership after resume: Debian/KDE host runtime, native Lead shell, Linux lifecycle/path/state abstractions, CLI-oriented start/stop/status orchestration, local gateway ownership, `cloudflared` Quick Tunnel process supervision, Remote Access native state and focused executable proof.

Decision log:

`docs/decisions/CRM_SPRINT_001_B_DECISIONS.md`

## Resume law

Do not continue either 40-commit lane from `b0bed39...`.

Required sequence:

1. apply only the bounded ChatStore baseline repair;
2. produce a new exact candidate SHA;
3. retarget the Maintainer exact-source harness to that SHA;
4. require exact SHA assertion, restore, Release build and aggregate SuiteChecks to PASS;
5. if another pre-existing defect appears, route another bounded baseline repair;
6. once a source is proven green, fan both Writer A and Writer B branches from the same exact green SHA;
7. only then begin the 40 + 40 substantive sprint counters.

## Parallel ownership law after resume

Writer A owns CRM authority/domain/persistence/server-policy work.

Writer B owns Linux host/runtime/native-control/tunnel work.

They must not casually modify each other's owned directories. Shared composition changes must be minimal and recorded in the writer's decision log when they constrain integration.

## Code-only emphasis

The sprint remains implementation-first.

The only active documentation expected from each writer is its dedicated major-decision log. Do not expand README/product prose/handoff documents during normal implementation.

Decision-log entries should normally ride with the code commit that implements the material decision.

No documentation-only padding commits count toward the 40.

## Commit quality law

The target is 40 **substantive** commits per writer, not 40 Git objects at any cost.

No:

- empty commits;
- whitespace churn;
- split-one-line padding;
- meaningless renames;
- reversible fake refactors;
- docs-only contribution farming.

If the authorized lane is genuinely complete before 40 meaningful commits, the writer stops and reports that rather than corrupting benchmark quality.

## Integration rule

Neither writer may merge, promote, deploy or self-accept.

After both completed returns, Maintainer must independently compare both candidates to the proven common source, inspect overlap/shared-file edits, run integration/review and produce a separate disposition.
