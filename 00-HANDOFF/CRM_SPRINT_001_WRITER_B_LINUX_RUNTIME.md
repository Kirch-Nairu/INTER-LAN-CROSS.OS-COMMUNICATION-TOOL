# KIRION FORGE — CODE WRITER B — CRM SPRINT 001 LINUX RUNTIME / NATIVE CONTROL

Technical Authority: Kirch Ivan Balite

Target product: INTER-LAN Developer Operations Control Plane

Execution mode: CODE-FOCUSED SPRINT

Commit target: 40 substantive atomic commits. No padding, empty commits, formatting-only churn, fake refactors, or documentation farming.

## Source and branch

The exact source SHA will be the CRM Sprint 001 repaired baseline produced after the known `MessageTextPolicy.cs` bootstrap defect is corrected and verified.

Writer branch:

`KIRCH-INTERLAN-CRM-S001-B-LINUX-RUNTIME`

Do not begin from old P2 branches or from `main` directly.

## Read first

1. `AGENTS.md`
2. `00-HANDOFF/CRM_F00_PRODUCT_REBASELINE.md`
3. `docs/CRM_PRODUCT_ARCHITECTURE.md`
4. `docs/CRM_LINUX_HOST_RUNTIME.md`
5. `docs/CRM_PHASE_CLOSURE_EVIDENCE.md`
6. this handoff

## Sprint mission

Build the Linux-first Technical Lead runtime foundation for the new CRM product.

The canonical host is the Technical Lead's Debian KDE daily-driver workstation. This sprint must make that host model real without turning the Lead experience into a browser wrapper.

Primary capability goals:

- native Debian/KDE Technical Lead shell using the existing desktop stack;
- dark/navy/blue command-center visual foundation;
- Linux-first host lifecycle abstraction;
- single-instance coordination;
- explicit runtime/data/artifact path resolution;
- startup health/state model;
- orderly shutdown model;
- host suspend/network-change tolerant lifecycle hooks where practical;
- CLI-oriented start/stop/status orchestration surface;
- local web-gateway process ownership;
- Cloudflare `cloudflared` process discovery/launch/stop/restart abstraction;
- parsing/capturing the ephemeral Quick Tunnel URL without trusting arbitrary stdout;
- native Remote Access panel/state;
- tunnel audit/event hooks/interfaces;
- KDE-friendly notifications/clipboard integration where portable abstractions permit;
- browser gateway shell for role-limited remote staff access only as needed for host/runtime integration;
- safe failure behavior when `cloudflared` is absent, exits, changes URL, or internet disappears.

Do not attempt the full work-package domain or backend workflow engine in this lane. Writer A owns CRM authority/data semantics.

## Owned scope

Writer B owns CRM/runtime-specific code under:

- `src/InterLan.Application/CrmHost/**`
- `src/InterLan.Desktop/Crm/**`
- `src/InterLan.Server/CrmGateway/**`
- `web-client/src/crm/**`

Writer B may minimally modify existing host/bootstrap/composition files needed to route execution into these owned modules. Shared-file changes must be small and logged as major decisions when they constrain future integration.

Writer B may add focused executable checks under:

- `tools/InterLan.CrmHostChecks/**`
- `tools/InterLan.CrmTunnelChecks/**`
- `tools/InterLan.CrmDesktopChecks/**`

Writer B owns the sprint decision log:

`docs/decisions/CRM_SPRINT_001_B_DECISIONS.md`

## Decision-log rule

Code remains the focus.

Update the decision log only for major runtime/product decisions, including:

- Linux filesystem/runtime conventions;
- process-ownership boundaries;
- CLI lifecycle semantics;
- tunnel trust/parsing model;
- native-vs-browser authority boundary;
- desktop composition choices that constrain later modules;
- portable fallback behavior.

Do not create documentation-only progress commits. Decision-log changes should normally ride with the code commit implementing the decision.

Each entry should record:

- date/commit;
- decision;
- reason;
- alternatives rejected;
- operational consequence;
- follow-up if any.

## Prohibited scope

Do not redesign or own:

- `src/InterLan.Domain/Crm/**`
- `src/InterLan.Contracts/Crm/**`
- `src/InterLan.Infrastructure/Crm/**`
- Writer A's authoritative workflow/state-machine/persistence implementation;
- candidate/CI/QA business rules except presentation/integration stubs required to compile against explicit contracts;
- GitHub Actions architecture;
- release signing/deployment;
- full closure-report generator;
- arbitrary shell execution from UI/imported data.

Do not merge, promote, deploy or self-accept.

## Required invariants

- the Debian KDE machine is the canonical host;
- Technical Lead privileged control remains native/local where practical;
- browser gateway is subordinate and role-limited;
- `interlan start`/host start never exposes unrestricted Lead-control APIs through the tunnel;
- Quick Tunnel URL is ephemeral runtime state, not canonical identity;
- Cloudflare transport never replaces application authentication/authorization;
- missing/broken `cloudflared` fails visibly without corrupting local state;
- tunnel/process crashes cannot corrupt SQLite/canonical CRM state;
- startup/shutdown are idempotent enough to avoid orphaned duplicate host/tunnel processes;
- paths follow Linux-first conventions and remain explicit/configurable;
- secrets do not enter logs, browser payloads or generated artifacts;
- browser/WebSocket transport must tolerate reconnect/change of public tunnel URL;
- no fake successful host state when dependencies are unavailable.

## Commit progression

Commits 1-10: native CRM shell, runtime state model, Linux path/config abstraction, single-instance/startup-health foundation.

Commits 11-20: CLI/start-stop-status orchestration, child-process lifecycle, local gateway ownership and clean shutdown/recovery behavior.

Commits 21-30: `cloudflared` discovery/process supervision, URL capture, restart/failure handling, Remote Access native state/panel and tunnel-focused executable checks.

Commits 31-40: KDE/native polish, client reconnect/runtime status integration, degraded network/tunnel scenarios, desktop/host verification and production-like lifecycle hardening.

If the authorized scope is genuinely complete before 40 meaningful commits, STOP rather than fabricate commits.

## Validation

Before first product mutation, independently run the repaired baseline:

```bash
dotnet build InterLan.sln -c Release
dotnet run --project tools/InterLan.SuiteChecks/InterLan.SuiteChecks.csproj -c Release --no-build
```

If `web-client/**` changes, run the repository-supported install/build path and report exact command/result.

Maintain focused host/tunnel/desktop proof. Run the full aggregate suite at substantive checkpoints near commits 10, 20, 30 and final.

Do not claim Debian/KDE, Cloudflare, or cross-platform runtime validation that was not actually executed. Clearly separate simulated/fake-process tests from real `cloudflared`/Debian evidence.

## Return

Return:

- exact starting SHA;
- exact final SHA;
- exact substantive commit count;
- files/projects changed;
- Linux runtime/CLI behavior implemented;
- native Lead behavior implemented;
- tunnel behavior implemented;
- validation actually run versus NOT RUN;
- major decisions recorded;
- known limitations/dependencies;
- `READY FOR INDEPENDENT REVIEW` or exact BLOCKED disposition.
