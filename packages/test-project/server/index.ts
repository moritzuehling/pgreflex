import { createBunHttpHandler, createBunWSHandler } from "trpc-bun-adapter";
import { appRouter } from "./router";

const createContext = () => ({
  user: 1,
});

const bunHandler = createBunHttpHandler({
  router: appRouter,
  // optional arguments:
  endpoint: "/trpc", // Default to ""
  createContext,
  onError: console.error,
  batching: {
    enabled: true,
  },
  emitWsUpgrades: true,
});

const wsHandler = createBunWSHandler({
  router: appRouter,
  endpoint: "/trpc",
  createContext,
  onError: console.error,
});

Bun.serve({
  port: 3005,
  idleTimeout: 255,
  websocket: wsHandler,
  fetch: bunHandler,
});
