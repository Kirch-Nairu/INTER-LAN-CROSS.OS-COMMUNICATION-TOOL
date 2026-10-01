# INTER-LAN Phase Closure, Evidence and Report Specification

Status: design specification.

## 1. Closure principle

A work package is not closed because coding stopped.

Closure requires the complete accepted delivery chain to be satisfied and recorded.

Minimum closure gate:

- implementation complete;
- exact candidate supplied;
- source SHA recorded;
- candidate SHA recorded;
- commit range/count recorded;
- changed files recorded;
- developer self-validation recorded;
- required CI/CD PASS for the exact accepted candidate;
- required QA PASS for the exact accepted candidate;
- required findings resolved or explicitly dispositioned;
- Technical Lead acceptance;
- accepted SHA recorded;
- integration record complete;
- closure notes complete;
- contribution attribution complete;
- required screenshots/evidence complete;
- closure report generated;
- closure artifact bundle generated;
- phase freeze recorded.

If a required item is missing, ordinary closure is blocked.

Exceptional override, if ever allowed, must be Technical Lead-only, explicitly reasoned and audited.

## 2. Structured screenshot evidence

Screenshots are first-class evidence records, not random image attachments.

Minimum screenshot metadata:

- Screenshot ID;
- project/work package;
- candidate SHA;
- accepted SHA when final;
- gate;
- captured by;
- capture timestamp;
- platform/OS;
- browser/client where relevant;
- viewport/resolution where relevant;
- description/purpose;
- requirement or acceptance criterion demonstrated;
- finding linkage where relevant;
- original filename;
- stored filename;
- content hash (SHA-256 or implementation-approved equivalent);
- immutable evidence storage reference.

Representative record:

```text
SCR-00192
Work Package: CAL-A04-F
Candidate: 21cab92...
Gate: QA
Captured by: Siris
Platform: Windows / Chrome
Viewport: 1920x1080
Purpose: Verify responsive calendar month view
Acceptance Criterion: AC-07
Hash: <sha256>
```

## 3. Evidence templates by package type

Screenshot/evidence requirements should be templated by work-package type.

Example frontend template:

- primary desktop view;
- responsive/mobile view where applicable;
- empty state;
- error state;
- permission-restricted state;
- final accepted behavior;
- any state explicitly required by acceptance criteria.

Example backend/security template may use non-UI evidence instead or in addition:

- API/runtime behavior;
- migration result;
- authorization rejection;
- persistence proof;
- restart/recovery proof;
- security negative case.

The system must not require meaningless screenshots solely to satisfy a checkbox. Evidence type should prove the actual criterion.

## 4. Evidence integrity

On evidence ingestion:

1. validate allowed type/size according to policy;
2. normalize safe storage filename without destroying original display name;
3. calculate content hash;
4. persist metadata;
5. bind evidence to candidate/work package/criterion/finding;
6. store immutable record or versioned replacement according to explicit policy;
7. emit audit event.

Closure bundles should include checksums for exported evidence.

## 5. Generated phase closure report

Every successfully closed phase should automatically generate a structured closure report from canonical CRM records.

Expected report title:

`<WORK-PACKAGE-ID> — PHASE CLOSURE REPORT`

Minimum sections:

1. Executive Summary
2. Work Package Identity
3. Original Operational Problem
4. Authorized Technical Solution
5. Scope and Boundaries
6. Requirements
7. Acceptance Criteria
8. Architecture / Contract Context
9. Implementation Summary
10. Source Control Evidence
11. Developer Contribution Record
12. Developer Self-Validation
13. CI/CD Technical Validation
14. QA Validation
15. Findings and Resolutions
16. Rework History
17. Regression Analysis
18. Requests / Clarifications / Decisions
19. Technical Lead Review
20. Integration Record
21. Final Accepted State
22. Screenshots and Evidence
23. Known Limitations
24. Closure Notes
25. Phase Freeze Record
26. Artifact Manifest
27. Audit Summary / Timeline

## 6. Source-control evidence section

The closure report must clearly identify:

- repository;
- source branch;
- source SHA;
- implementation/candidate branch;
- all submitted candidate SHAs;
- exact accepted SHA;
- commit range;
- accepted commit count;
- changed file list;
- final integration SHA if different from accepted candidate;
- integration relationship where known.

