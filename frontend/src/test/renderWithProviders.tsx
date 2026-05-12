import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import type { ReactElement } from "react";
import { MemoryRouter } from "react-router-dom";
import { ApiConfigProvider } from "../features/connection/ApiConfigContext";

export function installConnection() {
  localStorage.setItem(
    "ragnaforge-admin-ui.connection",
    JSON.stringify({
      baseUrl: "http://127.0.0.1:5099",
      apiKey: "local-key",
    }),
  );
}

export function renderWithProviders(ui: ReactElement, route = "/") {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={client}>
      <ApiConfigProvider>
        <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
      </ApiConfigProvider>
    </QueryClientProvider>,
  );
}

