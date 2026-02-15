import { db } from "./drizzle";
import { reflexConnection, reflexDb } from "pgreflex";
import { usersTable } from "./schema";

const connection = reflexConnection(db);

while (true) {
  const run = connection.createGroup();

  const myDb = reflexDb(db, run.subscribeTo);
  const res = await myDb.selectSingle(usersTable, [
    ["email", "==", "jenny@banani.co"],
  ]);
  console.log("Jenny is named", res.fullName);

  await run.invalidated;
}
