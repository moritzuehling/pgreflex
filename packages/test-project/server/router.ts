import { router } from "./trpc";
import { ping } from "./procedures/ping";
import {
  teamSubscription,
  testMutation,
  testSubscription,
} from "./procedures/testSubscription";

export const appRouter = router({
  ping,
  testSubscription,
  testMutation,
  teamSubscription,
});

export type AppRouter = typeof appRouter;
