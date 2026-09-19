CREATE INDEX ix_direct_conversation_members_user
    ON direct_conversation_members(user_id, conversation_id);

CREATE INDEX ix_messages_scope_order
    ON messages(scope_type, scope_id, created_utc, message_id);

CREATE INDEX ix_message_receipts_user_read
    ON message_receipts(user_id, read_utc, message_id);

CREATE INDEX ix_devices_user_revoked
    ON devices(user_id, revoked_utc, device_id);

CREATE INDEX ix_device_sessions_device_active
    ON device_sessions(device_id, revoked_utc, expires_utc);
