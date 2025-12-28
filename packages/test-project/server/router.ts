import { router } from "./trpc";
import { ping } from "./procedures/ping";

export const appRouter = router({
  ping,
});

export type AppRouter = typeof appRouter;
