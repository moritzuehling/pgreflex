import type { TRPCProcedureBuilder } from "@trpc/server";
import type { UnsetMarker } from "@trpc/server/unstable-core-do-not-import";
import { reflexDb, type AnyPgDb, type ReflexDB } from "./drizzle";
import { reflexConnection } from "./connection";

// eslint-disable-next-line @typescript-eslint/no-unsafe-function-type
type ArgumentTypes<F extends Function> = F extends (...args: infer A) => unknown
  ? A
  : never;

export function reflexTrpc(db: AnyPgDb) {
  const connection = reflexConnection(db);

  return function reflex<
    TContext,
    TMeta,
    TContextOverrides,
    TInputIn,
    TInputOut,
    TSubOuput,
    TOutputIn,
  >(
    proc: TRPCProcedureBuilder<
      TContext,
      TMeta,
      TContextOverrides,
      TInputIn,
      TInputOut,
      UnsetMarker,
      UnsetMarker,
      false
    >,
    fn: (
      opts: ArgumentTypes<ArgumentTypes<typeof proc.subscription>[0]>[0] & {
        db: ReflexDB;
      },
    ) => TSubOuput,
  ) {
    return proc.subscription<AsyncGenerator<TSubOuput, void, void>>(
      async function* manageSubscription(opts) {
        const abortPromise = new Promise((res) =>
          opts.signal?.addEventListener("abort", res),
        );

        while (!opts.signal?.aborted) {
          using group = connection.createGroup();

          const newOpts = {
            ...opts,
            db: reflexDb(db, group.subscribeTo),
          };
          yield await fn(newOpts);
          await Promise.race([group.invalidated, abortPromise]);
        }
      },
    );
  };
}
