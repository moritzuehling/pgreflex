import { publicProcedure } from "../trpc";
import { z } from "zod";
import { subscribe } from "pgreflex/trpc";

let i = 0;
export const testSubscription = subscribe(
  publicProcedure.input(z.object({ hello: z.string() })),
  async ({ input }) => {
    return input.hello + " " + ++i;
  }
);
