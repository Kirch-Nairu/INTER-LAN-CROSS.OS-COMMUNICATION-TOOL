# INTER-LAN Developer Operations Control Plane — Product Architecture

Status: design specification for the CRM rebaseline branch.

## 1. Product objective

INTER-LAN becomes a local-first engineering delivery system for real staff. Its central question is:

> What exact code state is authorized, being implemented, under validation, rejected, accepted, integrated or frozen; who owns it; and what evidence proves that state?

The product is not a generic task tracker and not a generic CRM.

## 2. Canonical topology

```text
                    DEBIAN KDE HOST
             Technical Lead / Authority Machine

     Native Technical Lead Control Application
                     |
                     v
             Local Control Services
                     |
        +------------+-------------+
        |                          |
        v                          v
   SQLite / Evidence          Web Gateway
   / Archive / Audit              |
                                  v
                           Cloudflare Tunnel
                                  |
                     +------------+------------+
                     |            |            |
                     v            v            v
                 Developer      CI/CD          QA
                 Web Client     Client         Client
```

The Technical Lead desktop is a real native application. Browser clients are subordinate staff clients.

## 3. Native Technical Lead experience

Target visual language:

- dark navy/blue technical command-center aesthetic;
- cobalt/electric-blue accent;
- dense but readable information;
- native KDE-friendly windowing;
- keyboard-first workflows;
- command palette;
- timeline and pipeline visualizations;
- KDE desktop notifications;
- no browser chrome for the Lead control surface.

Primary native sections:

- Command Center
- Projects
- Delivery Pipeline
- Work Packages
- People
- Candidates
- CI/CD
- QA
- Findings
- Rework
- Regressions
- Requests
- Artifacts
- Contributions
- Audit
- Administration
- Remote Access / Tunnel

The initial dashboard should prioritize delivery state, not employee-directory presentation.

Example summary metrics:

```text
ACTIVE DEVELOPMENT
WAITING CI/CD
WAITING QA
RETURNED FOR REWORK
LEAD ACCEPTANCE
PHASES CLOSED
OPEN REGRESSIONS
BLOCKING FINDINGS
```

## 4. Work Package as aggregate root

The Work Package is the primary operational aggregate.

Representative identity:

```text
CAL-A03-F
Project: ONE TALIBON
Feature: Employee Calendar
Phase: A03
Layer: Frontend
Technical Lead: Kirch Ivan Balite
Assigned Developer: AJ Rienze
Source SHA: ...
Current Candidate SHA: ...
Status: QA_FAILED / REWORK_REQUIRED
```

Expected work-package views:

- Overview
- Requirements
- Acceptance Criteria
- Files
- Commits
- Candidates
- CI/CD
- QA
- Findings
- Rework
- Regressions
- Requests
- Notes
- Screenshots / Evidence
- Artifacts
- Audit

## 5. Core domain model

```text
Project
  Feature
    WorkPackage
      Requirements
      AcceptanceCriteria
      Assignments
      CandidateSubmissions
        Commits
        ChangedFiles
        CIValidation
        QAValidation
        Findings
        Evidence
      ReworkCycles
      Regressions
      Requests
      Notes
      Decisions
      PhaseClosure
      ArtifactBundle

StaffMember
  Roles
  Assignments
  AcceptedContributions
  FindingsOwned
  Requests

AuditEvent
  Actor
  Time
  Aggregate
  EventType
  Before/After references
```

## 6. Candidate model

Candidate records are immutable historical evidence.

Example:

```text
Candidate 001
SHA: aaa111
CI: PASS
QA: FAIL

Candidate 002
SHA: bbb222
CI: PASS
QA: PASS
Lead: REWORK

Candidate 003
SHA: ccc333
CI: PASS
QA: PASS
Lead: ACCEPT
Accepted SHA: ccc333
```

A later candidate never overwrites a prior candidate or inherits prior PASS results.

## 7. Candidate submission template

A developer submission should capture at minimum:

- branch;
- candidate SHA;
- source SHA;
- commit count;
- commit range;
- changed-file list;
- implementation summary;
- self-validation performed;
- known limitations;
- notes to validator;
- evidence attachments where required.

The system should freeze the submitted candidate record and notify the next gate owner.

## 8. File-level evidence

Every candidate should retain file-level evidence where available:

- repository-relative path;
- change type;
- added lines;
- deleted lines;
- first commit touching the file in the candidate range;
- last commit touching the file in the candidate range;
- owning layer/work package.

