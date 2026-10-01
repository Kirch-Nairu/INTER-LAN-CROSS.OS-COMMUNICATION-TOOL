# CRM-F00 — INTER-LAN Developer Operations Control Plane Rebaseline

Technical Authority: **Kirch Ivan Balite**

Status: **PRODUCT REBASELINE / DOCUMENTATION ONLY**

Rebaseline source:

`main@d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`

Rebaseline branch:

`KIRCH-INTERLAN-DEVELOPER-OPS-CRM-REBASELINE`

## 1. Product-direction decision

INTER-LAN is being reoriented from a messaging-first cross-OS communication product into a **local-first Developer Operations Control Plane / Development CRM** for real engineering staff.

The previous communication stack is not discarded wholesale. Reusable infrastructure should be preserved where it still serves the new product:

- local ASP.NET Core hosting;
- native desktop composition;
- browser-client delivery;
- authentication, sessions, device identity and enrollment;
- realtime notifications;
- SQLite persistence and migrations;
- TLS and local-network transport;
- cross-platform validation infrastructure.

Messaging-specific product semantics are no longer the primary domain. They may survive as comments, findings discussion, requests, notifications and handoff communication.

## 2. Canonical operating environment

The Technical Lead is transitioning to Linux. The **Debian KDE workstation is both the Technical Lead's primary day-to-day development machine and the canonical INTER-LAN host machine**.

This is not a secondary server hidden elsewhere. The host is the Technical Lead's main workstation.

Design consequences:

- Linux is a first-class and primary runtime, not an afterthought;
- the Technical Lead control surface is a native desktop application, not a wrapped browser app;
- system lifecycle must behave cleanly on Debian/KDE;
- local filesystem, secret storage, process supervision, notifications, permissions and shutdown behavior must be designed for Linux first;
- Windows and other clients remain important for validation and staff access, but they are not the canonical host authority;
- server data, artifacts, closure reports and audit evidence live on the Technical Lead host unless explicitly exported/backed up.

## 3. Product identity

Working product identity:

**INTER-LAN — Developer Operations Control Plane**

Purpose:

> Track the exact engineering state of every authorized work package: what is approved, who owns it, which source/candidate/accepted SHA is authoritative, what evidence exists, which gate currently owns the candidate, what failed, what was repaired, what was finally integrated, and what immutable closure artifact proves completion.

This is not intended to become a generic Jira clone or a generic employee CRM.

The central unit of work is the **Work Package**.

## 4. Governing delivery model

The product must encode the ONE TALIBON delivery lifecycle as executable workflow rather than documentation-only guidance.

Canonical roles:

- Technical Lead / Backend — Kirch Ivan Balite
- Frontend Development — AJ Rienze / Jhayr
- CI/CD & Technical Validation — Kurt Shine Khasi Halasan
- Quality Assurance — Siris

Canonical delivery chain:

`Requirement / Problem -> Technical Analysis -> Authorized Work Package -> Backend Foundation -> Frontend Implementation -> CI/CD -> QA -> Technical Lead Acceptance -> Integration -> Phase Freeze -> Closure -> Next Authorized Phase`

Core laws:

1. Authority before implementation.
2. Backend first where required.
3. Validation belongs to an exact candidate SHA.
4. Any source change creates a new candidate requiring revalidation.
5. Implementation, CI/CD, QA and final lead acceptance are distinct gates.
6. A discovering role does not automatically become the fixing role.
7. Only accepted work is integrated.
8. Closed phases are immutable historical records; later changes create revision work packages rather than rewriting closure history.

## 5. Required workflow state machine

Minimum work-package lifecycle:

- `DRAFT`
- `AUTHORIZED`
- `IMPLEMENTING`
- `CANDIDATE_SUBMITTED`
- `CI_PENDING`
- `CI_RUNNING`
- `CI_FAILED`
- `CI_PASSED`
- `QA_PENDING`
- `QA_RUNNING`
- `QA_FAILED`
- `QA_PASSED`
- `LEAD_REVIEW`
- `LEAD_REWORK`
- `LEAD_ACCEPTED`
- `INTEGRATION_AUTHORIZED`
- `INTEGRATED`
- `PHASE_FROZEN`
- `CLOSED`

Transitions must be enforced by policy, not stored as arbitrary strings.

Examples:

- QA cannot issue PASS against a candidate without the required CI PASS for that same candidate.
- A repaired candidate cannot inherit an earlier candidate's CI or QA PASS.
- Closure is blocked without required candidate identity, accepted SHA, validation evidence, screenshots/evidence where required, Technical Lead disposition, integration record and closure notes.

## 6. Major subsystems

The target product should be decomposed conceptually into:

