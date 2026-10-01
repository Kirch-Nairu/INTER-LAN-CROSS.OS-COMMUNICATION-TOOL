# INTER-LAN Linux Host Runtime Contract

Status: design specification for the CRM rebaseline.

## 1. Canonical host decision

The Technical Lead's **Debian KDE workstation is the primary development machine and the canonical INTER-LAN host**.

The runtime must therefore be designed Linux-first.

This host performs all of the following roles:

- Technical Lead workstation;
- local application host;
- canonical SQLite/evidence store host;
- closure-artifact generator;
- audit-history host;
- developer/CI/QA web-gateway host;
- Cloudflare tunnel process owner;
- native desktop control surface.

The host is not headless by default. It is the Technical Lead's daily machine.

## 2. Native control application

The Technical Lead UI must be a real native desktop application suitable for Debian KDE.

Target properties:

- native window behavior;
- native keyboard shortcuts;
- KDE notifications;
- multi-monitor-friendly layout;
- no requirement to keep a browser open for Lead operations;
- dark/navy/blue command-center visual language;
- fast startup and clean shutdown;
- privilege-sensitive operations remain local where practical.

A webview wrapper around the developer portal does not satisfy this requirement.

## 3. CLI-first lifecycle

Primary command:

```bash
interlan start
```

`interlan start` is an orchestrator. It should eventually perform a bounded sequence similar to:

1. validate required host dependencies/configuration;
2. acquire a single-instance lock;
3. validate configured data/artifact paths;
4. open/validate SQLite;
5. run approved database migrations;
6. start local application/backend services;
7. start the role-limited developer/CI/QA web gateway;
8. start realtime services;
9. start the native Technical Lead control app;
10. start the Cloudflare Quick Tunnel for the web gateway;
11. capture the assigned tunnel URL;
12. publish the URL into native runtime state;
13. emit a READY summary;
14. keep process ownership coordinated for clean stop/recovery.

Representative output:

```text
INTER-LAN Developer Operations

Database          READY
Authority         READY
Native control    READY
Web gateway       READY
Realtime          READY
Cloudflare        CONNECTED

Local gateway:
http://127.0.0.1:<port>

Developer portal:
https://<assigned>.trycloudflare.com

Authorized staff: <count>
```

Shutdown command:

```bash
interlan stop
```

Expected orderly shutdown sequence:

1. stop new privileged transitions/submissions as appropriate;
2. notify connected browser clients that the host is stopping;
3. stop/close remote tunnel;
4. stop accepting new gateway connections;
5. flush/checkpoint persistence according to the database policy;
6. stop realtime/background workers;
7. stop local services;
8. close native control runtime;
9. release single-instance lock;
10. exit with explicit status.

## 4. Cloudflare Quick Tunnel integration

The startup experience should automatically launch a Cloudflare Quick Tunnel for the browser gateway when remote access is enabled.

Intended runtime shape:

```text
Developer/QA/CI browser
        |
        v
Cloudflare Quick Tunnel
        |
        v
127.0.0.1:<gateway-port>
        |
        v
Role-limited INTER-LAN web gateway
```

The tunnel process is owned by INTER-LAN lifecycle management.

The assigned public URL should be visible in:

- CLI READY output;
- native Remote Access panel;
- copy-to-clipboard action;
- optional QR-code presentation;
- audit history.

The application must treat a Quick Tunnel URL as ephemeral runtime state. Restarting the tunnel may produce a new URL.

Implementation must verify actual installed `cloudflared` behavior/version rather than assuming a URL or process state.

## 5. Public-gateway boundary

The tunnel must expose the role-limited staff web gateway, not unrestricted Technical Lead control APIs.

Public/remote users may need capabilities such as:

- view assigned packages;
- submit candidate identity and notes;
- upload allowed evidence;
- record gate results when their role permits it;
- create/respond to findings and requests;
- view/download allowed closed artifacts.

Technical Lead-only operations should remain local/native where practical, including:

- final acceptance;
- integration authorization;
- phase freeze/closure override;
- role/capability administration;
- host configuration;
- global export/backup controls;
- tunnel administration.

The tunnel is transport. INTER-LAN application authentication/authorization remains authoritative.

## 6. Tunnel control panel

The native app should expose a Remote Access view containing at least:

- tunnel state;
- current public URL;
- uptime;
- local gateway address;
- connected/known staff session summary;
- Copy URL;
- optional Show QR;
- Restart Tunnel;
- Disable Remote Access;
- recent tunnel lifecycle events.

Tunnel start/stop/restart and URL changes should produce audit events.

## 7. Linux-first filesystem layout

Final implementation should define explicit Linux paths rather than scattering runtime files through the repository checkout.

Conceptual categories:

- configuration;
- SQLite database;
- uploaded evidence;
- screenshots;
- generated reports;
- closure bundles;
- backups;
- logs/runtime diagnostics;
- temporary generation workspace;
- local secret references.

Exact paths belong to CRM-F00/implementation authority, but they should follow Linux user/system conventions and remain configurable.

No closure artifact may silently include:

- `.env` files;
- tokens;
- passwords;
- Cloudflare credentials;
- private keys;
- database encryption material;
- unrelated user-home content.

## 8. Linux secret storage

Linux is the primary host. Host-side secret handling must therefore have an explicit Linux strategy.

Where available, native secret-service integration should be preferred for user-session secrets.

Fallback behavior must be explicit, permission-restricted and visible to the Technical Lead. The application must not claim stronger secret protection than the host implementation actually provides.

## 9. Resilience on a daily-driver host

Because the canonical host is also the Technical Lead's primary workstation, the runtime must tolerate ordinary workstation realities:

- sleep/suspend/resume;
- network interface changes;
- Wi-Fi changes;
- temporary internet loss while LAN/local runtime remains available;
- tunnel loss/reconnect;
- screen lock/unlock;
- user logout/shutdown;
- app restart after crash;
- host reboot;
- external monitor changes.

Cloud/internet loss must not corrupt canonical local records.

Tunnel failure must not destroy local authority state.

## 10. Backup and recovery expectation

Local-first does not mean single-copy forever.

The host design must eventually support:

- consistent database backup;
- evidence/artifact backup;
- checksum verification;
- restore into a clean INTER-LAN runtime;
- recovery documentation;
- explicit retention policy.

Backup/export is separate from phase-closure bundles.

## 11. Development and validation expectation

Linux is the canonical runtime target, but cross-platform behavior still matters for staff clients and repository quality.

The product should eventually prove:

- native Debian host lifecycle;
- browser access from Windows/Linux clients;
- WebSocket/realtime behavior through supported transport paths;
- evidence upload/download across platforms;
- artifact generation on Linux;
- screenshot/report handling with portable filenames;
- safe shutdown/restart/recovery.
