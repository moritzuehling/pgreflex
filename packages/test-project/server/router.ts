import { router } from "./trpc";
import { ping } from "./procedures/ping";
import { testMutation, testSubscription } from "./procedures/testSubscription";

export const appRouter = router({
  ping,
  testSubscription,
  testMutation,
});

export type AppRouter = typeof appRouter;
