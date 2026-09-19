ALTER TABLE devices ADD COLUMN credential_hash TEXT NULL;

CREATE UNIQUE INDEX ux_devices_credential_hash
    ON devices(credential_hash)
    WHERE credential_hash IS NOT NULL;
