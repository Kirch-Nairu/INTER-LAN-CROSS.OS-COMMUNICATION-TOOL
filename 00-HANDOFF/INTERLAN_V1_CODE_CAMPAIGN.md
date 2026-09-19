# INTER-LAN V1 CODE CAMPAIGN

Technical Authority: **Kirch Ivan Balite**

Repository: `Kirch-Nairu/INTER-LAN-CROSS.OS-COMMUNICATION-TOOL`

This document is the execution authority for the code-first V1 campaign from P2 through P7.

It supersedes stale phase wording in older repository documents where those documents conflict with the active branch, accepted predecessor, or this campaign.

## 1. Operating model

The program runs in this order:

```text
CODE CORRECTNESS
→ LOCAL PROOF
→ ATOMIC COMMIT
→ NEXT SLICE
→ PHASE ACCEPTANCE
→ NORMAL MERGE
→ NEXT PHASE
→ P7 CODE FREEZE
→ WHOLE-SUITE CI/CD WAVE
→ RC VALIDATION
→ RELEASE
```

### Hard rules

- Primary objective: complete INTER-LAN V1 code before investing in new CI/CD architecture.
- P2 through P7 target **300+ meaningful atomic commits per phase**.
- Empty commits, whitespace-only farming, meaningless renames, and artificial contribution padding are forbidden.
- Normal merges only from P2 forward. Do not squash accepted phase history.
- Do not force-push accepted/promoted branches.
- Security, authorization, persistence, migration, and data-integrity regressions stop forward work and are fixed immediately.
- Tests and executable local verification are code and are implemented during each phase.
- GitHub Actions orchestration, release workflows, deployment automation, signing automation, and branch-protection engineering are deferred until the final CI/CD wave.
- Existing workflows may run passively. If they expose a real source-code portability/compiler defect, fix the source. Do not redesign workflow infrastructure during the code campaign.
- No subsystem is called complete without executable proof.

## 2. Historical phases

P0 and P1 are already accepted and squash-merged. Their old granular branch history is not counted as preserved final-lineage phase commits.

Accepted main predecessor for the active campaign:

`main@91c010dd3bd448fc9bd5ec9159e94262569e3e79`

## 3. Active campaign phases

### P2 — Direct messaging + durable pairing

Scope:
- authenticated user directory;
- canonical 1:1 conversations;
- durable ordered messages;
- idempotent client message IDs;
- replies;
- delivered/read receipts with real recipient semantics;
- history pagination and cursor catch-up;
- SignalR delivery;
- offline/reconnect behavior;
- persistent pairing;
- device credentials;
- device-session renewal;
- certificate pinning;
- revocation;
- multi-device identity policy;
- credential lifecycle/rotation;
- concurrency behavior;
- SQLite locking baseline;
- typed client access path.

P2 may not close until:
- message history survives restart;
- pairing survives restart;
- paired devices renew sessions without re-enrollment;
- revoked devices cannot renew or continue receiving realtime data;
- server stores no plaintext device credential;
- client pairing state is protected according to platform capability;
- certificate pinning is enforced for native clients;
- unauthorized users cannot read/write other DMs;
- duplicate sends do not duplicate persistence;
- realtime broadcast follows durable persistence;
- offline catch-up is deterministic;
- receipt semantics are correct;
- concurrent sends and conversation creation do not corrupt authority;
- local Windows proof is clean;
- source remains portable by design.

### P3 — Group chat

Scope:
- group metadata;
- OWNER / ADMIN / MEMBER authority;
- membership lifecycle;
- add/remove/promote/demote rules;
- durable system events;
- group message stream;
- receipts;
- ordered history;
- cursor catch-up;
- SignalR delivery;
- immediate removal/revocation effect;
- audit trail.

Exit conditions:
- creator is OWNER;
- non-members cannot read/send;
- unauthorized members cannot mutate authority;
- removed users lose HTTP and realtime access immediately;
- group messages persist before broadcast;
- duplicate message IDs remain idempotent;
- history survives restart;
- realtime targets current membership only;
- concurrency and IDOR tests pass.

### P4 — File hosting

Scope:
- authenticated uploads/downloads;
- membership authorization;
- sanitized filenames;
- MIME and size policy;
- quota model;
- streaming SHA-256;
- safe staging;
- content-addressed local storage;
- durable attachment records;
- message/group association;
- authorized download;
- cleanup/recovery;
- filesystem portability.

Exit conditions:
- no path traversal or arbitrary path exposure;
- unauthorized file access fails;
- hash and byte count are deterministic;
- metadata survives restart;
- interrupted writes recover cleanly;
- duplicate-content behavior is defined;
- portable behavior is proven for Windows/Linux/macOS filesystem semantics where practical.

### P5 — Telegram archival + restore

Telegram is archive storage only, never realtime transport.

Required state machine:

```text
LOCAL
→ PENDING
→ ARCHIVING
→ ARCHIVED

or

FAILED
→ RETRY
```

