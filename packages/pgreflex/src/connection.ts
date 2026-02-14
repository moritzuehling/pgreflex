import type { AnyPgDb } from "./drizzle";
import { getRandomString } from "./getRandomString";
import { getConnectInfo } from "./connection/auth";
import { connect, type Connection } from "./connection/socket";
import type { ConditionSet } from "./generated/protocol";

export type ReflexConnection = Awaited<ReturnType<typeof reflexConnection>>;
export type ReflexSubscribeTo = ReturnType<
  ReflexConnection["createGroup"]
>["subscribeTo"];

type FnMap = Map<string, () => void>;

export async function reflexConnection(db: AnyPgDb) {
  let currentSocket: Connection | null = null;

  const invalidateGroupFn = new Map<string, () => void>();
  const subscriptionSucceededFn = new Map<string, () => void>();
  keepConnectionsAlive(
    (c) => (currentSocket = c),
    db,
    invalidateGroupFn,
    subscriptionSucceededFn,
  );

  function createGroup() {
    // 72 bit will basically never collide
    // we can assume it to be globally unique, i guess
    // alternative? just increment, and have server do it connection-specific?
    const groupId = getRandomString(12);
    const { promise: invalidatedPromise, resolve: invalidateGroup } =
      Promise.withResolvers<void>();
    invalidateGroupFn.set(groupId, () => {
      invalidateGroupFn.delete(groupId);
      invalidateGroup();
    });

    function addSubscription(conditions: ConditionSet) {
      const subscriptionId = getRandomString(12);
      const hasSent = currentSocket?.send({
        addSubscriptionToGroup: {
          subscriptionId: subscriptionId,
          groupId,
          conditions,
        },
      });
      const { resolve, promise } = Promise.withResolvers<void>();
      if (hasSent ?? false) {
        subscriptionSucceededFn.set(subscriptionId, () => {
          resolve();
          subscriptionSucceededFn.delete(subscriptionId);
        });
      } else {
        // If the socket isn't open, we just pretend it succeeded
        // we'll invalidate upon (re-)connection.
        resolve();
      }
      return promise;
    }

    return {
      invalidated: invalidatedPromise,
      subscribeTo: addSubscription,

      get isInvalidated() {
        return !invalidateGroupFn.has(groupId);
      },
    };
  }

  return {
    createGroup,
  };
}

async function keepConnectionsAlive(
  setConnection: (c: Connection) => void,
  db: AnyPgDb,
  invalidateGroupFn: FnMap,
  subscriptionSucceededFn: FnMap,
) {
  while (true) {
    const c = connect(await getConnectInfo(db));
    console.log("reconnecting!");
    await handleConnection(c, invalidateGroupFn, subscriptionSucceededFn);
    console.log("connection lost, reconnecting");
  }
}

async function handleConnection(
  connection: Connection,
  invalidateGroupFn: FnMap,
  subscriptionSucceededFn: FnMap,
) {
  try {
    await connection.connected;
    resolveAndClear(subscriptionSucceededFn);
    resolveAndClear(invalidateGroupFn);
    invalidateGroupFn.clear();

    for await (const msg of connection.messageIterator) {
      if (msg.invalidateGroup) {
        invalidateGroupFn.get(msg.invalidateGroup.groupId)?.();
      } else if (msg.subscriptionAcknowledged) {
        subscriptionSucceededFn.get(
          msg.subscriptionAcknowledged.subscriptionId,
        )?.();
      }
    }

    // connection dead, rip
  } catch {
    console.error("error when processing stuff!");
  }
}

function resolveAndClear(map: FnMap) {
  for (const resolver of map.values()) {
    resolver();
  }
  map.clear();
}
