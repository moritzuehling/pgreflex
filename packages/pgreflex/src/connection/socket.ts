// connect.ts (Node 22+ / Bun)
// TLS TCP client with SPKI pin check + Protobuf framing.

import tls from "node:tls";
import crypto from "node:crypto";
import type { ConnectInfo } from "./auth";
import { ClientToServer, ServerToClient } from "../generated/protocol";

export type EventType = "open" | "message" | "error" | "close";

export type MessageEvent = { data: ServerToClient };

export type ConnectResult = {
  addEventListener: (type: EventType, fn: (ev?: any) => void) => void;
  removeEventListener: (type: EventType, fn: (ev?: any) => void) => void;
  send: (msg: Omit<ClientToServer, "messageId">) => void;
  close: () => void;
};

function computeSpkiPinBase64(certDer: Buffer): string {
  const x509 = new crypto.X509Certificate(certDer);
  const spkiDer = x509.publicKey.export({
    type: "spki",
    format: "der",
  });
  return crypto.createHash("sha256").update(spkiDer).digest("base64");
}

export function connect({ cert, server }: ConnectInfo): ConnectResult {
  const listeners = new Map<EventType, Set<(ev?: any) => void>>();

  const emit = (type: EventType, ev?: any) => {
    const set = listeners.get(type);
    if (!set) return;
    for (const fn of set) {
      fn(ev);
    }
  };

  const addEventListener = (type: EventType, fn: (ev?: any) => void) => {
    let set = listeners.get(type);
    if (!set) listeners.set(type, (set = new Set()));
    set.add(fn);
  };

  const removeEventListener = (type: EventType, fn: (ev?: any) => void) => {
    listeners.get(type)?.delete(fn);
  };

  const socket = tls.connect({
    host: server.host,
    port: server.port,
    key: cert.privateKeyPem,
    cert: cert.certificatePem,
    servername: server.host,
    rejectUnauthorized: false,

    checkServerIdentity: (_hostname, cert) => {
      if (!cert?.raw) return new Error("No server certificate presented.");

      const pin = computeSpkiPinBase64(cert.raw);
      if (pin !== server.certificate_hash) {
        return new Error(
          `Server certificate pin mismatch. expected=${server.certificate_hash} got=${pin}`,
        );
      }
      return undefined;
    },
  });

  // Framing buffer
  let buf = Buffer.alloc(0);

  socket.on("secureConnect", () => emit("open"));
  socket.on("close", () => emit("close"));
  socket.on("error", (err) => emit("error", err));

  socket.on("data", (chunk: Buffer) => {
    buf = Buffer.concat([buf, chunk]);

    while (true) {
      if (buf.length < 4) break; // Need at least length prefix

      const length = buf.readInt32BE(0);
      if (buf.length < 4 + length) break;

      const msgBytes = buf.subarray(4, 4 + length);
      buf = buf.subarray(4 + length);

      try {
        const msg = ServerToClient.decode(msgBytes);
        emit("message", { data: msg } satisfies MessageEvent);
      } catch (e) {
        console.error("Failed to decode message", e);
        socket.destroy();
      }
    }
  });

  let messageId = 0;
  const lenParams = Buffer.alloc(4);
  return {
    addEventListener,
    removeEventListener,
    send: (msg) => {
      var bytes = ClientToServer.encode({
        ...msg,
        messageId: messageId++,
      }).finish();

      lenParams.writeInt32LE(bytes.length, 0);
      socket.write(lenParams);
      socket.write(bytes);
    },
    close: () => socket.end(),
  } satisfies ConnectResult;
}
