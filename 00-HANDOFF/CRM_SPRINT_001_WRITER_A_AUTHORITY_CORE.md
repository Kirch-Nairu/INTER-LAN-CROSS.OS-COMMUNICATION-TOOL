# KIRION FORGE — CODE WRITER A — CRM SPRINT 001 AUTHORITY CORE

Technical Authority: Kirch Ivan Balite

Target product: INTER-LAN Developer Operations Control Plane

Execution mode: CODE-FOCUSED SPRINT

Commit target: 40 substantive atomic commits. No padding, empty commits, formatting-only churn, fake refactors, or documentation farming.

## Source and branch

Common repaired source candidate:

`b0bed39e4f1b7bfeb17a65d85383362e74f65b35`

This source contains the bounded bootstrap repair for the previously malformed `MessageTextPolicy.cs` control-character literals. The repaired blob bytes were independently re-read after mutation. The staging Maintainer did **not** obtain a full runtime/Actions proof for this exact commit, so Writer A MUST establish the Release build and aggregate SuiteChecks before any CRM product mutation. If that preflight fails, stop and return the exact blocker rather than coding around it.

Writer branch:

`KIRCH-INTERLAN-CRM-S001-A-AUTHORITY-CORE`

Do not begin from old P2 branches or from `main` directly.

## Read first

1. `AGENTS.md`
2. `00-HANDOFF/CRM_F00_PRODUCT_REBASELINE.md`
3. `docs/CRM_PRODUCT_ARCHITECTURE.md`
4. `docs/CRM_WORK_PACKAGE_JSON_IMPORT.md`
5. `docs/CRM_PHASE_CLOSURE_EVIDENCE.md`
6. this handoff

## Sprint mission

Build the first authoritative CRM backend vertical slice around the Work Package.

The Work Package is the central operational object. This sprint must turn the documented delivery doctrine into executable domain policy and durable local state.

Primary capability goals:

- projects and features;
- staff identities/roles needed by CRM authority;
- work-package identity and ownership;
- required lifecycle/state machine;
- transition policy enforcement;
- assignments;
- requirements/deliverables/acceptance criteria;
- exact source/candidate/accepted SHA records;
- immutable candidate submissions;
- audit events for authoritative transitions;
- declarative ChatGPT/AI JSON work-package import into DRAFT only;
- schema and semantic validation of imports;
- explicit Technical Lead authorization transition;
- persistence and migration support in SQLite;
- role-limited server contracts/endpoints for the above;
- executable negative tests proving forbidden transitions fail closed.

Do not attempt the entire future CRM in this sprint. Closure report generation, full evidence bundles, contribution ledger, advanced regressions and full CI/QA UX are later lanes unless a minimal type/foreign-key is required now.

## Owned scope

Writer A owns CRM-specific code under:

- `src/InterLan.Domain/Crm/**`
- `src/InterLan.Contracts/Crm/**`
- `src/InterLan.Infrastructure/Crm/**`
- `src/InterLan.Server/Crm/**`

Writer A may modify existing CRM-adjacent shared composition files only when required to wire the owned CRM implementation into the existing application, including migrations/endpoint registration. Keep shared-file edits minimal and explain them in the decision log.

Writer A may add focused executable checks under:

- `tools/InterLan.CrmAuthorityChecks/**`
- `tools/InterLan.CrmImportChecks/**`

Writer A owns the sprint decision log:

`docs/decisions/CRM_SPRINT_001_A_DECISIONS.md`

## Decision-log rule

Code remains the focus.

Update the decision log only when a material decision is made that future maintainers need to understand, such as:

- state-machine invariant;
- authority boundary;
- persistence shape that constrains later work;
- JSON-import trust boundary;
- irreversible schema choice;
- compatibility decision with reused P2 infrastructure.

Do not create documentation-only progress commits. Decision-log changes should normally ride with the code commit that implements the decision.

Every entry should contain:

- date/commit;
- decision;
- reason;
- alternatives rejected;
- compatibility/migration consequence;
- follow-up if any.

## Prohibited scope

Do not redesign:

- `web-client/**`
- the visual/native desktop control plane;
- Cloudflare tunnel orchestration;
- Linux host CLI lifecycle;
- unrelated messaging UI;
- GitHub Actions architecture;
- release/deployment/signing;
- historical closure/report rendering beyond minimal domain placeholders;
- old P2 functionality unless adaptation is necessary for the CRM authority slice.

Do not merge, promote, deploy or self-accept.

## Required invariants

- imports never execute shell commands;
- imports never auto-authorize work;
- imported packages are DRAFT until explicit authorized transition;
- workflow states are policy-controlled, not arbitrary strings;
- QA PASS cannot exist for a candidate lacking required same-candidate CI PASS;
- repaired/new candidate cannot inherit prior candidate PASS state;
- accepted SHA identity is explicit;
- candidate history is append-oriented/immutable from normal application paths;
- authoritative mutations emit durable audit events;
- role/ownership checks fail closed;
- SQLite migrations are deterministic and restart-safe;
- no secrets are stored in work-package/import payloads.

## Commit progression

Commits 1-10: domain vocabulary, IDs, roles/capabilities, work-package aggregate, state machine and policy tests.

Commits 11-20: SQLite schema/migrations/repositories, transactional mutation/audit behavior and restart/migration checks.

Commits 21-30: candidate/SHA evidence, assignments/criteria, JSON schema/import validation, DRAFT creation and authorization rules.

Commits 31-40: server contracts/endpoints, negative authorization/transition cases, concurrency/idempotency boundaries, aggregate executable proof and integration hardening.

If the authorized scope is genuinely complete before 40 meaningful commits, STOP rather than fabricate commits.

## Validation

Before first product mutation, independently run the repaired source candidate:

```bash
dotnet build InterLan.sln -c Release
dotnet run --project tools/InterLan.SuiteChecks/InterLan.SuiteChecks.csproj -c Release --no-build
```

Then maintain focused executable proof for the CRM authority/import slice. Run the full aggregate suite at substantive checkpoints near commits 10, 20, 30 and final.

Any pre-existing failure not attributable to this branch must be reported precisely. Do not silently repair unrelated code outside authority.

## Return

Return:

- exact starting SHA;
- exact final SHA;
- exact substantive commit count;
- files/projects changed;
- implemented authority invariants;
- migrations introduced;
- JSON import behavior;
- validation commands and results;
- major decisions recorded;
- known limitations/dependencies;
- `READY FOR INDEPENDENT REVIEW` or exact BLOCKED disposition.
