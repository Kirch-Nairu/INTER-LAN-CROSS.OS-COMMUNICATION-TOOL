ALTER TABLE users ADD COLUMN password_salt TEXT NULL;
ALTER TABLE users ADD COLUMN password_hash TEXT NULL;
ALTER TABLE users ADD COLUMN password_iterations INTEGER NULL;

CREATE TABLE join_requests (
    request_id TEXT PRIMARY KEY,
    invite_id TEXT NOT NULL,
    requested_username TEXT NOT NULL,
    display_name TEXT NOT NULL,
    device_name TEXT NOT NULL,
    platform TEXT NOT NULL,
    enrollment_secret_hash TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('PENDING', 'APPROVED', 'REJECTED')),
    created_utc TEXT NOT NULL,
    decided_by_user_id TEXT NULL,
    decided_utc TEXT NULL,
    user_id TEXT NULL,
    device_id TEXT NULL,
    enrollment_exchanged_utc TEXT NULL,
    FOREIGN KEY (invite_id) REFERENCES invite_tokens(invite_id),
    FOREIGN KEY (decided_by_user_id) REFERENCES users(user_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (device_id) REFERENCES devices(device_id)
);

CREATE INDEX ix_join_requests_status_created
    ON join_requests(status, created_utc);

CREATE TABLE device_sessions (
    session_id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    device_id TEXT NULL,
    token_hash TEXT NOT NULL UNIQUE,
    created_utc TEXT NOT NULL,
    expires_utc TEXT NOT NULL,
    revoked_utc TEXT NULL,
    last_seen_utc TEXT NULL,
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (device_id) REFERENCES devices(device_id)
);

CREATE INDEX ix_device_sessions_user_active
    ON device_sessions(user_id, revoked_utc, expires_utc);
