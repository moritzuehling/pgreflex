import {
  integer,
  pgEnum,
  pgTable,
  text,
  timestamp,
  boolean,
  varchar,
  index,
  primaryKey,
} from "drizzle-orm/pg-core";

const id = () => varchar({ length: 255 });

export const usersTable = pgTable("users", {
  id: id().primaryKey(),
  email: text().notNull(),
  image: text(),
  createdAt: timestamp().notNull().defaultNow(),
  fullName: text(),
  firstName: text(),
  lastName: text(),
  hostedDomain: text(),
  dubId: varchar({ length: 255 }),
  onboardingRole: text(),
  onboardingOrgSize: text(),
  country: text(),
});

export const subscriptionBillingInterval = pgEnum("billing_interval", [
  "year",
  "month",
  "week",
  "day",
  "not-recurring",
]);

export const subscriptionsTable = pgTable(
  "subscriptions",
  {
    userId: varchar({ length: 255 })
      .primaryKey()
      .references(() => usersTable.id, {
        onDelete: "cascade",
      }),
    isActive: boolean().notNull(),
    screenCount: integer().notNull(),
    lastScreenRecordTime: timestamp(),
    figmaExportCount: integer().notNull().default(0),
    lastFigmaExportTime: timestamp(),
    createdAt: timestamp().notNull().defaultNow(),
    emailPayment: text(),
    stripeSubscriptionId: varchar({ length: 255 }).unique(),
    billingInterval: subscriptionBillingInterval(),
    updatedAt: timestamp()
      .notNull()
      .defaultNow()
      .$onUpdate(() => new Date()),
  },
  (st) => [
    index("indiv_subscription_by_stripe_id").on(st.stripeSubscriptionId),
    index().on(st.userId),
  ],
);

export const teamSubscriptionsTable = pgTable(
  "teamSubscriptions",
  {
    teamId: id()
      .primaryKey()
      .references(() => teamsTable.id, { onDelete: "cascade" }),
    isActive: boolean().notNull(),
    stripeSubscriptionId: varchar({ length: 255 }).unique().notNull(),
    billingInterval: subscriptionBillingInterval(),
    emailPayment: text().notNull(),
    createdAt: timestamp().notNull().defaultNow(),
    updatedAt: timestamp()
      .notNull()
      .defaultNow()
      .$onUpdate(() => new Date()),
  },
  (st) => [index("team_subscription_by_stripe_id").on(st.stripeSubscriptionId)],
);

export const teamsTable = pgTable("teams", {
  id: id().primaryKey(),
  name: varchar({ length: 1024 }),
  avatarUrl: text(),
  createdAt: timestamp().notNull().defaultNow(),
  updatedAt: timestamp()
    .notNull()
    .defaultNow()
    .$onUpdate(() => new Date()),
});

export const teamRoleEnum = pgEnum("teamrole", ["owner", "editor"]);
export const teamMembersTable = pgTable(
  "team_members",
  {
    teamId: id()
      .notNull()
      .references(() => teamsTable.id, { onDelete: "cascade" }),
    userId: id()
      .notNull()
      .references(() => usersTable.id),
    role: teamRoleEnum().notNull(),
    createdAt: timestamp().notNull().defaultNow(),
    updatedAt: timestamp()
      .notNull()
      .defaultNow()
      .$onUpdate(() => new Date()),
  },
  (table) => [
    primaryKey({ columns: [table.teamId, table.userId] }),
    index().on(table.teamId),
    index().on(table.userId),
  ],
);

export const teamInviteStatusEnum = pgEnum("team_invite_status", [
  "pending",
  "accepted",
  "declined",
]);
export const teamInvitesTable = pgTable(
  "team_invites",
  {
    id: id().primaryKey(),
    teamId: id()
      .notNull()
      .references(() => teamsTable.id, { onDelete: "cascade" }),
    email: text().notNull(),
    role: teamRoleEnum().notNull(),
    status: teamInviteStatusEnum().notNull(),
    createdBy: id()
      .notNull()
      .references(() => usersTable.id, { onDelete: "cascade" }),

    createdAt: timestamp().notNull().defaultNow(),
    updatedAt: timestamp()
      .notNull()
      .defaultNow()
      .$onUpdate(() => new Date()),
    finalizedAt: timestamp(),
    finalizedBy: id().references(() => usersTable.id, {
      onDelete: "set null",
    }),
  },
  (tbl) => [
    index().on(tbl.email),
    index().on(tbl.teamId),
    index().on(tbl.finalizedBy),
    index().on(tbl.createdBy),
  ],
);
