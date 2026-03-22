import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { TestPage } from "./TestPage";
import { queryClient, trpc, trpcClient } from "./trpc";
import { QueryClientProvider } from "@tanstack/react-query";
import "./index.css";

const TRPCProvider = trpc.Provider;
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <TRPCProvider client={trpcClient} queryClient={queryClient}>
      <QueryClientProvider client={queryClient}>
        <TestPage />
      </QueryClientProvider>
    </TRPCProvider>
  </StrictMode>,
);
