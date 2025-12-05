import { createBunHttpHandler } from "trpc-bun-adapter";
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
  responseMeta() {
    return {
      status: 202,
      headers: {},
    };
  },
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
      new Response("Server Running", { status: 404 })
    );
  },
});
