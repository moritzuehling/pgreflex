import { active_servers } from "./pgreflex-schema";
import type { AnyPgDb } from "./drizzle";

export async function relfexConnection(db: unknown) {
  const servers = await (db as AnyPgDb).select().from(active_servers);

  if (servers.length === 0) {
    throw new Error("No pgreflex servers registered in db!");
  }

  const randomServer = servers[(servers.length * Math.random()) | 0];

  return await new Promise<WebSocket>((resolve, reject) => {
    const socket = new WebSocket(
      (randomServer.uri[0] + "/ws").replace("//", "/")
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
}
