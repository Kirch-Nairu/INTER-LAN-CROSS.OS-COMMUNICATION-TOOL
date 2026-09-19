CREATE TABLE users (
    user_id TEXT PRIMARY KEY,
    username TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL,
    role TEXT NOT NULL CHECK (role IN ('OWNER', 'ADMIN', 'MEMBER')),
    created_utc TEXT NOT NULL,
    disabled_utc TEXT NULL
);

CREATE TABLE server_identity (
    singleton_key INTEGER PRIMARY KEY CHECK (singleton_key = 1),
    server_id TEXT NOT NULL UNIQUE,
    server_name TEXT NOT NULL,
    owner_user_id TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (owner_user_id) REFERENCES users(user_id)
);

CREATE TABLE server_settings (
    singleton_key INTEGER PRIMARY KEY CHECK (singleton_key = 1),
    bind_address TEXT NOT NULL DEFAULT '0.0.0.0',
    port INTEGER NOT NULL DEFAULT 7443 CHECK (port BETWEEN 1 AND 65535),
    discovery_enabled INTEGER NOT NULL DEFAULT 1 CHECK (discovery_enabled IN (0, 1)),
    client_approval_required INTEGER NOT NULL DEFAULT 1 CHECK (client_approval_required IN (0, 1)),
    storage_path TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE TABLE devices (
    device_id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    device_name TEXT NOT NULL,
    platform TEXT NOT NULL,
    certificate_fingerprint TEXT NULL,
    approved_utc TEXT NULL,
    revoked_utc TEXT NULL,
    last_seen_utc TEXT NULL,
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);

CREATE TABLE invite_tokens (
    invite_id TEXT PRIMARY KEY,
    token_hash TEXT NOT NULL UNIQUE,
    created_by_user_id TEXT NOT NULL,
    expires_utc TEXT NOT NULL,
    consumed_utc TEXT NULL,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (created_by_user_id) REFERENCES users(user_id)
);

CREATE TABLE direct_conversations (
    conversation_id TEXT PRIMARY KEY,
    created_utc TEXT NOT NULL
);

CREATE TABLE direct_conversation_members (
    conversation_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    joined_utc TEXT NOT NULL,
    PRIMARY KEY (conversation_id, user_id),
    FOREIGN KEY (conversation_id) REFERENCES direct_conversations(conversation_id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);

CREATE TABLE groups (
    group_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    topic TEXT NULL,
    created_by_user_id TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (created_by_user_id) REFERENCES users(user_id)
);

CREATE TABLE group_members (
    group_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    group_role TEXT NOT NULL CHECK (group_role IN ('OWNER', 'ADMIN', 'MEMBER')),
    joined_utc TEXT NOT NULL,
    removed_utc TEXT NULL,
    PRIMARY KEY (group_id, user_id),
    FOREIGN KEY (group_id) REFERENCES groups(group_id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);

CREATE TABLE messages (
    message_id TEXT PRIMARY KEY,
    scope_type TEXT NOT NULL CHECK (scope_type IN ('DIRECT', 'GROUP')),
    scope_id TEXT NOT NULL,
    sender_user_id TEXT NOT NULL,
    client_message_id TEXT NOT NULL,
    body TEXT NULL,
    reply_to_message_id TEXT NULL,
    created_utc TEXT NOT NULL,
    edited_utc TEXT NULL,
    deleted_utc TEXT NULL,
    UNIQUE (sender_user_id, client_message_id),
    FOREIGN KEY (sender_user_id) REFERENCES users(user_id),
    FOREIGN KEY (reply_to_message_id) REFERENCES messages(message_id)
);

CREATE INDEX ix_messages_scope_created
    ON messages(scope_type, scope_id, created_utc);

CREATE TABLE message_receipts (
    message_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    delivered_utc TEXT NULL,
    read_utc TEXT NULL,
    PRIMARY KEY (message_id, user_id),
    FOREIGN KEY (message_id) REFERENCES messages(message_id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);

CREATE TABLE attachments (
    attachment_id TEXT PRIMARY KEY,
    message_id TEXT NULL,
    uploader_user_id TEXT NOT NULL,
    original_filename TEXT NOT NULL,
    mime_type TEXT NOT NULL,
    byte_size INTEGER NOT NULL CHECK (byte_size >= 0),
    sha256 TEXT NOT NULL,
    local_cache_path TEXT NULL,
    archive_state TEXT NOT NULL CHECK (archive_state IN ('PENDING', 'ARCHIVING', 'ARCHIVED', 'FAILED')),
    created_utc TEXT NOT NULL,
    FOREIGN KEY (message_id) REFERENCES messages(message_id) ON DELETE SET NULL,
    FOREIGN KEY (uploader_user_id) REFERENCES users(user_id)
);

CREATE INDEX ix_attachments_sha256 ON attachments(sha256);
CREATE INDEX ix_attachments_archive_state ON attachments(archive_state);

CREATE TABLE telegram_archives (
    attachment_id TEXT PRIMARY KEY,
    destination_chat_id TEXT NOT NULL,
    telegram_message_id TEXT NOT NULL,
    telegram_file_id TEXT NULL,
    archived_sha256 TEXT NOT NULL,
    archived_utc TEXT NOT NULL,
    FOREIGN KEY (attachment_id) REFERENCES attachments(attachment_id) ON DELETE CASCADE
);

CREATE TABLE archive_jobs (
    archive_job_id TEXT PRIMARY KEY,
    attachment_id TEXT NOT NULL,
    state TEXT NOT NULL CHECK (state IN ('PENDING', 'RUNNING', 'SUCCEEDED', 'FAILED')),
    attempt_count INTEGER NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    next_attempt_utc TEXT NULL,
    last_error TEXT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    FOREIGN KEY (attachment_id) REFERENCES attachments(attachment_id) ON DELETE CASCADE
);

CREATE INDEX ix_archive_jobs_state_next
    ON archive_jobs(state, next_attempt_utc);

CREATE TABLE audit_events (
    audit_event_id TEXT PRIMARY KEY,
    actor_user_id TEXT NULL,
    event_type TEXT NOT NULL,
    subject_type TEXT NOT NULL,
    subject_id TEXT NULL,
    payload_json TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (actor_user_id) REFERENCES users(user_id)
);
