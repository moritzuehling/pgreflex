const allowedChars =
  "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_-";

if (allowedChars.length != 64)
  throw new Error(
    "allowed chars must be power of 2, or crypto prng will be biased."
  );

export function getRandomString(len: number) {
  const arr = new Uint8Array(len);
  globalThis.crypto.getRandomValues(arr);

  return [...arr].map((a) => allowedChars[a % allowedChars.length]).join("");
}
