import { active_servers } from "./pgreflexSchema";
import type { AnyPgDb } from "./drizzle";
import { getRandomString } from "./getRandomString";
import type {
  ClientToServerMessage,
  ServerToClientMessage,
  WireConditionSet,
} from "./wire";
import { getRequestId } from "./getRequestId";

export type ReflexConnection = Awaited<ReturnType<typeof reflexConnection>>;
export type ReflexSubscribeTo = ReturnType<
  ReflexConnection["createGroup"]
>["subscribeTo"];
export async function reflexConnection(db: AnyPgDb) {
  const servers = await (db as AnyPgDb).select().from(active_servers);

  if (servers.length === 0) {
    throw new Error("No pgreflex servers registered in db!");
  }

  const randomServer = servers[(servers.length * Math.random()) | 0];

  // todo: switch to our socket implementation
  const socket = await new Promise<WebSocket>((resolve, reject) => {
    const socket = new WebSocket(
      (randomServer.uri[0] + "/ws").replace("//", "/"),
    );
    socket.onerror = reject;
    socket.onopen = () => {
      console.log("sending secret");
      socket.send(randomServer.shared_secret);
    };
    socket.onmessage = (msg) => {
      console.log("onmessage", msg.data);
      if (msg.data === "authenticated") {
        resolve(socket);
      }
    };
  });

  socket.addEventListener("message", (ev) => {
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
    if (socket.readyState !== WebSocket.OPEN) {
      console.log("socket is in", socket.readyState);
      throw new Error("not connected!");
    }

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
      if (socket.readyState !== WebSocket.OPEN) {
        throw new Error("not connected?");
      }
      const requestId = getRequestId();

      const { resolve, promise } = Promise.withResolvers<void>();
      subscriptionSucceededFn.set(requestId, () => {
        resolve();
        subscriptionSucceededFn.delete(requestId);
      });

      socket.send(
        JSON.stringify({
          messageType: "subscribe",
          requestId,
          groupId,
          conditionSet,
        } satisfies ClientToServerMessage),
      );

      return promise;
    }

    return {
      invalidated: promise,
      subscribeTo: addSubscription,

      get isInvalidated() {
        return invalidateGroupFn.has(groupId);
      },
    };
  }

  return {
    createGroup,
  };
}
