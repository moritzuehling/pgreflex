import { router } from "./trpc";
import { ping } from "./procedures/ping";
import { testSubscription } from "./procedures/testSubscription";

export const appRouter = router({
  ping,
  testSubscription,
});

export type AppRouter = typeof appRouter;
