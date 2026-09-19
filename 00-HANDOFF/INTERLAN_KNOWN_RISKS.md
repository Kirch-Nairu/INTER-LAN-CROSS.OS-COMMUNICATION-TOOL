# INTER-LAN KNOWN RISKS AND DEBUG-HOLE REGISTER

This register exists so deferred problems cannot disappear from context.

## R1 — Desktop does not yet host canonical server lifecycle

Current desktop is still a shell while `InterLan.Server` is a separate executable.

Risk:
final packaging could produce two disconnected artifacts that do not satisfy the V1 contract that one desktop installation can create/own a server.

Required:
introduce reusable ServerHost composition and wire SERVER OWNER MODE to it before P6 completion.

## R2 — Configuration authority is split

Current sources include:
- IConfiguration/environment;
- SQLite `server_settings`;
- RuntimeConfiguration/runtime.json.

Risk:
Kestrel, discovery, UI, storage, or archive can advertise/use different values.

Required:
define canonical runtime settings service and explicit override precedence.

## R3 — Verification projects are not all in the solution

Current solution omits multiple phase check/smoke projects.

Risk:
`dotnet build InterLan.sln` can be green while verification code does not compile.

Required:
normalize verification topology and create aggregate local suite entrypoint.

## R4 — Network-smoke process/port races

Existing smokes reserve a random port, release it, then launch Kestrel.

Risk:
parallel runners/processes can steal the port and create flaky failures.

Required:
shared process/network harness with deterministic diagnostics and safer lifecycle.

## R5 — Realtime revocation gap

SignalR validates token at connect time and keeps the connection in a user group.

Risk:
a revoked device may continue receiving realtime events until disconnect.

Required:
disconnect/revalidate revoked realtime clients and prove immediate loss of delivery.

## R6 — Receipt semantics incomplete

Current receipt path does not fully distinguish actual recipient delivery from read.

Risk:
UI may display false delivery/read states.

Required:
define and test delivery/read state machine.

## R7 — Multi-device identity unresolved

Join approval currently creates a new user record, while username is unique.

Risk:
same human cannot cleanly pair laptop + phone to one user identity.

Required:
define existing-user device enrollment vs new-user enrollment and migrate safely.

## R8 — SQLite concurrency policy missing

Current connection setup enables foreign keys only.

Risk:
P3–P5 concurrent writes may surface `database is locked`, long stalls, or inconsistent retry behavior.

Required:
explicit WAL/busy-timeout/synchronous policy and concurrency torture tests.

## R9 — Web asset composition not deterministic

Vite output is gitignored under server wwwroot and no lockfile/publish composition is authoritative.

Risk:
clean server publish can omit Android/web client entirely.

Required:
locked frontend dependencies and one local publish composition contract before P6 acceptance.

## R10 — Dependency reproducibility incomplete

Risks:
- npm lockfile absent;
- NuGet lock policy absent;
- SDK/action version policies differ.

Required:
dependency locking during code campaign; workflow pinning in final CI/CD wave.

## R11 — Workflow sediment

Existing P0/P1/P2 workflows duplicate restore/build/test work.

Risk:
adding P3–P7 workflows would multiply runtime and debugging surfaces.

Required:
do not add new phase workflow architecture; consolidate after P7.

## R12 — Live Telegram is nondeterministic

Risk:
rate limits, external outage, DNS, missing secret, or runner egress can make ordinary CI flaky.

Required:
mandatory deterministic fake/archive simulator; optional secret-enabled live proof.

## R13 — Cross-platform filesystem differences

Risk:
Windows/Linux/macOS differ on invalid names, case sensitivity, locking, permissions, path limits, Unicode, and atomic replace semantics.

Required:
portable file abstraction and tests during P4.

## R14 — Unix pairing-state protection weaker than Windows

Windows uses DPAPI-protected local key.
Current Unix fallback uses 0600 key material next to encrypted state.

Risk:
encryption does not add protection against compromise of the same Unix user account.

Required:
macOS Keychain / Linux Secret Service where available, with explicit fallback policy.

## R15 — Migration matrix can become late-stage failure

Fresh database tests alone are insufficient.

Risk:
P7/final CI discovers upgrades from P0/P1/P2 historical schemas fail.

Required:
maintain historical-schema upgrade fixtures/checks throughout later phases.

## Predicted final CI/CD failure order if unresolved

1. aggregate-suite orchestration;
2. missing web assets in publish output;
3. owner desktop/server-host composition;
4. Linux/macOS packaging/secure-storage differences;
5. network-smoke flakiness;
6. SQLite concurrency;
7. filesystem portability;
8. migration upgrades;
9. browser self-signed TLS/session integration;
10. Telegram external dependency;
11. release artifact/signing/version composition.
