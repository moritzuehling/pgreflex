import { publicProcedure } from "../trpc";

export const ping = publicProcedure.query(() => "pong!");
