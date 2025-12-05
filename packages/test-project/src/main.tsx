import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { TestPage } from "./TestPage";
import { queryClient } from "./trpc";
import { QueryClientProvider } from "@tanstack/react-query";
import "./index.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <TestPage />
    </QueryClientProvider>
  </StrictMode>
);
