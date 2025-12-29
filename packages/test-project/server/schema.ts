import {
  pgTable,
  text,
  timestamp,
  doublePrecision,
  bigint,
} from "drizzle-orm/pg-core";

export const users = pgTable("users", {
  id: text().primaryKey().notNull(),
  name: text().notNull(),
  birthday: timestamp().notNull(),
  email: text(),
  address: text(),
  moneyLeft: doublePrecision("money_left"),
  numFriends: bigint("num_friends", { mode: "number" }),
});
