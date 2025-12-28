import { pgSchema, text, timestamp } from "drizzle-orm/pg-core";

const pgreflexSchema = pgSchema("pgreflex");

export const active_servers = pgreflexSchema
  .view("active_servers", {
    slot_name: text().notNull(),
    uri: text().array().notNull(),
    server_hostname: text().notNull(),
    created_at: timestamp().notNull(),
    shared_secret: text().notNull(),
  })
  .existing();
