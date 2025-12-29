import { createBunHttpHandler } from "trpc-bun-adapter";
import { appRouter } from "./router";
import { db } from "./drizzle";
import { reflexConnection } from "pgreflex";

const createContext = () => ({
  user: 1,
});

const bunHandler = createBunHttpHandler({
  router: appRouter,
  // optional arguments:
  endpoint: "/trpc", // Default to ""
  createContext,
  onError: console.error,
  allowMethodOverride: true,
  batching: {
    enabled: true,
  },
  emitWsUpgrades: false, // pass true to upgrade to WebSocket
});

Bun.serve({
  port: 3005,
  fetch(request, response) {
    return (
      bunHandler(request, response) ??
      new Response(`Server running! ${request.url}`, { status: 404 })
    );
  },
});
console.log(await reflexConnection(db));
