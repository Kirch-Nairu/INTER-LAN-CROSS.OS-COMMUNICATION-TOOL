# CRM Sprint 001 B — Runtime Architecture Decisions

## 2026-10-01 — Linux paths follow XDG ownership

**Commit:** `73786606f303e30e1acf41e2b257364b540f959a`

**Decision:** Runtime configuration, state, durable data, cache and ephemeral lock/state files resolve through XDG roots with deterministic home-directory fallbacks.

**Reason:** The Debian KDE workstation is both a daily-driver development machine and the canonical host; INTER-LAN must not scatter canonical data through the repository or current working directory.

**Alternatives rejected:** repository-relative state; one undifferentiated `~/.interlan` directory.

**Compatibility/migration consequence:** existing messaging storage remains reusable, but CRM host composition now has an explicit Linux-first path contract.

**Follow-up:** packaging may expose overrides, but overrides must remain absolute and preserve durable-vs-ephemeral separation.

## 2026-10-01 — Single-instance authority is an owned file handle

**Commit:** `5ab5776184f84580b0ce8a018d43f0edf8a2cf37`

**Decision:** Canonical host ownership is proven by an exclusive open lease, not by lock-file existence or a remembered PID.

**Reason:** stale files and PID reuse must not block recovery or cause an unrelated process to be treated as INTER-LAN authority.

**Alternatives rejected:** PID-file authority; deleting any existing lock file before start.

**Compatibility/migration consequence:** runtime-state PIDs are diagnostic metadata only and never ownership authority.

**Follow-up:** systemd packaging can wrap the same invariant rather than replace it.

## 2026-10-01 — Remote tunnel targets a dedicated subordinate gateway

**Commit:** `5b1cb183de7e95da833abb325f05381b3c7bff13`

**Decision:** `cloudflared` targets a dedicated loopback-only `--crm-gateway` server mode, not the full legacy server and never the native Lead control plane.

**Reason:** remote transport must not expand the Technical Lead authority boundary.

**Alternatives rejected:** tunneling the complete existing server; embedding the Lead application in a WebView.

**Compatibility/migration consequence:** future developer/CI/QA HTTP routes must be deliberately added to the CRM gateway with application authentication and role policy.

**Follow-up:** the current gateway intentionally exposes only its readiness surface until those authenticated CRM routes exist.

## 2026-10-01 — Backend and staff gateway are separate supervised children

**Commit:** `6d37ef127424c8458797745f66695654e255a316`

**Decision:** the canonical HTTPS backend and role-limited browser gateway run as distinct loopback child processes with separate ports and readiness gates.

**Reason:** local canonical authority and remote browser transport have different security and failure semantics and must not be conflated.

**Alternatives rejected:** declaring readiness when processes merely spawn; using the public tunnel as the canonical service endpoint.

**Compatibility/migration consequence:** existing server hosting is reused as the canonical backend while the CRM gateway remains independently evolvable.

**Follow-up:** authenticated CRM gateway routes should call bounded backend contracts without exposing Lead-only operations.

## 2026-10-01 — Quick Tunnel URLs are ephemeral runtime state

**Commit:** `43c2cea10cf592a58285b87843357338c1cf7b78`

**Decision:** Quick Tunnel death or network loss degrades remote access while preserving local authority; reconnect creates a new URL and old URLs are never treated as canonical identity.

**Reason:** Quick Tunnel addresses can change after restart, network loss or workstation suspend.

**Alternatives rejected:** persisting a Quick Tunnel URL as stable configuration; failing the local host solely because cloudflared is absent.

**Compatibility/migration consequence:** local runtime status is authoritative; public URL is only transient status metadata.

**Follow-up:** named/stable tunnels, if introduced later, require a separate explicit authority and credential design.

## 2026-10-01 — Suspend invalidates remote transport before sleep

**Commit:** `4567f78fcbb90694705a6d1c58a2dd9b637a9201`

**Decision:** suspend handling drops Quick Tunnel transport while retaining canonical local state; resume reconciles the host and reacquires remote access when it was previously enabled.

**Reason:** the canonical host is also the Technical Lead's daily-driver workstation and must tolerate normal suspend/resume behavior without trusting stale transport state.

**Alternatives rejected:** preserving the pre-suspend public URL as valid; shutting down canonical durable state for every suspend.

**Compatibility/migration consequence:** a KDE/system power signal adapter can invoke the existing coordinator without changing host authority semantics.

**Follow-up:** bind the coordinator to the selected Debian/KDE power-event integration during productization.
