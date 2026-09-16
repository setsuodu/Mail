-- Broadcast mails: is_broadcast = true means fan-out on player inbox list

ALTER TABLE mails
    ADD COLUMN IF NOT EXISTS is_broadcast BOOLEAN NOT NULL DEFAULT FALSE;

CREATE INDEX IF NOT EXISTS idx_mails_broadcast
    ON mails (project_id, is_broadcast, created_at DESC)
    WHERE is_broadcast = TRUE;
