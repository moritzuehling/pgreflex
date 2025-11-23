import { randomUUIDv5, randomUUIDv7, sleep, sql } from "bun";
import { generateUser } from "./generateUser";

await sql`CREATE TABLE  IF NOT EXISTS "users" (
  "id" text,
  "name" text NOT NULL,
  "birthday" timestamp NOT NULL,
  "email" text,
  "address" text,
  "money_left" float8,
  "num_friends" int8,
  PRIMARY KEY ("id")
  );`;

const [{ cnt }] = await sql`SELECT count(*)::int4 as cnt FROM users`;

for (let i = cnt; i < 150; i++) {
  const { id, name, birthday, email, address, money_left, num_friends } =
    generateUser();

  await sql`INSERT INTO USERS (
    "id", "name", "birthday", "email", "address", "money_left", "num_friends"
  )
  VALUES
    (${id}, ${name}, ${birthday}, ${email}, ${address}, ${money_left}, ${num_friends})
  `;
}

console.log("Creating DB updates.");

while (true) {
  const [{ id }] =
    await sql`SELECT id FROM users ORDER BY id LIMIT 1 OFFSET 75`;

  const {
    id: new_id,
    name,
    birthday,
    email,
    address,
    money_left,
    num_friends,
  } = generateUser();
  await sql`UPDATE USERS SET
    id=${new_id},
    name=${name},
    birthday=${birthday},
    email=${email},
    address=${address},
    money_left=${money_left},
    num_friends=${num_friends}
  WHERE
    id=${id}
  `;

  await sleep(1000);
}
