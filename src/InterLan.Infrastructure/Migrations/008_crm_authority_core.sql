CREATE TABLE crm_projects (
    project_id TEXT PRIMARY KEY,
    project_key TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    created_by_user_id TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    FOREIGN KEY (created_by_user_id) REFERENCES users(user_id)
);

CREATE TABLE crm_features (
    feature_id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    feature_key TEXT NOT NULL,
    name TEXT NOT NULL,
    created_by_user_id TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    UNIQUE (project_id, feature_key),
    FOREIGN KEY (project_id) REFERENCES crm_projects(project_id) ON DELETE CASCADE,
    FOREIGN KEY (created_by_user_id) REFERENCES users(user_id)
);

CREATE TABLE crm_staff_role_grants (
    role_grant_id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    crm_role TEXT NOT NULL CHECK (crm_role IN (
        'TECHNICAL_LEAD',
        'BACKEND_DEVELOPER',
        'FRONTEND_DEVELOPER',
        'CI_CD_VALIDATOR',
        'QUALITY_ASSURANCE',
        'OBSERVER',
        'SYSTEM'
    )),
    granted_by_user_id TEXT NOT NULL,
    granted_utc TEXT NOT NULL,
    revoked_utc TEXT NULL,
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (granted_by_user_id) REFERENCES users(user_id)
);

CREATE UNIQUE INDEX ux_crm_staff_role_active
    ON crm_staff_role_grants(user_id, crm_role)
    WHERE revoked_utc IS NULL;

CREATE TABLE crm_work_packages (
    work_package_id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    feature_id TEXT NOT NULL,
    phase TEXT NOT NULL,
    layer TEXT NOT NULL CHECK (layer IN (
        'BACKEND', 'FRONTEND', 'INFRASTRUCTURE', 'VALIDATION', 'CROSS_LAYER'
    )),
    technical_lead_user_id TEXT NOT NULL,
    state TEXT NOT NULL CHECK (state IN (
        'DRAFT',
        'AUTHORIZED',
        'IMPLEMENTING',
        'CANDIDATE_SUBMITTED',
        'CI_PENDING',
        'CI_RUNNING',
        'CI_FAILED',
        'CI_PASSED',
        'QA_PENDING',
        'QA_RUNNING',
        'QA_FAILED',
        'QA_PASSED',
        'LEAD_REVIEW',
        'LEAD_REWORK',
        'LEAD_ACCEPTED',
        'INTEGRATION_AUTHORIZED',
        'INTEGRATED',
        'PHASE_FROZEN',
        'CLOSED'
    )),
    source_repository TEXT NOT NULL,
    source_branch TEXT NOT NULL,
    source_sha TEXT NOT NULL,
    source_verified INTEGER NOT NULL CHECK (source_verified IN (0, 1)),
    problem TEXT NOT NULL,
    solution TEXT NOT NULL,
    ci_required INTEGER NOT NULL CHECK (ci_required IN (0, 1)),
    qa_required INTEGER NOT NULL CHECK (qa_required IN (0, 1)),
    screenshots_required INTEGER NOT NULL CHECK (screenshots_required IN (0, 1)),
    self_checks_json TEXT NOT NULL,
    version INTEGER NOT NULL DEFAULT 1 CHECK (version >= 1),
    created_by_user_id TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    FOREIGN KEY (project_id) REFERENCES crm_projects(project_id),
    FOREIGN KEY (feature_id) REFERENCES crm_features(feature_id),
    FOREIGN KEY (technical_lead_user_id) REFERENCES users(user_id),
    FOREIGN KEY (created_by_user_id) REFERENCES users(user_id)
);

CREATE INDEX ix_crm_work_packages_state ON crm_work_packages(state);
CREATE INDEX ix_crm_work_packages_project_feature ON crm_work_packages(project_id, feature_id);

CREATE TABLE crm_requirements (
    work_package_id TEXT NOT NULL,
    requirement_id TEXT NOT NULL,
    requirement_text TEXT NOT NULL,
    ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
    PRIMARY KEY (work_package_id, requirement_id),
    FOREIGN KEY (work_package_id) REFERENCES crm_work_packages(work_package_id) ON DELETE CASCADE
);

CREATE TABLE crm_deliverables (
    work_package_id TEXT NOT NULL,
    deliverable_id TEXT NOT NULL,
    deliverable_text TEXT NOT NULL,
    ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
    PRIMARY KEY (work_package_id, deliverable_id),
    FOREIGN KEY (work_package_id) REFERENCES crm_work_packages(work_package_id) ON DELETE CASCADE
);

CREATE TABLE crm_acceptance_criteria (
    work_package_id TEXT NOT NULL,
    criterion_id TEXT NOT NULL,
    criterion_text TEXT NOT NULL,
    evidence_kinds_json TEXT NOT NULL,
    ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
    PRIMARY KEY (work_package_id, criterion_id),
    FOREIGN KEY (work_package_id) REFERENCES crm_work_packages(work_package_id) ON DELETE CASCADE
);

CREATE TABLE crm_scope_rules (
    scope_rule_id TEXT PRIMARY KEY,
    work_package_id TEXT NOT NULL,
    scope_kind TEXT NOT NULL CHECK (scope_kind IN ('OWNED', 'PROHIBITED')),
    pattern TEXT NOT NULL,
    UNIQUE (work_package_id, scope_kind, pattern),
    FOREIGN KEY (work_package_id) REFERENCES crm_work_packages(work_package_id) ON DELETE CASCADE
);

CREATE TABLE crm_assignments (
    assignment_id TEXT PRIMARY KEY,
    work_package_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    crm_role TEXT NOT NULL,
    assigned_by_user_id TEXT NOT NULL,
    assigned_utc TEXT NOT NULL,
    revoked_utc TEXT NULL,
    FOREIGN KEY (work_package_id) REFERENCES crm_work_packages(work_package_id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (assigned_by_user_id) REFERENCES users(user_id)
);

CREATE UNIQUE INDEX ux_crm_assignment_active
    ON crm_assignments(work_package_id, user_id, crm_role)
    WHERE revoked_utc IS NULL;