- **CONTROL** — native Technical Lead desktop client;
- **GATEWAY** — browser client for developers, CI/CD, QA and observers;
- **AUTHORITY** — roles, capabilities, workflow transitions and ownership;
- **EVIDENCE** — candidate, commit, file, screenshot and validation evidence;
- **FORGE** — declarative JSON work-package import;
- **VALIDATION** — CI/CD and QA records/gates;
- **ROUTER** — findings, rework, requests, regressions and responsible-owner routing;
- **LEDGER** — developer contribution and accepted-delivery history;
- **ARCHIVE** — closure reports and immutable phase bundles;
- **TUNNEL** — Cloudflare tunnel lifecycle for browser clients;
- **AUDIT** — append-only operational history.

## 7. Work-package record minimum

Every work package must retain at least:

- project;
- feature;
- operational/problem statement;
- authorized technical solution;
- work-package ID;
- phase;
- layer;
- Technical Lead;
- assigned owner(s);
- repository;
- source branch and source SHA;
- candidate branch and candidate SHA(s);
- accepted SHA;
- requirements;
- deliverables;
- acceptance criteria;
- owned files/scope;
- prohibited files/scope;
- required self-validation;
- commit count and commit range;
- exact files changed and file-level footprint;
- CI/CD result and findings;
- QA result and findings;
- rework history;
- requests/clarifications;
- regression history;
- Technical Lead disposition;
- integration/freeze state;
- contribution credit;
- screenshots/evidence;
- closure notes;
- completion date;
- generated closure artifacts;
- complete audit timeline.

## 8. Developer contribution model

Raw commit count is recorded but must not be treated as the sole productivity metric.

Contribution records should emphasize accepted engineering output:

- accepted work packages;
- accepted candidate commits;
- accepted file footprint;
- features delivered;
- rework cycles;
- regressions introduced/resolved where evidence supports attribution;
- closure participation;
- final accepted state.

## 9. Failure routing

Failure categories must route to the responsible owner according to the operating model.

Examples:

- backend/business-rule defect -> backend/Technical Lead owner;
- authorization/security defect -> backend/security owner;
- frontend implementation defect -> assigned frontend developer;
- CI/CD infrastructure defect -> CI/CD owner;
- QA procedure/evidence defect -> QA owner;
- cross-layer architecture defect -> Technical Lead disposition.

Rework and regression must be distinct concepts:

- **rework** = failed before acceptance;
- **regression** = previously accepted behavior later became broken.

## 10. Staff-facing product boundaries

The Technical Lead receives the full native control plane.

Browser clients should be role-focused and intentionally smaller. Developers primarily need:

- My Work;
- work-package detail;
- candidate submission;
- findings/rework response;
- clarification/request creation;
- evidence upload;
- closure history and artifact download.

CI/CD and QA receive their own gate-focused views.

Privileged operations such as final acceptance, integration authorization, phase freeze, system configuration and role management are Technical Lead authority and should not be casually exposed through the remote public gateway.

## 11. Reuse mapping from old INTER-LAN

Old capability -> new role:

- messaging -> comments, requests, findings discussion and notifications;
- realtime transport -> pipeline updates, gate changes, candidate alerts and blocker notifications;
- pairing -> staff/device enrollment;
- device identity -> trusted staff device/session;
- native owner runtime -> Technical Lead host runtime;
- web client -> developer/CI/QA gateway;
- SQLite -> canonical local-first operational database;
- test harness -> product acceptance and regression infrastructure.

## 12. Delivery roadmap

Planned implementation order:

1. `CRM-F00` — Product Rebase / architecture freeze / reusable-infrastructure inventory.
2. `CRM-A01-B` — Authority Core: users, roles, projects, features, work packages, assignments, state machine, audit.
3. `CRM-A02-B` — Candidate Evidence: source/candidate/accepted SHA records, commits, files, immutable submissions.
4. `CRM-A03-B` — Validation Gates: CI/CD, QA, findings, PASS/FAIL transitions, revalidation rules, routing.
5. `CRM-A04-N` — Native Lead Console for Debian KDE.
6. `CRM-A05-W` — Developer/CI/QA browser gateway.
7. `CRM-A06-B` — Rework, regressions, requests and escalation.
8. `CRM-A07-B` — Closure Engine and immutable artifact bundles.
9. `CRM-A08-B` — Contribution Ledger and developer profiles.
10. `CRM-A09-H` — Security, backup/restore, audit integrity, malformed-input and failure-recovery hardening.
11. `CRM-A10-P` — Linux-first productization, CLI lifecycle and tunnel orchestration.
12. `CRM-A11` — End-to-end acceptance simulation from requirement through CLOSED and later revision.

## 13. Current authority boundary

This document records product direction only.

No implementation is authorized by this file alone.

Before code mutation, CRM-F00 must inventory the existing codebase and explicitly classify components as:

- retain as-is;
- retain but adapt;
- retire;
- replace;
- unknown pending proof.

The old 4x P2 benchmark must not simply resume as if messaging remained the target product. A new green baseline and bounded CRM work packages are required before implementation resumes.
