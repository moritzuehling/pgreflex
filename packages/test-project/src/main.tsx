import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createTRPCClient, httpBatchLink } from "@trpc/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createTRPCOptionsProxy } from "@trpc/tanstack-react-query";
import { TestPage } from "./TestPage";
import type { AppRouter } from "../server/router";
import "./index.css";

const queryClient = new QueryClient();
const trpcClient = createTRPCClient<AppRouter>({
  links: [httpBatchLink({ url: "http://localhost:2022" })],
});
export const trpc = createTRPCOptionsProxy<AppRouter>({
  client: trpcClient,
  queryClient,
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <TestPage />
    </QueryClientProvider>
  </StrictMode>
);
