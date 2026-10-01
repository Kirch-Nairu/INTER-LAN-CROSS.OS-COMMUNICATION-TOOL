CREATE TABLE crm_audit_events (
    audit_event_id TEXT PRIMARY KEY,
    actor_user_id TEXT NOT NULL,
    work_package_id TEXT NULL,
    candidate_id TEXT NULL,
    event_type TEXT NOT NULL,
    before_state TEXT NULL,
    after_state TEXT NULL,
    payload_json TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (actor_user_id) REFERENCES users(user_id),
    FOREIGN KEY (work_package_id) REFERENCES crm_work_packages(work_package_id),
    FOREIGN KEY (candidate_id) REFERENCES crm_candidate_submissions(candidate_id)
);

CREATE INDEX ix_crm_audit_work_package_time
    ON crm_audit_events(work_package_id, created_utc, audit_event_id);

CREATE INDEX ix_crm_audit_candidate_time
    ON crm_audit_events(candidate_id, created_utc, audit_event_id);

CREATE TABLE crm_import_receipts (
    import_receipt_id TEXT PRIMARY KEY,
    importer_user_id TEXT NOT NULL,
    declared_work_package_id TEXT NULL,
    resulting_work_package_id TEXT NULL,
    schema_version TEXT NOT NULL,
    source_method TEXT NOT NULL CHECK (source_method IN ('FILE', 'PASTE', 'GUIDED_FORM')),
    payload_sha256 TEXT NOT NULL,
    validation_result TEXT NOT NULL CHECK (validation_result IN ('ACCEPTED_DRAFT', 'REJECTED')),
    warnings_json TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (importer_user_id) REFERENCES users(user_id),
    FOREIGN KEY (resulting_work_package_id) REFERENCES crm_work_packages(work_package_id)
);

CREATE INDEX ix_crm_import_receipts_hash
    ON crm_import_receipts(payload_sha256, created_utc);
