# P1 — LAN Connectivity and Identity

P1 is the first networked phase.

## Trust model

Discovery is untrusted metadata. A discovery packet can tell a client that a server exists, but it cannot grant membership or authority.

The persistent server certificate establishes a stable SHA-256 fingerprint. Native clients are expected to pin that fingerprint after explicit pairing. The certificate private key remains on the owner host.

The V1 Android web client is hosted by the same HTTPS server. Because V1 uses a locally generated certificate rather than a public CA certificate, a mobile browser may require the user to trust/accept the local certificate before opening the web client. P1 does not pretend otherwise.

## Enrollment

Owner bootstrap is loopback-only and one-time.

Owner flow:

1. bootstrap server locally;
2. owner logs in;
3. owner issues an invite;
4. client submits join request using the invite and a client-held enrollment secret;
5. owner approves/rejects;
6. approved client exchanges its one-time enrollment secret for a bearer session;
7. only the session-token hash is stored server-side.

Invite tokens and session tokens are returned to the caller once and are never persisted plaintext.

## Discovery

UDP multicast:

- group: `239.255.72.77`
- port: `47777`
- protocol: `interlan-v1`

Announcement contains server ID, display name, HTTPS port and certificate fingerprint.

Manual server URI remains valid even if multicast is unavailable.
