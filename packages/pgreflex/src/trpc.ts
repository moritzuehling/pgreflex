import type {
  TRPCProcedureBuilder,
  TRPCSubscriptionProcedure,
} from "@trpc/server";
import type {
  inferTrackedOutput,
  UnsetMarker,
} from "@trpc/server/unstable-core-do-not-import";

type DefaultValue<TValue, TFallback> = TValue extends UnsetMarker
  ? TFallback
  : TValue;

// eslint-disable-next-line @typescript-eslint/no-unsafe-function-type
type ArgumentTypes<F extends Function> = F extends (...args: infer A) => unknown
  ? A
  : never;

export function subscribe<
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
    opts: ArgumentTypes<ArgumentTypes<typeof proc.subscription>[0]>[0]
  ) => TSubOuput
): TRPCSubscriptionProcedure<{
  input: DefaultValue<TInputIn, void>;
  output: AsyncIterable<inferTrackedOutput<Awaited<TSubOuput>>, void, any>;
  meta: TMeta;
}> {
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
