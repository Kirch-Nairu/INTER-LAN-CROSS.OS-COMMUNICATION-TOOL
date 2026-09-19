CREATE TABLE direct_conversation_preferences (
    conversation_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    pinned_utc TEXT NULL,
    muted_until_utc TEXT NULL,
    archived_utc TEXT NULL,
    updated_utc TEXT NOT NULL,
    PRIMARY KEY (conversation_id, user_id),
    FOREIGN KEY (conversation_id) REFERENCES direct_conversations(conversation_id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

CREATE INDEX ix_direct_preferences_user
    ON direct_conversation_preferences(user_id, archived_utc, pinned_utc);
