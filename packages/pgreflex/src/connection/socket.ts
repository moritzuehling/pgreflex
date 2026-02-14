// connect.ts (Node 22+ / Bun)
// TLS TCP client with SPKI pin check + Protobuf framing.

import tls from "node:tls";
import crypto from "node:crypto";
import type { ConnectInfo } from "./auth";
import { ClientToServer, ServerToClient } from "../generated/protocol";
import { pushableIterator } from "../util/pushableIterator";

function computeSpkiPinBase64(certDer: Buffer): string {
  const x509 = new crypto.X509Certificate(certDer);
  const spkiDer = x509.publicKey.export({
    type: "spki",
    format: "der",
  });
  return crypto.createHash("sha256").update(spkiDer).digest("base64");
}

export interface Connection {
  send(clientToServer: Omit<ClientToServer, "messageId">): boolean;
  messageIterator: AsyncGenerator<ServerToClient, void, void>;
  connected: Promise<void>;
}

export function connect({ cert, server }: ConnectInfo): Connection {
  const {
    finish: finishStream,
    iterator: messageIterator,
    push: pushMessage,
  } = pushableIterator<ServerToClient>();

  const {
    promise: connected,
    reject: onConnectFail,
    resolve: onConnected,
  } = Promise.withResolvers<void>();

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

  socket.on("secureConnect", () => {
    onConnected();
  });

  socket.on("close", (e) => {
    onConnectFail(e.error);
    finishStream();
  });

  // Framing buffer
  let buf = Buffer.alloc(0);
  socket.on("data", (chunk: Buffer) => {
    console.log("got bytes", chunk.byteLength);
    buf = Buffer.concat([buf, chunk]);

    while (true) {
      if (buf.length < 4) break; // Need at least length prefix

      const length = buf.readInt32LE(0);

      console.log("length", buf.slice(0, 4));

      if (buf.length < 4 + length) break;

      const msgBytes = buf.subarray(4, 4 + length);
      buf = buf.subarray(4 + length);

      try {
        const msg = ServerToClient.decode(msgBytes);
        pushMessage(msg);
      } catch (e) {
        console.error("Failed to decode message", e);
        socket.destroy();
      }
    }
  });

  function send(msg: ClientToServer) {
    if (socket.readyState !== "open") {
      return false;
    }

    var bytes = ClientToServer.encode({
      ...msg,
      messageId: messageId++,
    }).finish();

    try {
      lenParams.writeInt32LE(bytes.length, 0);
      socket.write(lenParams);
      socket.write(bytes);
    } catch (e) {
      console.error("Failed to write to stream", e);
      return false;
    }

    return true;
  }

  let messageId = 0;
  const lenParams = Buffer.alloc(4);
  return {
    send,
    messageIterator,
    connected,
  };
}