Git-derived evidence is preferred. Manual entry is fallback only.

## 9. Staff profiles and contribution ledger

Profiles should show:

- current assignments;
- role and specialization;
- accepted work packages;
- closed phases;
- accepted commits;
- accepted files/footprint;
- recent delivery history;
- rework and regression history when evidence supports linkage.

Raw commit count is informational, not a standalone performance score.

## 10. Findings

Findings are first-class records, not free-form comments.

Minimum fields:

- finding ID;
- candidate SHA;
- gate;
- reporter;
- severity;
- type;
- expected behavior;
- observed behavior;
- reproducible steps;
- evidence;
- responsible owner;
- disposition;
- status;
- resolution candidate.

A fixed finding that changes code creates a new candidate and resets required downstream validation according to policy.

## 11. Rework vs regression

Rework:

> A candidate fails before acceptance and returns to an owner for correction.

Regression:

> Previously accepted behavior is shown to be broken by a later code state.

Regression records should connect:

- affected closed package;
- previously accepted SHA;
- behavior previously proven;
- later candidate where the regression is detected;
- responsible follow-up package;
- severity/blocking state.

## 12. Structured requests

Supported request categories should include:

- Clarification Request
- Backend Contract Request
- API Change Request
- Data Requirement
- QA Clarification
- Environment Request
- Dependency Request
- Architecture Decision
- Reopen Request
- Revision Request
- Access Request

Requests may be blocking or non-blocking and must have explicit sender, recipient/owner, work package, status, timestamps and resolution notes.

## 13. Closed history and revisions

Closing a phase must create an immutable historical record.

A later modification must create a revision package rather than mutate the original closure.

Example:

```text
Original: CAL-A03-F
Revision: CAL-A03-F-R01
Parent: CAL-A03-F
Reason: Change request
Reference artifact: CAL-A03-F-CLOSURE-BUNDLE.zip
```

## 14. Role model

Initial roles/capabilities:

- **Technical Lead** — authorize, assign, disposition, integrate, freeze, close, reopen/revise by explicit policy, manage product configuration;
- **Backend Developer** — assigned backend packages and submissions;
- **Frontend Developer** — assigned frontend packages and submissions;
- **CI/CD Validator** — exact-candidate technical validation and findings;
- **QA** — functional acceptance validation and findings;
- **Observer** — read-only visibility;
- **System** — automated state transitions, notifications, evidence generation and artifact generation.

Authorization should be capability/policy based, not a single generic admin flag.

## 15. Developer web client

The developer browser surface should remain intentionally focused.

Primary screens/actions:

- My Work
- Work Package Detail
- Submit Candidate
- Respond to Finding
- Request Clarification
- Upload Evidence
- View Validation State
- View Closed Phase
- Download Closure Artifact

Do not mirror every Lead administration screen into the web client.

## 16. Native Lead command center

The Lead view should support rapid operational triage:

- package counts by gate;
- blocking findings;
- waiting approvals;
- staff blockers;
- candidate and SHA identity;
- exact owner;
- validation state;
- pending requests;
- regression alerts;
- tunnel status;
- recent audit events.

## 17. Notifications

Representative routing:

- Developer submits candidate -> CI/CD owner notified.
- CI PASS -> QA owner notified.
- CI FAIL -> responsible implementation owner notified.
- QA FAIL -> responsible implementation owner notified with findings.
- QA PASS -> Technical Lead notified.
- Lead ACCEPT -> integration/closure workflow becomes available.
- Phase CLOSED -> closure bundle generated and relevant staff notified.

Realtime delivery should be used for convenience, but canonical state remains persisted server-side.

## 18. Audit

Every authority-significant action must generate an append-only audit event.

Examples:

- work package drafted;
- package authorized;
- assignment accepted;
- candidate submitted;
- CI result recorded;
- QA result recorded;
- finding created/resolved;
- rework issued;
- Lead disposition issued;
- accepted SHA recorded;
- integration recorded;
- phase frozen;
- closure bundle generated;
- revision package opened;
- tunnel started/stopped;
- import accepted/rejected.

Audit is historical reconstruction, not ordinary application logging.

## 19. Security boundary

The public/remote browser gateway must not expose unrestricted host authority.

Technical Lead-only actions should stay on the native/local control plane where practical.

The browser gateway must enforce application authentication and authorization even when an upstream tunnel/access layer exists.

Imported JSON, uploaded screenshots and report-generation inputs are data. They do not gain shell-command authority.
