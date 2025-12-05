import { useQuery } from "@tanstack/react-query";
import { trpc } from "./trpc";

export function TestPage() {
  const pong = useQuery(trpc.ping.queryOptions());

  return (
    <div>
      Hello World!
      <br />
      {pong.data ?? "Loading!"}
    </div>
  );
}
