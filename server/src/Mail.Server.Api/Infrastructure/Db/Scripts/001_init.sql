-- Mail service initial schema

CREATE TABLE IF NOT EXISTS mails (
    id              UUID PRIMARY KEY,
    project_id      TEXT NOT NULL,
    title           TEXT NOT NULL,
    content         TEXT NOT NULL,
    attachments     JSONB NOT NULL DEFAULT '[]',
    sender_name     TEXT,
    expire_at       TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by      TEXT
);

CREATE INDEX IF NOT EXISTS idx_mails_project_created
    ON mails (project_id, created_at DESC);

CREATE TABLE IF NOT EXISTS user_mails (
    id              UUID PRIMARY KEY,
    mail_id         UUID NOT NULL REFERENCES mails(id) ON DELETE CASCADE,
    project_id      TEXT NOT NULL,
    user_id         TEXT NOT NULL,
    is_read         BOOLEAN NOT NULL DEFAULT FALSE,
    is_claimed      BOOLEAN NOT NULL DEFAULT FALSE,
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    claimed_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (mail_id, user_id)
);

CREATE INDEX IF NOT EXISTS idx_user_mails_inbox
    ON user_mails (project_id, user_id, is_deleted, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_user_mails_user
    ON user_mails (user_id, project_id);
