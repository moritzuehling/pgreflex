import { users } from "../schema";
import { publicProcedure, reflex } from "../trpc";

export const testSubscription = reflex(publicProcedure, async ({ db }) => {
  return await db.select(users, [["moneyLeft", ">=", 50]]);
});
