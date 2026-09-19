ALTER TABLE devices ADD COLUMN credential_created_utc TEXT NULL;
ALTER TABLE devices ADD COLUMN credential_rotated_utc TEXT NULL;
ALTER TABLE devices ADD COLUMN credential_last_used_utc TEXT NULL;
