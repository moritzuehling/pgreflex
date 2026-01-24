import { desc } from "drizzle-orm";
import type { AnyPgDb } from "../drizzle";
import { clientAuthentications, servers } from "../pgreflexSchema";
import {
  generateClientCertificateP256,
  type ClientCertificate,
} from "./certificate";

export interface ConnectInfo {
  server: typeof servers.$inferSelect;
  cert: ClientCertificate;
}

export async function getConnectInfo(db: AnyPgDb): Promise<ConnectInfo> {
  const cert = await generateClientCertificateP256("pgreflex-client");

  // TODO: switch to activeServers :)
  const server = (
    await db.select().from(servers).orderBy(desc(servers.created_at)).limit(1)
  ).at(0);

  if (!server) {
    throw new Error("no pgreflex server available :(");
  }

  await db.insert(clientAuthentications).values({
    for_server_slot_name: server.slot_name,
    client_certificate_hash: cert.spkiSha256Base64,
  });

  return {
    cert,
    server,
  };
}
