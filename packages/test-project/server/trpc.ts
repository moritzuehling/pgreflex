import { initTRPC } from "@trpc/server";
import { reflexTrpc } from "pgreflex";
import { db } from "./drizzle";

const t = initTRPC.create();

export const router = t.router;
export const publicProcedure = t.procedure;

export const reflex = reflexTrpc(db);