Do not invent Git evidence. Unknown/unverified values must be labeled accordingly.

## 7. Validation sections

CI/CD section should include:

- candidate SHA validated;
- checks executed;
- environment/runner information when relevant;
- PASS/FAIL result;
- findings;
- links/references/evidence where stored;
- timestamp;
- validating role/person.

QA section should include:

- candidate SHA validated;
- scenarios/criteria checked;
- expected/observed results;
- screenshots/evidence;
- findings;
- PASS/FAIL disposition;
- timestamp;
- QA owner.

Validation evidence applies to the exact candidate only.

## 8. Rework history

The report must preserve every meaningful candidate/rework cycle rather than showing only the successful end state.

Example:

```text
Candidate A — 111AAA
CI: PASS
QA: FAIL
Finding: FND-0012

Candidate B — 222BBB
CI: PASS
QA: PASS
Lead: REWORK
Decision: DEC-0009

Candidate C — 333CCC
CI: PASS
QA: PASS
Lead: ACCEPT
Accepted SHA: 333CCC
```

## 9. Closure summary page

The first report page should be concise and executive-readable.

Suggested fields:

- Product/Project
- Work Package
- Feature
- Phase / Layer
- Developer(s)
- Technical Lead
- Source SHA
- Accepted SHA
- CI/CD result
- QA result
- Lead disposition
- Integration status
- Phase status
- Accepted contribution summary
- Closure date
- Final disposition

Example final disposition:

`ACCEPTED / INTEGRATED / FROZEN / CLOSED`

## 10. Generated formats

Every closure should generate at minimum:

- `<ID>-CLOSURE.pdf`
- `<ID>-CLOSURE.html`
- `<ID>-CLOSURE.json`
- `<ID>-CLOSURE-BUNDLE.zip`

Purpose:

- PDF — formal human-readable record;
- HTML — searchable/browser-readable archival record;
- JSON — machine-readable complete closure state;
- ZIP — evidence-preserving portable phase footprint.

## 11. Closure bundle structure

Target structure:

```text
<WORK-PACKAGE-ID>-CLOSURE/
|
+-- <ID>-CLOSURE.pdf
+-- <ID>-CLOSURE.html
+-- <ID>-CLOSURE.json
+-- manifest.json
+-- README.html
|
+-- work-package/
|   +-- authorization.json
|   +-- requirements.json
|   +-- acceptance-criteria.json
|   +-- assignments.json
|
+-- git/
|   +-- source-sha.txt
|   +-- candidates.json
|   +-- accepted-sha.txt
|   +-- commits.json
|   +-- changed-files.json
|   +-- final.patch
|
+-- validation/
|   +-- developer/
|   +-- ci/
|   +-- qa/
|
+-- findings/
+-- rework/
+-- regressions/
+-- requests/
|
+-- screenshots/
|   +-- SCR-xxxxx.png
|   +-- SCR-xxxxx.json
|
+-- decisions/
|   +-- technical-lead.json
|
+-- audit/
|   +-- timeline.json
|
+-- checksums/
    +-- SHA256SUMS
```

Exact filenames may evolve, but the bundle must preserve equivalent evidence categories.

## 12. Secret exclusion

Closure/report generation must explicitly exclude or redact secrets.

Never include by default:

- `.env`;
- API tokens;
- passwords;
- session secrets;
- private keys;
- Cloudflare credentials;
- database encryption keys;
- unrelated host files;
- private local configuration not required as evidence.

The report generator must operate from allowlisted structured records/evidence, not blindly ZIP entire repository/home directories.

## 13. Closed-phase immutability

A `CLOSED` phase is historical evidence.

Do not mutate its accepted candidate history or generated closure record to represent later work.

Later work must create a revision/new work package that references the original closure.

The original closure bundle remains downloadable for developers who need to understand prior accepted behavior.

## 14. Developer lookup value

Closed artifacts must be useful months later.

A developer revising a feature should be able to obtain the prior phase bundle and answer:

- what was originally requested;
- what scope was authorized;
- what exact SHA was accepted;
- which files changed;
- which validation ran;
- what screenshots proved behavior;
- what rework occurred;
- what limitations remained;
- why the Technical Lead accepted it.

This historical usefulness is a primary product requirement, not merely compliance paperwork.
