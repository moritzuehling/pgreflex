import { publicProcedure } from "../trpc";
import { z } from "zod";
import { reflexSubscription } from "pgreflex/trpc";

let i = 0;
export const testSubscription = reflexSubscription(
  publicProcedure.input(z.object({ hello: z.string() })),
  async ({ input }) => {
    return input.hello + " " + ++i;
  }
);
