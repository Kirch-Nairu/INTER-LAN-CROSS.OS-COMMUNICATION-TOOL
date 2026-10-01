# CRM Sprint 001 — Writer A Authority Decisions

Date: 2026-10-01
Lane: CRM Authority Core
Branch: `KIRCH-INTERLAN-CRM-S001-A-AUTHORITY-CORE`

This log records only architecture decisions that materially govern the Writer A authority lane.

## 1. CRM roles extend existing authenticated identity

CRM does not introduce a parallel login or credential system. Existing INTER-LAN bearer sessions remain the authentication boundary. CRM role grants are additive authority records over existing users. The legacy server `OWNER` role maps to CRM `TECHNICAL_LEAD` at the CRM server authorization boundary; non-owner users must hold an active CRM role grant before a CRM capability is available.

This keeps authentication ownership unchanged while allowing narrower CRM capabilities such as implementation, CI validation, QA validation, observation, and Lead-only disposition.

## 2. Candidate history, validation, acceptance, and audit are append-oriented evidence

Candidate submissions are immutable sequence records with exact source and candidate SHAs. CI/QA records carry both candidate ID and exact candidate SHA, and gate progression queries only PASS evidence for the current candidate. Validation from an older or repaired SHA is not inherited.

Lead acceptance is represented separately from workflow state through an explicit accepted-SHA record. `LEAD_ACCEPTED` therefore does not itself authorize integration; `INTEGRATION_AUTHORIZED` additionally requires persisted accepted-SHA evidence for the current candidate.

CRM audit records are appended in the same SQLite transaction as the authority mutation they describe.

## 3. Durable workflow mutations use optimistic authority versions

Each work package carries a monotonically increasing `version`. Durable transitions, candidate submission, validation recording, and accepted-SHA recording require the caller's expected version and fail closed when another authority mutation has already advanced the package.

This prevents stale clients or concurrent operators from silently overwriting workflow authority.

## 4. Work-package JSON import is declarative, data-only, and DRAFT-only

The supported schema is `interlan.work-package.v1`. Parsing rejects unknown fields, excessive payload size/depth, comments/trailing syntax, and executable or secret-bearing property names. Import performs semantic resolution against existing projects, features, users, CRM roles, layer compatibility, scope rules, and exact Git SHA formatting.

The normalized semantic payload receives a deterministic SHA-256 fingerprint. Raw imported JSON is not persisted. The persisted receipt contains the fingerprint, source method, schema, warnings, and result metadata.

A successful import is inserted atomically as `DRAFT`. Import has no parameter or code path that can create `AUTHORIZED` or any later state. A separate Technical Lead authorization transition is always required.

## 5. Source verification is an authorization prerequisite, not an import prerequisite

A work package may be drafted or imported with `source.verified = false` so incomplete planning data can be reviewed. That condition is surfaced as a warning. Both the in-memory aggregate and durable transition engine reject `DRAFT -> AUTHORIZED` while the source identity remains unverified.

This allows planning without converting uncertain Git identity into executable authority.
