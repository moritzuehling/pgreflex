import { useState } from "react";
import { trpc } from "./trpc";
import { useSubscription } from "@trpc/tanstack-react-query";
import { useMutation } from "@tanstack/react-query";

export function TestPage() {
  const [ts, setTs] = useState(+new Date());
  const mut = useMutation(trpc.testMutation.mutationOptions());

  const subscription = useSubscription(
    trpc.testSubscription.subscriptionOptions(undefined, {
      onData(data) {
        // eslint-disable-next-line react-hooks/purity
        console.log("delay", Date.now() - ts);
        // eslint-disable-next-line react-hooks/purity
        console.log("server->client", Date.now() - data.sentAt);
      },
    }),
  );

  return (
    <div>
      <button
        onClick={() => {
          const n = Date.now();
          console.log("setTs", n);
          setTs(n);
          mut.mutate({ ts: n });
        }}
      >
        Yay?
      </button>
      <br />
      Data:{" "}
      <pre>
        {subscription.data
          ? JSON.stringify(subscription.data, undefined, 2)
          : "no data yet :("}
      </pre>
    </div>
  );
}
