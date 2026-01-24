// connect.ts (Node 22+ / Bun)
// TLS TCP client with SPKI pin check (base64 sha256 of SubjectPublicKeyInfo).
//
// Usage example at bottom.

import tls from "node:tls";
import crypto from "node:crypto";
import type { ConnectInfo } from "./auth";

export type EventType = "open" | "message" | "error" | "close";

export type MessageEvent = { data: string };

export type ConnectResult = {
  addEventListener: (type: EventType, fn: (ev?: any) => void) => void;
  removeEventListener: (type: EventType, fn: (ev?: any) => void) => void;
  send: (text: string) => void;
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

  // Newline-delimited UTF-8 messages for now (matches your C# StreamReader loop idea).
  let buf = "";

  socket.on("secureConnect", () => emit("open"));
  socket.on("close", () => emit("close"));
  socket.on("error", (err) => emit("error", err));
  socket.on("data", (data: Buffer) => {
    buf += data.toString("utf8");
    while (true) {
      const idx = buf.indexOf("\n");
      if (idx < 0) break;
      const line = buf.slice(0, idx);
      buf = buf.slice(idx + 1);
      emit("message", { data: line } satisfies MessageEvent);
    }
  });

  return {
    addEventListener,
    removeEventListener,
    send: (text: string) => {
      socket.write(text + "\n", "utf8");
    },
    close: () => socket.end(),
  };
}
