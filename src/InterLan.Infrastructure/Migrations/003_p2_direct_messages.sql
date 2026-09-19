ALTER TABLE direct_conversations ADD COLUMN pair_key TEXT NULL;

CREATE UNIQUE INDEX ux_direct_conversations_pair_key
    ON direct_conversations(pair_key)
    WHERE pair_key IS NOT NULL;

CREATE INDEX ix_direct_members_user
    ON direct_conversation_members(user_id, conversation_id);

CREATE INDEX ix_messages_scope_cursor
    ON messages(scope_type, scope_id, created_utc, message_id);
