import type {
  TRPCProcedureBuilder,
  TRPCSubscriptionProcedure,
} from "@trpc/server";
import type {
  inferTrackedOutput,
  UnsetMarker,
} from "@trpc/server/unstable-core-do-not-import";
import { reflexDb, type AnyPgDb, type ReflexDB } from "./drizzle";
import { reflexConnection } from "./connection";

type DefaultValue<TValue, TFallback> = TValue extends UnsetMarker
  ? TFallback
  : TValue;

// eslint-disable-next-line @typescript-eslint/no-unsafe-function-type
type ArgumentTypes<F extends Function> = F extends (...args: infer A) => unknown
  ? A
  : never;

export function reflexTrpc<DB extends AnyPgDb>(db: AnyPgDb) {
  const connectionPromise = reflexConnection(db);

  return function reflex<
    TContext,
    TMeta,
    TContextOverrides,
    TInputIn,
    TInputOut,
    TSubOuput
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
        db: ReflexDB<DB>;
      }
    ) => TSubOuput
  ): TRPCSubscriptionProcedure<{
    input: DefaultValue<TInputIn, void>;
    output: AsyncIterable<inferTrackedOutput<Awaited<TSubOuput>>, void, any>;
    meta: TMeta;
  }> {
    return proc.subscription(async function* manageSubscription(opts) {
      const connection = await connectionPromise;

      while (!opts.signal?.aborted) {
        const group = await connection.createGroup();

        const newOpts = {
          ...opts,
          db: reflexDb(db, group.subscribeTo),
        };
        yield await fn(newOpts);
        await group.invalidated;
      }

      console.log("ending subscription");
      console.log(opts.signal?.reason);
    });
  };
}