Scope:
- server-only bot credential;
- destination validation;
- durable archive jobs;
- idempotency;
- retry/backoff;
- crash recovery;
- Telegram metadata;
- checksum verification;
- restore;
- archive loss/tamper behavior;
- secret-safe diagnostics.

Mandatory suite uses a deterministic Telegram abstraction/fake. Real Telegram is an optional secret-enabled staging proof, not a mandatory ordinary suite dependency.

### P6 — Usable native/web clients + product composition

Native:
- first-run server/client choice;
- embedded/reusable owner server host;
- discovery/manual connect;
- fingerprint confirmation;
- enrollment/approval;
- persistent pairing;
- session renewal;
- DM/groups/files;
- connection state;
- unread state;
- settings;
- unpair/logout;
- owner/admin controls.

Web/Android:
- same authorization/contracts;
- browser-appropriate pairing/session model;
- DM/groups/files;
- responsive touch-first UX;
- reconnect/error recovery;
- no false native-style certificate-pinning claims.

P6 also defines deterministic local product composition and publish contracts. Packaging automation is deferred, but packaging behavior is not.

### P7 — Hardening / recovery / release-candidate code

Attack:
- bootstrap races;
- invite replay/brute force;
- enrollment replay;
- credential theft/revocation;
- role escalation;
- DM/group/file IDOR;
- malformed realtime payloads;
- oversized payloads;
- Unicode/path abuse;
- archive duplication;
- Telegram outage;
- SQLite restart/locking/corruption handling;
- interrupted uploads;
- reconnect storms;
- concurrent message/file traffic;
- corrupt pairing state;
- certificate mismatch;
- migration upgrades;
- secret/log leakage;
- denial-of-service bounds.

Exit:
- zero known critical/high authorization defects;
- restart/recovery suite passes;
- migration matrix passes;
- backup/restore is proven;
- DM/group/file persistence is proven;
- Telegram outage/recovery behavior is proven;
- native and web clients are usable without PowerShell/API calls;
- known limitations are documented;
- codebase is frozen for CI/CD engineering.

## 4. Integration spine required during code campaign

The following are NOT deferred to the final CI/CD wave:

- canonical configuration authority;
- reusable owner ServerHost lifecycle;
- typed client SDK/session layer;
- shared process/network test harness;
- aggregate local verification entrypoint;
- deterministic migrations;
- dependency locking;
- local publish composition;
- cross-platform-safe filesystem behavior;
- secure-storage platform policy.

The intended runtime shape is:

```text
InterLan.Desktop
├─ SERVER OWNER MODE
│  └─ reusable InterLan ServerHost lifecycle
└─ CLIENT MODE
   └─ typed InterLan client/session layer
```

The owner desktop application must eventually own the canonical server lifecycle. A separate manually-started server executable alone does not satisfy V1.

## 5. Configuration authority

The code campaign must converge configuration into one explicit precedence model.

Target:

```text
persisted canonical server settings
        ↓
runtime settings service
        ↓
Kestrel / discovery / storage / archive / clients
```

Environment variables are explicit test/operator overrides, not an accidental second source of truth.

Do not allow SQLite server_settings, runtime.json, and IConfiguration to diverge silently.

## 6. Verification cadence

Every commit:
- affected code compiles or focused proof passes.

Approximately every 25 meaningful commits:
- phase-local checkpoint suite.

Approximately every 50:
- restart / persistence / migration replay.

Approximately every 100:
- architecture and invariant review.

At phase completion:
- full phase-local acceptance;
- review;
- normal merge preserving all commits.

## 7. Final CI/CD boundary

Do not create new P3–P7 phase workflow architecture.

After P7 freeze, consolidate existing automation into one whole-suite dependency graph consuming repository-local commands.

The final wave covers:
- restore/build;
- aggregate verification;
- Windows/Linux/macOS;
- web build;
- publish composition;
- native artifacts;
- checksums;
- release manifest/versioning;
- signing/notarization where applicable;
- GitHub release;
- required checks/branch protection;
- optional secret-enabled Telegram proof;
- final RC/tag workflow.

See `00-HANDOFF/INTERLAN_FINAL_CICD_CONTRACT.md`.

## 8. Failure policy

```text
security / authorization / persistence / migration defect
→ stop feature expansion
→ reproduce
→ regression test
→ smallest fix
→ prove
→ continue

ordinary implementation defect
→ fix in current slice
→ focused proof
→ commit

workflow-only / runner-only infrastructure problem
→ record
→ defer to final CI/CD wave

source portability/compiler problem exposed by existing CI
→ fix source now
```

## 9. Commit policy

Target: 300+ meaningful commits per active phase.

Valid commit units include:
- contract;
- schema;
- migration;
- domain invariant;
- repository query;
- command;
- authorization guard;
- API route;
- realtime event;
- client state;
- validation;
- error mapping;
- regression test;
- restart test;
- concurrency test;
- documentation;
- justified refactor.

The technical completion gate always has priority over the counter.
