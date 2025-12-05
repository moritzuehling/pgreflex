import { publicProcedure } from "../trpc";
import { z } from "zod";
import type { TRPCProcedureBuilder } from "@trpc/server";

let i = 0;
export const testSubscription = subscribe(
  publicProcedure.input(z.object({ hello: z.string() })),
  async ({ input }) => {
    return input.hello + " " + ++i;
  }
);

type UnsetMarker = "unsetMarker" & {
  __brand: "unsetMarker";
};
// eslint-disable-next-line @typescript-eslint/no-unsafe-function-type
type ArgumentTypes<F extends Function> = F extends (...args: infer A) => unknown
  ? A
  : never;

function subscribe<
  TContext,
  TMeta,
  TContextOverrides,
  TInputIn,
  TInputOut,
  TOutputIn
>(
  proc: TRPCProcedureBuilder<
    TContext,
    TMeta,
    TContextOverrides,
    TInputIn,
    TInputOut,
    UnsetMarker,
    TOutputIn,
    false
  >,
  fn: (
    opts: ArgumentTypes<ArgumentTypes<typeof proc.subscription>[0]>[0]
  ) => void
) {
  return proc.subscription(async function* manageSubscription(opts) {
    while (!opts.signal?.aborted) {
      console.log("yielding :)");
      yield await fn(opts);
      await new Promise((res) => setTimeout(res, 1000));
    }

    console.log("ending subscription");
    console.log(opts.signal?.reason);
  });
}

export const test = publicProcedure.input(
  z.object({
    minAge: z.number(),
  })
);
