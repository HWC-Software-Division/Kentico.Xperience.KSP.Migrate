import * as React from "react";
import { ApiResponse, ContentType } from "./types";

const { useState, useEffect, useCallback } = React;

export function useContentTypes(apiBaseUrl: string) {
  const [items, setItems]     = useState<ContentType[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res  = await fetch(`${apiBaseUrl}/list`);
      const json: ApiResponse<ContentType[]> = await res.json();
      if (!json.success) throw new Error(json.error ?? "API error");
      setItems(json.data ?? []);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load content types");
    } finally {
      setLoading(false);
    }
  }, [apiBaseUrl]);

  useEffect(() => { void load(); }, [load]);

  return { items, loading, error, reload: load };
}
