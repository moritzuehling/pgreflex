// Backend (normal trpc, drizzle is used through `db` wrapped)
import { teamsTable } from "../schema";
import { publicProcedure, reflex } from "../trpc";
import { z } from "zod";

const hi = publicProcedure.input(
  z.object({
    hello: z.string(),
  }),
);

export const teamSubscription = reflex(hi, async ({ db }) => {
  return await db.selectSingle(teamsTable, [
    ["id", "==", "5tBTGcA8rfskJ1kKg3Yc"],
  ]);
});
