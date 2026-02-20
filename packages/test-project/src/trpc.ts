import {
  createTRPCClient,
  httpLink,
  httpSubscriptionLink,
  splitLink,
} from "@trpc/client";
import { QueryClient } from "@tanstack/react-query";
import { createTRPCReact } from "@trpc/react-query";
import type { AppRouter } from "../server/router";

export const queryClient = new QueryClient();
const trpcClient = createTRPCClient<AppRouter>({
  links: [
    splitLink({
      condition: (op) => op.type == "subscription",
      true: httpSubscriptionLink({ url: "/trpc" }),
      false: httpLink({ url: "/trpc" }),
    }),
  ],
});

export const trpc = createTRPCReact<AppRouter>();
/*
export const trpc = createTRPCOptionsProxy<AppRouter>({
  client: trpcClient,
  queryClient,
});
*/
