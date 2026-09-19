# INTER-LAN KNOWN RISKS AND DEBUG-HOLE REGISTER

This register exists so deferred problems cannot disappear from context.

Risk state must be reconciled against observable source/runtime. Historical items are retained and marked where current source appears to address them.

## R1 — Desktop owner lifecycle composition

Status: PARTIALLY ADDRESSED IN SOURCE / P6 ACCEPTANCE STILL OPEN

Reusable `InterLanServerHost` and desktop owner-runtime integration spine exist from P2, but full product composition remains a P6 acceptance responsibility.

## R2 — Configuration authority divergence

Status: PARTIALLY ADDRESSED IN SOURCE

P2 added canonical runtime-settings foundations and SQLite-backed settings. Continue preventing divergence among persisted settings, environment overrides, Kestrel, discovery, storage, and later archive configuration.

## R3 — Verification topology drift

Status: ADDRESSED IN SOURCE, MUST REMAIN GUARDED

The solution and aggregate suite include active P3 verification projects. Future phases must keep repository-local checks inside the complete build/aggregate topology.

## R4 — Network-smoke process/port races

Status: PARTIALLY ADDRESSED

Shared server-process/network harness exists from P2. Continue treating real process/port lifecycle failures as harness/runtime evidence, not as a reason to weaken product tests.

## R5 — Realtime revocation gap

Status: ADDRESSED FOR P2 DEVICES; P3 MEMBERSHIP REVOCATION REMAINS CRITICAL

P2 added active connection revocation. P3 additionally depends on current-membership targeting and per-invocation membership checks so removed group members lose group realtime authority immediately.

Risk to keep attacking:
- existing connected sockets retained in user-global transport groups;
- any new group event path that broadcasts without resolving current membership;
- any hub invocation that trusts connection-time membership.

## R6 — Receipt semantics

Status: IMPLEMENTED IN SOURCE / NOT VERIFIED IN THIS RECONCILIATION

Direct and group receipt paths distinguish recipient delivery/read semantics. Preserve sender restrictions and current group authority.

## R7 — Multi-device identity

Status: IMPLEMENTED IN P2 SOURCE / HISTORICAL RISK

P2 supports additional approved devices for an existing user identity. Later client/product work must preserve this behavior.

## R8 — SQLite concurrency and group authority races

Status: ACTIVE P3 HIGH-PRIORITY RISK

WAL/busy-timeout baseline and P3 concurrency checks exist.

P3 still must prove that concurrent:
- membership add/restore;
- remove vs role mutation;
- role mutation vs send;
- duplicate group send;
- receipt upserts;
- history reads during writes

do not create duplicate authority, stale authorization, uncaught lock races, or inconsistent final state.

Any race that can violate group authority is a stop-feature-expansion defect.

## R9 — Web asset composition

Status: DEFERRED TO P6 PRODUCT COMPOSITION

Do not let final product publish omit web/Android assets. Locked frontend dependency and deterministic composition remain required before P6 acceptance.

## R10 — Dependency reproducibility

Status: OPEN ACROSS CAMPAIGN

Maintain deterministic SDK/NuGet/frontend dependency policy in repository-native tooling. Workflow pinning belongs to the final CI/CD wave.

## R11 — Workflow sediment

Status: INTENTIONALLY DEFERRED

Do not add P3–P7 workflow architecture. Existing workflows are passive signals only. Consolidation occurs after P7 code freeze.

## R12 — Live Telegram nondeterminism

Status: FUTURE P5

Mandatory archive proof must use deterministic abstraction/fake. Live Telegram is optional staging evidence.

## R13 — Cross-platform filesystem behavior

Status: FUTURE P4/P7

Path, Unicode, locking, permissions, case sensitivity, and atomic replace behavior remain portability risks.

## R14 — Unix pairing-state protection

Status: OPEN / PLATFORM POLICY REQUIRED

Windows DPAPI is stronger than the existing Unix fallback. macOS Keychain / Linux Secret Service policy remains required before final native acceptance.

## R15 — Migration matrix regression

Status: ACTIVE CONTINUOUS RISK

P3 introduced migration `008_p3_group_authority.sql` and updated migration checks. Historical accepted schema upgrades must continue to remain data-preserving, not merely schema-creating.

## R16 — P3 durable-memory / authority drift

Status: CORRECTIVE GOVERNANCE WAVE IN PROGRESS

Observed defect:
- P3 source advanced while root `AGENTS.md`, execution state, and commit ledger still described P2;
- no repository-local KIRION `.forge` memory existed.

Impact:
- a fresh agent could not reconstruct current P3 authority without conversation replay;
- implementation could drift from accepted phase boundaries;
- validation/promotion claims could become ambiguous.

Required:
- maintain `.forge/AUTHORITY.md`, `.forge/SSOT_CURRENT.md`, `.forge/NEST.md`, engineering log, active handoff, execution state, ledger, and risks as durable project memory;
- always verify observable Git before substantial mutation.

## R17 — Group ownership lifecycle policy

Status: POLICY BOUNDARY TO PRESERVE

Current P3 source keeps creator OWNER immutable through ordinary remove/role-mutation paths. Do not invent owner transfer, owner removal, or multi-owner semantics unless the Technical Authority/phase contract explicitly adds them.

## R18 — Group history visibility semantics

Status: POLICY BOUNDARY TO PRESERVE

Current phase contract requires active members to be authorized for group history but does not establish "history only since join" semantics. Do not silently add temporal visibility restrictions without an explicit product decision.

## Evidence note

This risk reconciliation is based on current repository source inspection and Git observation. It is not a substitute for executable P3 validation.
