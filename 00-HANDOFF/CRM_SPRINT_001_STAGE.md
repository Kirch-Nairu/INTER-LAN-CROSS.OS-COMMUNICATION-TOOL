# KIRION FORGE — CRM SPRINT 001 — TWO-WRITER STAGE

Technical Authority: Kirch Ivan Balite

Status: **ACTIVE — TWO WRITERS ASSIGNED**

Rebaseline authority branch:

`KIRCH-INTERLAN-DEVELOPER-OPS-CRM-REBASELINE`

Common repaired source candidate:

`b0bed39e4f1b7bfeb17a65d85383362e74f65b35`

The known compile-blocking malformed control-character literals in `src/InterLan.Domain/MessageTextPolicy.cs` were repaired on this source candidate. The repaired blob bytes were independently re-read and confirmed to contain escaped `\t`, `\r`, and `\n` source sequences rather than embedded control bytes.

No full runtime/Actions proof was attached to this exact commit at staging time. Therefore both writers MUST independently run the Release build and aggregate SuiteChecks before their first CRM product mutation. A pre-existing failure causes an immediate BLOCKED return; neither writer may silently expand scope to repair unrelated code.

The bootstrap repair is infrastructure prerequisite work and does not count toward either writer's 40 sprint commits.

## Writer A — Authority Core

Handoff:

`00-HANDOFF/CRM_SPRINT_001_WRITER_A_AUTHORITY_CORE.md`

Branch:

`KIRCH-INTERLAN-CRM-S001-A-AUTHORITY-CORE`

Created from exactly:

`b0bed39e4f1b7bfeb17a65d85383362e74f65b35`

Target: **40 substantive atomic code commits**.

Primary ownership: CRM domain authority, workflow/state policy, persistence, candidate/SHA evidence, declarative JSON work-package import, server contracts/endpoints and focused executable proof.

Decision log:

`docs/decisions/CRM_SPRINT_001_A_DECISIONS.md`

## Writer B — Linux Runtime / Native Control

Handoff:

`00-HANDOFF/CRM_SPRINT_001_WRITER_B_LINUX_RUNTIME.md`

Branch:

`KIRCH-INTERLAN-CRM-S001-B-LINUX-RUNTIME`

Created from exactly:

`b0bed39e4f1b7bfeb17a65d85383362e74f65b35`

Target: **40 substantive atomic code commits**.

Primary ownership: Debian/KDE host runtime, native Lead shell, Linux lifecycle/path/state abstractions, CLI-oriented start/stop/status orchestration, local gateway ownership, `cloudflared` Quick Tunnel process supervision, Remote Access native state and focused executable proof.

Decision log:

`docs/decisions/CRM_SPRINT_001_B_DECISIONS.md`

## Parallel ownership law

Writer A owns CRM authority/domain/persistence/server-policy work.

Writer B owns Linux host/runtime/native-control/tunnel work.

They must not casually modify each other's owned directories. Shared composition changes must be minimal and recorded in the writer's decision log when they constrain integration.

## Code-only emphasis

This sprint is implementation-first.

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

After both returns, Maintainer must independently compare both candidates to the common source, inspect overlap/shared-file edits, run integration/review and produce a separate disposition.
