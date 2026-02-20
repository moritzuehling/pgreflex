// Backend (normal trpc, drizzle is used through `db` wrapped)
import { teamsTable } from "../schema";
import { publicProcedure, reflex } from "../trpc";

export const teamSubscription = reflex(publicProcedure, async ({ db }) => {
  return await db.selectSingle(teamsTable, [
    ["id", "==", "5tBTGcA8rfskJ1kKg3Yc"],
  ]);
});

import { db } from "../drizzle";
import { z } from "zod";
import { eq } from "drizzle-orm";
import { usersTable } from "../schema";

export const testSubscription = reflex(publicProcedure, async ({ db }) => {
  console.timeEnd("full update");
  console.timeEnd("after update finished");
  const user = await db.selectSingle(usersTable, [
    ["email", "==", "jenny@banani.co"],
  ]);

  return {
    receivedAfter: Date.now() - +user.createdAt,
    sentAt: Date.now(),
    user,
  };
});

export const testMutation = publicProcedure
  .input(
    z.object({
      ts: z.number(),
    }),
  )
  .mutation(async ({ input: { ts } }) => {
    console.time("full update");
    console.time("a");
    await db
      .update(usersTable)
      .set({
        createdAt: new Date(ts),
      })
      .where(eq(usersTable.email, "jenny@banani.co"));
    console.timeEnd("a");
    console.time("after update finished");
  });
