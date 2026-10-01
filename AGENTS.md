# INTER-LAN Engineering Authority — Developer Operations CRM Rebaseline

Technical Authority: **Kirch Ivan Balite**

Branch-local authority applies to:

`KIRCH-INTERLAN-DEVELOPER-OPS-CRM-REBASELINE`

Rebaseline source:

`main@d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`

## Current mode

**CRM-F00 PRODUCT REBASELINE / DOCUMENTATION ONLY**

INTER-LAN is being reoriented from a messaging-first cross-OS communication product into a **local-first Developer Operations Control Plane / Development CRM** for real engineering staff.

The Technical Lead is transitioning to Linux. The **Debian KDE workstation is the primary development machine and the canonical INTER-LAN host**.

No implementation is authorized by this branch-local authority yet.

The old P2 messaging campaign and 4x P2 benchmark must not resume on this branch as if the old product target were unchanged.

## Read and obey

1. `00-HANDOFF/CRM_F00_PRODUCT_REBASELINE.md`
2. `docs/CRM_PRODUCT_ARCHITECTURE.md`
3. `docs/CRM_LINUX_HOST_RUNTIME.md`
4. `docs/CRM_WORK_PACKAGE_JSON_IMPORT.md`
5. `docs/CRM_PHASE_CLOSURE_EVIDENCE.md`

Historical P0/P1/P2 campaign documents remain useful evidence about the existing implementation, but they do not override the CRM rebaseline direction on this branch.

## CRM-F00 purpose

Before implementation resumes, CRM-F00 must inventory the existing codebase and classify each relevant component as:

- retain as-is;
- retain but adapt;
- retire;
- replace;
- unknown pending proof.

A new bounded implementation handoff must then identify an exact green source SHA, owned files, prohibited scope, acceptance criteria and executable validation.

## Product laws

- The Work Package is the central operational unit.
- Authority precedes implementation.
- Validation belongs to an exact candidate SHA.
- Any code change creates a new candidate requiring the applicable revalidation.
- Implementation, CI/CD, QA and Technical Lead acceptance remain distinct gates.
- The discovering role does not automatically become the fixing role.
- Only accepted work is integrated.
- Closed phases are immutable historical records.
- Later revisions create new work packages linked to prior closure artifacts.
- AI/ChatGPT-generated JSON is declarative input data, never automatic shell authority.
- JSON imports create reviewed drafts before explicit Technical Lead authorization.
- Required screenshots/evidence are structured records bound to candidate/criteria.
- Closure generates detailed PDF/HTML/JSON reports plus a checksummed evidence bundle.
- The Debian KDE host owns canonical local state, native Lead control, artifact generation and tunnel lifecycle.
- Browser clients are subordinate role-limited staff surfaces.
- Cloudflare tunnel transport never replaces INTER-LAN authentication/authorization.
- Technical Lead-only actions should remain local/native where practical.
- Secrets must not leak into reports, closure bundles or browser clients.

## Implementation stop rule

Do not mutate product code under CRM-F00 documentation authority.

Implementation begins only after a later explicit Code Writer handoff names:

- exact source SHA;
- exact branch;
- owned scope;
- prohibited scope;
- migration rules;
- validation commands;
- acceptance criteria;
- failure/stop conditions.
