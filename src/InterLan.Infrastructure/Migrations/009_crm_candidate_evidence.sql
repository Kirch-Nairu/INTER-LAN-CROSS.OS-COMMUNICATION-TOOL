CREATE TABLE crm_candidate_submissions (
    candidate_id TEXT PRIMARY KEY,
    work_package_id TEXT NOT NULL,
    sequence INTEGER NOT NULL CHECK (sequence > 0),
    branch TEXT NOT NULL,
    source_sha TEXT NOT NULL,
    candidate_sha TEXT NOT NULL,
    commit_count INTEGER NOT NULL CHECK (commit_count >= 0),
    commit_range TEXT NULL,
    self_validation_json TEXT NOT NULL,
    known_limitations TEXT NULL,
    notes_to_validator TEXT NULL,
    submitted_by_user_id TEXT NOT NULL,
    submitted_utc TEXT NOT NULL,
    UNIQUE (work_package_id, sequence),
    UNIQUE (work_package_id, candidate_sha),
    FOREIGN KEY (work_package_id) REFERENCES crm_work_packages(work_package_id) ON DELETE CASCADE,
    FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id)
);

CREATE INDEX ix_crm_candidate_work_sequence
    ON crm_candidate_submissions(work_package_id, sequence);

CREATE TABLE crm_candidate_files (
    candidate_file_id TEXT PRIMARY KEY,
    candidate_id TEXT NOT NULL,
    path TEXT NOT NULL,
    change_type TEXT NOT NULL,
    added_lines INTEGER NOT NULL CHECK (added_lines >= 0),
    deleted_lines INTEGER NOT NULL CHECK (deleted_lines >= 0),
    UNIQUE (candidate_id, path),
    FOREIGN KEY (candidate_id) REFERENCES crm_candidate_submissions(candidate_id) ON DELETE CASCADE
);

CREATE TABLE crm_candidate_validations (
    validation_id TEXT PRIMARY KEY,
    candidate_id TEXT NOT NULL,
    candidate_sha TEXT NOT NULL,
    gate TEXT NOT NULL CHECK (gate IN ('CI', 'QA', 'LEAD')),
    result TEXT NOT NULL CHECK (result IN ('RUNNING', 'PASSED', 'FAILED')),
    actor_user_id TEXT NOT NULL,
    summary TEXT NULL,
    recorded_utc TEXT NOT NULL,
    FOREIGN KEY (candidate_id) REFERENCES crm_candidate_submissions(candidate_id) ON DELETE CASCADE,
    FOREIGN KEY (actor_user_id) REFERENCES users(user_id)
);

CREATE INDEX ix_crm_candidate_validations_candidate_gate
    ON crm_candidate_validations(candidate_id, gate, recorded_utc);

CREATE TABLE crm_work_package_acceptance (
    work_package_id TEXT PRIMARY KEY,
    candidate_id TEXT NOT NULL UNIQUE,
    accepted_sha TEXT NOT NULL,
    accepted_by_user_id TEXT NOT NULL,
    accepted_utc TEXT NOT NULL,
    FOREIGN KEY (work_package_id) REFERENCES crm_work_packages(work_package_id) ON DELETE CASCADE,
    FOREIGN KEY (candidate_id) REFERENCES crm_candidate_submissions(candidate_id),
    FOREIGN KEY (accepted_by_user_id) REFERENCES users(user_id)
);
