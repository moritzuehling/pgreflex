import { ping } from "./procedures/ping";
import { router } from "./trpc";

export const appRouter = router({
  ping,
});

export type AppRouter = typeof appRouter;
