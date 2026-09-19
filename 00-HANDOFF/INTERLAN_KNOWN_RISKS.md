# INTER-LAN KNOWN RISKS AND DEBUG-HOLE REGISTER

This register keeps material risks visible without turning governance into bureaucracy.

## R1 — Desktop/server composition

P6 remains responsible for complete owner desktop/product composition. Reusable server-host foundations already exist.

## R2 — Configuration authority divergence

Keep persisted settings, explicit overrides, Kestrel, discovery, storage, and later archive configuration aligned.

## R3 — Verification topology drift

P3 verification projects are in the solution/aggregate suite. Future changes must keep executable checks buildable and reachable.

## R4 — Network-smoke lifecycle

Shared harness exists, but process/port lifecycle failures must still be classified instead of hidden.

## R5 — Realtime revocation

P3 requires current-membership targeting and per-invocation authorization. Any event path that can reach a removed member is a stop-feature-expansion defect.

## R6 — Receipt semantics

Group receipts must preserve recipient-only delivered/read semantics and current membership authority.

## R7 — SQLite concurrency and group authority races

**SOURCE HARDENED — EXECUTABLE VERIFICATION PENDING**

Attack:
- concurrent membership add/restore;
- remove vs role mutation;
- remove vs send;
- duplicate group send;
- concurrent receipt upserts;
- history reads during writes;
- admin metadata mutation vs removal;
- revocation-raced authorized reads vs messages/events/receipts committed later.

Source mitigation through `ea5729ada72064d3770cb4da64d56c539227eda8`:
- group mutations acquire non-deferred SQLite write transactions before authority reads;
- receipt writes share the same authority transaction;
- post-commit realtime delivery targets do not re-authorize the already-committed actor;
- concurrency checks cover add/restore, remove-vs-send, remove-vs-role, and remove-vs-receipt;
- create/add active-user checks occur inside serialized writes;
- create/update responses are materialized before commit, so an authority change cannot cause a false post-commit failure;
- authorized directory/detail/history/message/receipt/event/typing-target reads use one read snapshot across authorization and projection;
- concurrency regressions cover remove-vs-metadata and revocation-raced reads, including exclusion of post-removal sentinel messages;
- restart history verification accepts either valid remove-vs-send serialization outcome without weakening persistence checks.

Required remaining evidence:
- executable P3 concurrency run;
- realtime smoke after the fan-out change;
- no duplicate authority;
- no stale authorization;
- no uncaught lock race treated as success;
- deterministic final state.

## R8 — Web/product composition

Deferred to P6. Final product output must not omit browser assets.

## R9 — Dependency reproducibility

Maintain deterministic repository-native dependency policy. Workflow consolidation belongs after P7 freeze.

## R10 — Workflow sediment

Do not add P3–P7 workflow architecture. Existing Actions are passive signals only.

## R11 — Telegram nondeterminism

Future P5 mandatory proof uses a deterministic archive abstraction/fake. Live Telegram remains optional staging evidence.

## R12 — Filesystem portability

Future P4/P7 risk: path rules, Unicode, case sensitivity, locking, permissions, atomic replacement.

## R13 — Unix pairing-state protection

Platform secure-storage policy remains open for final native acceptance.

## R14 — Migration matrix regression

**SOURCE COVERAGE STRENGTHENED — EXECUTABLE VERIFICATION PENDING**

P3 adds migration `008_p3_group_authority.sql`. Historical upgrades must remain data-preserving, not just schema-creating.

The migration check now seeds a migration-007 database with P2 direct conversation memberships, a message, receipt state, conversation preference, device credential metadata, and an existing group membership, then requires every seeded value to remain present after migration 008. The executable migration gate is still NOT RUN in this environment.

## R15 — Group ownership policy boundary

Current P3 keeps creator OWNER immutable through ordinary remove/role mutation. Do not invent owner transfer/removal/multi-owner semantics without an explicit product decision.

## R16 — Group history visibility boundary

Current contract authorizes current active members to read group history. No "history only since join" rule exists. Do not invent one silently.

## R17 — Group IDOR / audit evidence

**SOURCE HARDENED — EXECUTABLE VERIFICATION PENDING**

Current P3 source checks cover:
- true non-member denial for group detail/history/message/receipt/event/send surfaces;
- cross-group message IDs rejected for detail, receipts, edit, delete, reply, forward cursor, and recent cursor;
- HTTP 403/404 route isolation for representative non-member and cross-group message cases;
- membership audit payloads that identify the target user directly.

The core/realtime executable gates remain NOT RUN here.

## R18 — Governance drift

**CORRECTED IN PRODUCT-REPO STRUCTURE; CONTINUITY MUST STAY CURRENT**

A P3 implementation wave advanced while old P2 state files remained stale. A subsequent correction mistakenly imported `.forge/` product memory from the wrong Forge repository.

Correct model:
- full doctrine remains in `Operation-FORGE.kirion`;
- INTER-LAN keeps a thin active instruction layer, lane record, and handoffs;
- repo truth outranks memory;
- old conversation summaries are never Git authority.

## Evidence note

Risk status is based on current source/Git inspection unless a specific executable gate is recorded.
