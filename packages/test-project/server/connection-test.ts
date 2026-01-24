import { getConnectInfo } from "pgreflex/connection/auth";
import { connect } from "pgreflex/connection/socket";

import { db } from "./drizzle";

const ci = await getConnectInfo(db);
const c = connect(ci);

// It would be illegal to insert the certificate here.

c.addEventListener("open", () => {
  console.log("connected (TLS + pin OK)");
  c.send("hello from node/bun");
});

c.addEventListener("message", (e) => {
  console.log("server said:", e.data);
});

c.addEventListener("error", (err) => {
  console.error("connection error:", err);
});

c.addEventListener("close", () => {
  console.log("closed");
});
