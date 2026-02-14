export function pushableIterator<T>() {
  const queue: T[] = [];

  let isFinished = false;
  let promise = Promise.withResolvers<void>();

  function push(el: T) {
    queue.push(el);
    promise.resolve();
  }

  function finish() {
    isFinished = true;
    promise.resolve();
  }

  async function* pushableIteratorInt() {
    while (queue.length > 0) {
      // We can do as T here, as we're checking the size
      yield queue.pop() as T;
    }

    if (isFinished) {
      return;
    }

    // We know we are empty at this instant, and JS is single-threaded
    // So, we can create a new promise right now, and if someone pushes, we'll know
    promise = Promise.withResolvers();
    await promise.promise;
  }

  return {
    push,
    finish,
    iterator: pushableIteratorInt(),
  };
}
