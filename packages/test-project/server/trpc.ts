import { initTRPC } from "@trpc/server";
import { reflexTrpc } from "pgreflex";

const t = initTRPC.create();

export const router = t.router;
export const publicProcedure = t.procedure;

import { db } from "./drizzle";
export const reflex = reflexTrpc(db);
