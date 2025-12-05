import { initTRPC } from "@trpc/server";
import { createBunHttpHandler } from "trpc-bun-adapter";

const t = initTRPC.create();

export const router = t.router({
  ping: t.procedure.query(() => "pong"),
});
const createContext = () => ({
  user: 1,
});

const bunHandler = createBunHttpHandler({
  router,
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
      new Response("Websocket Running", { status: 404 })
    );
  },
});
