import { db } from "./drizzle";
import { reflexConnection, reflexDb } from "pgreflex";
import { teamsTable } from "./schema";

const connection = reflexConnection(db);

while (true) {
  const group = connection.createGroup();
  const myDb = reflexDb(db, group.subscribeTo);
  const team = await myDb.selectSingle(teamsTable, [
    ["id", "==", "5tBTGcA8rfskJ1kKg3Yc"],
  ]);
  console.clear();
  console.log("Jenny is part of:");
  console.log(team);

  // this promise resolves as soon as any query in the subscription would change
  await group.invalidated;
  console.log("invalidated!");
}
