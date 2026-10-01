# KIRION FORGE — CRM SPRINT 001 — TWO-WRITER STAGE

Technical Authority: Kirch Ivan Balite

Status: STAGED FOR CODE EXECUTION AFTER COMMON BASELINE REPAIR

Rebaseline authority branch:

`KIRCH-INTERLAN-DEVELOPER-OPS-CRM-REBASELINE`

Staging head at issuance:

`36c661da9207558715ffbabe0352488b29e87767`

## Writers

Writer A:

`00-HANDOFF/CRM_SPRINT_001_WRITER_A_AUTHORITY_CORE.md`

Intended branch:

`KIRCH-INTERLAN-CRM-S001-A-AUTHORITY-CORE`

Writer B:

`00-HANDOFF/CRM_SPRINT_001_WRITER_B_LINUX_RUNTIME.md`

Intended branch:

`KIRCH-INTERLAN-CRM-S001-B-LINUX-RUNTIME`

Both writers target 40 substantive atomic code commits, with no commit-count padding.

## Shared prerequisite

The prior P2 source contains a known compile-blocking malformed control-character literal in:

`src/InterLan.Domain/MessageTextPolicy.cs`

The two sprint branches must not fan out from that broken source.

A single bounded common-baseline repair is authorized before fan-out:

- repair that file only;
- preserve intended behavior allowing TAB/CR/LF while rejecting unsupported control characters;
- run Release build and aggregate SuiteChecks;
- use the repaired exact SHA as the common source for both sprint branches.

This repair is infrastructure prerequisite work and does not count toward either writer's 40 sprint commits.

## Parallel-ownership law

Writer A owns CRM authority/domain/persistence/server-policy work.

Writer B owns Linux host/runtime/native-control/tunnel work.

They must not casually modify each other's owned directories. Shared composition changes must be minimized and called out in their decision logs.

## Code-only emphasis

The sprint is implementation-first.

The only active documentation expected from each writer is its dedicated major-decision log. Do not expand product prose, README material or handoff documentation during normal implementation.

Decision-log entries should be committed alongside the code that implements the decision.

## Integration rule

Neither writer may merge, promote, deploy or self-accept.

After both returns, Maintainer must independently compare both candidates to the common source, inspect overlap/shared-file edits, run integration/review, and produce a separate disposition.
