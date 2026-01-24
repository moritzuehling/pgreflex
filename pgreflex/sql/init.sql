CREATE SCHEMA IF NOT EXISTS pgreflex;

CREATE UNLOGGED TABLE IF NOT EXISTS pgreflex.servers (
  slot_name TEXT PRIMARY KEY,
  host TEXT NOT NULL,
  port int NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  certificate_hash TEXT NOT NULL
);

CREATE UNLOGGED TABLE IF NOT EXISTS pgreflex.client_authentications (
  for_server_slot_name TEXT  NOT NULL,
  client_certificate_hash TEXT NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  expires_at TIMESTAMP NOT NULL DEFAULT (CURRENT_TIMESTAMP + '30s')
);

CREATE OR REPLACE VIEW
  pgreflex.active_servers
AS (
  SELECT
    s.*,
    rs.temporary,
    rs.active
  FROM
    pgreflex.servers s,
    pg_catalog.pg_replication_slots rs
  WHERE
    rs.slot_name = s.slot_name
)
