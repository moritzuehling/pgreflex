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

  get status(): object;
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
  });

  socket.on("secureConnect", () => {
    const remoteCert = socket.getPeerX509Certificate()!;
    const pin = computeSpkiPinBase64(remoteCert.raw!);
    if (pin !== server.certificate_hash) {
      console.error(
        "fail",
        `Server certificate pin mismatch. expected=${server.certificate_hash} got=${pin}`,
      );
      onConnectFail(
        new Error(
          `Server certificate pin mismatch. expected=${server.certificate_hash} got=${pin}`,
        ),
      );
      return;
    }
    console.log("[pgreflex] Server certificate validated, connected!");
    onConnected();
  });

  socket.on("error", (e) => {
    onConnectFail(e);
  });

  socket.on("close", (e) => {
    finishStream();
  });

  // Framing buffer
  let buf = Buffer.alloc(0);
  socket.on("data", (chunk: Buffer) => {
    buf = Buffer.concat([buf, chunk]);

    while (true) {
      if (buf.length < 4) break; // Need at least length prefix

      const length = buf.readInt32LE(0);

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

    get status() {
      return {
        remote: socket.remoteAddress,
        state: socket.readyState,
        bytesRead: socket.bytesRead,
        bytesWritten: socket.bytesWritten,
        encrypted: socket.encrypted,
        servername: socket.servername,
        errored: socket.errored && {
          name: socket.errored.name,
          message: socket.errored.message,
        },
        serverCertificate: computeSpkiPinBase64(
          socket.getPeerCertificate().raw,
        ),
        clientCertificate: cert.spkiSha256Base64,
      };
    },
  };
}
