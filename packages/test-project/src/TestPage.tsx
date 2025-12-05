import { useQuery } from "@tanstack/react-query";
import { trpc } from "./trpc";
import { useSubscription } from "@trpc/tanstack-react-query";

export function TestPage() {
  const pong = useQuery(trpc.ping.queryOptions());
  const subscription = useSubscription(
    trpc.testSubscription.subscriptionOptions({
      hello: "laaaaal",
    })
  );

  return (
    <div>
      Hello World!
      <br />
      {pong.data ?? "Loading!"}
      <br />
      {subscription.data}
    </div>
  );
}
