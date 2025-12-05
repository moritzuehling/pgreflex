import { initTRPC } from "@trpc/server";
import { createBunServeHandler, createBunWSHandler, type CreateBunContextOptions } from "trpc-bun-adapter";

const t = initTRPC.create();

export const router = t.router({
  ping: t.procedure.query(() => "pong"),
});
const createContext = (opts: CreateBunContextOptions) => ({});
const websocket = createBunWSHandler({
  router,
  createContext,
  onError: console.error,
  batching: {
    enabled: true,
  },
});
