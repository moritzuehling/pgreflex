import type { AnyPgDb } from "./drizzle";
import { getRandomString } from "./getRandomString";
import { getRequestId } from "./getRequestId";
import { getConnectInfo } from "./connection/auth";
import { connect } from "./connection/socket";
import type { ServerToClientMessage, WireConditionSet } from "./wire";

export type ReflexConnection = Awaited<ReturnType<typeof reflexConnection>>;
export type ReflexSubscribeTo = ReturnType<
  ReflexConnection["createGroup"]
>["subscribeTo"];
export async function reflexConnection(db: AnyPgDb) {
  const connectInfo = await getConnectInfo(db);
  const socket = connect(connectInfo);

  socket.addEventListener("message", (ev) => {
    console.log("message", ev);
    const data = JSON.parse(ev.data) as ServerToClientMessage;
    switch (data.messageType) {
      case "invalidate":
        invalidateGroupFn.get(data.groupId)?.();
        break;
      case "subscribeSucess":
        subscriptionSucceededFn.get(data.requestId)?.();
        break;
    }
  });

  const invalidateGroupFn = new Map<string, () => void>();
  const subscriptionSucceededFn = new Map<number, () => void>();

  function createGroup() {
    // 72 bit will basically never collide
    // we can assume it to be globally unique, i guess
    // alternative? just increment, and have server do it connection-specific?
    const groupId = getRandomString(12);
    const { promise, resolve } = Promise.withResolvers<void>();
    invalidateGroupFn.set(groupId, () => {
      invalidateGroupFn.delete(groupId);
      resolve();
    });

    function addSubscription(conditionSet: WireConditionSet) {
      const requestId = getRequestId();

      const { resolve, promise } = Promise.withResolvers<void>();
      subscriptionSucceededFn.set(requestId, () => {
        resolve();
        subscriptionSucceededFn.delete(requestId);
      });
      return promise;
    }

    return {
      invalidated: promise,
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
