import { randomUUIDv7 } from "bun";

export function generateUser() {
  const name = randomName();
  return {
    id: randomUUIDv7("base64url"),
    name: name,
    birthday: new Date(Math.random() * Date.now()),
    email: name.replace(" ", ".").toLowerCase() + "@gmail.com",
    address: Math.random() > 0.5 ? "totally real place" : null,
    money_left: Math.random() * 100000,
    num_friends: (Math.random() * 50) | 0,
  };
}

function randomName() {
  return (
    firstnames[(Math.random() * firstnames.length) | 0] +
    " " +
    lastnames[(Math.random() * lastnames.length) | 0]
  );
}

const firstnames = [
  "John",
  "Jim",
  "Tom",
  "Markus",
  "Lukas",
  "Paul",
  "Hank",
  "William",
  "Tina",
  "Hannah",
  "Jenny",
  "Martha",
  "Karen",
  "Sabrina",
  "Lina",
  "Nina",
];

const lastnames = [
  "Johnson",
  "Smith",
  "Smithson",
  "Miller",
  "Butcher",
  "Tailor",
  "Potter",
  "Cutter",
  "Bricks",
];
