import { sql } from "drizzle-orm";
import {
  integer,
  pgSchema,
  text,
  timestamp,
  boolean,
} from "drizzle-orm/pg-core";

const pgreflexSchema = pgSchema("pgreflex");

export const servers = pgreflexSchema.table("servers", {
  slot_name: text().primaryKey(),
  host: text().notNull(),
  port: integer().notNull(),
  created_at: timestamp().notNull().defaultNow(),
  certificate_hash: text().notNull(),
});
export const active_servers = pgreflexSchema
  .view("active_servers", {
    slot_name: text().notNull(),
    host: text().notNull(),
    port: integer().notNull(),
    created_at: timestamp().notNull(),
    certificate_hash: text().notNull(),
    temporary: boolean().notNull(),
    active: boolean().notNull(),
  })
  .existing();

export const clientAuthentications = pgreflexSchema.table(
  "client_authentications",
  {
    client_certificate_hash: text().notNull().primaryKey(),
    for_server_slot_name: text().notNull(),
    created_at: timestamp()
      .notNull()
      .default(sql`CURRENT_TIMESTAMP`),
    expires_at: timestamp()
      .notNull()
      .default(sql`CURRENT_TIMESTAMP + '30s'`),
  },
);
