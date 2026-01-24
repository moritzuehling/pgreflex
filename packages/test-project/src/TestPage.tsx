import { trpc } from "./trpc";
import { useSubscription } from "@trpc/tanstack-react-query";

export function TestPage() {
  const subscription = useSubscription(
    trpc.testSubscription.subscriptionOptions(),
  );

  return (
    <div>
      Hello World!
      <br />
      Data:{" "}
      {subscription.data ? JSON.stringify(subscription.data) : "no data yet :("}
    </div>
  );
}
