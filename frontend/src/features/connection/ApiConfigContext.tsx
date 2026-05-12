import { createContext, useContext, useEffect, useState } from "react";
import { ApiClient, type ApiConnectionConfig } from "../../api/client";

const STORAGE_KEY = "ragnaforge-admin-ui.connection";

interface ApiConfigContextValue {
  connection: ApiConnectionConfig;
  setConnection: (next: ApiConnectionConfig) => void;
  client: ApiClient;
  ready: boolean;
}

const defaultConnection: ApiConnectionConfig = {
  baseUrl: "http://127.0.0.1:5099",
  apiKey: ""
};

const ApiConfigContext = createContext<ApiConfigContextValue | null>(null);

export function ApiConfigProvider({ children }: { children: React.ReactNode }) {
  const [connection, setConnectionState] = useState<ApiConnectionConfig>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored) {
      return defaultConnection;
    }

    try {
      return { ...defaultConnection, ...(JSON.parse(stored) as Partial<ApiConnectionConfig>) };
    } catch {
      return defaultConnection;
    }
  });

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(connection));
  }, [connection]);

  const [client] = useState(() => new ApiClient(() => connection));

  return (
    <ApiConfigContext.Provider
      value={{
        connection,
        setConnection: setConnectionState,
        client,
        ready: Boolean(connection.baseUrl.trim() && connection.apiKey.trim())
      }}
    >
      {children}
    </ApiConfigContext.Provider>
  );
}

export function useApiConfig() {
  const context = useContext(ApiConfigContext);
  if (!context) {
    throw new Error("useApiConfig must be used within ApiConfigProvider.");
  }

  return context;
}
