// Frontend
import { trpc } from "./trpc";

export function TestPage() {
  const team = trpc.teamSubscription.useSubscription();
  return <div>Jenny is in the team: {team.data?.name}</div>;
}
