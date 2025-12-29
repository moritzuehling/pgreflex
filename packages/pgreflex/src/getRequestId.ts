let currentRequestId = 0;

export function getRequestId() {
  return ++currentRequestId;
}
