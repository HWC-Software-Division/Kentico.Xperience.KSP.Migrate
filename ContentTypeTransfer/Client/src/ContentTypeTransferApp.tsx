/**
 * ContentTypeTransferApp.tsx
 *
 * Single-file React admin UI for the Content Type Transfer module.
 * Registered as template "@contentTypeTransfer/OverviewPage" etc. in UIPages.cs
 *
 * Install deps:  npm i @kentico/xperience-admin-base
 */

import { useState, useEffect, useRef, useCallback } from "react";

// ── Types (mirror C# DTOs) ────────────────────────────────────────────────

interface ContentTypeField {
  name: string;
  dataType: string;
  isRequired: boolean;
  fieldType: string;
  caption: string | null;
}

interface ContentType {
  name: string;
  codeName: string;
  fields: ContentTypeField[];
}

interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
}

interface ImportResult {
  created: number;
  updated: number;
  errors: string[];
}

// ── Constants ─────────────────────────────────────────────────────────────

const API = "/api/content-type-transfer";

const NS_COLORS: Record<string, { bg: string; color: string; border: string }> = {
  HXC:      { bg: "#e6f1fb", color: "#185fa5", border: "#b5d4f4" },
  KSP:      { bg: "#e1f5ee", color: "#0f6e56", border: "#9fe1cb" },
  Ecommerce:{ bg: "#faeeda", color: "#854f0b", border: "#fac775" },
  Legacy:   { bg: "#f1efe8", color: "#5f5e5a", border: "#d3d1c7" },
};

// ── Helpers ───────────────────────────────────────────────────────────────

const getNamespace = (codeName: string) => codeName.split(".")[0];

const getNsColor = (ns: string) => NS_COLORS[ns] ?? NS_COLORS["Legacy"];

// ── Sub-components ────────────────────────────────────────────────────────

const Badge = ({ ns }: { ns: string }) => {
  const c = getNsColor(ns);
  return (
    <span style={{
      fontSize: 11, padding: "2px 8px", borderRadius: 4, fontWeight: 500,
      background: c.bg, color: c.color, border: `0.5px solid ${c.border}`,
    }}>
      {ns}
    </span>
  );
};

const Alert = ({
  type, children,
}: {
  type: "info" | "success" | "error";
  children: React.ReactNode;
}) => {
  const styles = {
    info:    { bg: "#e6f1fb", border: "#b5d4f4", color: "#185fa5" },
    success: { bg: "#eaf3de", border: "#c0dd97", color: "#3b6d11" },
    error:   { bg: "#fcebeb", border: "#f7c1c1", color: "#a32d2d" },
  }[type];
  return (
    <div style={{
      padding: "11px 14px", marginBottom: 14, borderRadius: 7, fontSize: 13,
      background: styles.bg, border: `0.5px solid ${styles.border}`, color: styles.color,
    }}>
      {children}
    </div>
  );
};

const ContentTypeTable = ({
  items,
  checkable = false,
  selected,
  onToggle,
}: {
  items: ContentType[];
  checkable?: boolean;
  selected?: Set<string>;
  onToggle?: (codeName: string) => void;
}) => (
  <div style={{ border: "0.5px solid #e0e0e0", borderRadius: 10, overflow: "hidden" }}>
    <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
      <thead>
        <tr style={{ background: "#fafafa", borderBottom: "0.5px solid #e0e0e0" }}>
          {checkable && (
            <th style={{ width: 36, padding: "8px 10px" }} />
          )}
          <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "38%" }}>Name</th>
          <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "30%" }}>Code name</th>
          <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "16%" }}>Namespace</th>
          <th style={{ padding: "8px 12px", textAlign: "right", fontSize: 11, fontWeight: 500, color: "#888" }}>Fields</th>
        </tr>
      </thead>
      <tbody>
        {items.map((ct, i) => {
          const ns = getNamespace(ct.codeName);
          const checked = selected?.has(ct.codeName) ?? false;
          return (
            <tr
              key={ct.codeName}
              onClick={() => onToggle?.(ct.codeName)}
              style={{
                borderTop: "0.5px solid #e0e0e0",
                background: checked ? "#eef5fc" : i % 2 ? "#fafafa" : "#fff",
                cursor: checkable ? "pointer" : "default",
              }}
            >
              {checkable && (
                <td style={{ padding: "8px 10px" }}>
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={() => onToggle?.(ct.codeName)}
                    onClick={(e) => e.stopPropagation()}
                    style={{ accentColor: "#185fa5" }}
                  />
                </td>
              )}
              <td style={{ padding: "8px 12px", fontSize: 13, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {ct.name}
              </td>
              <td style={{ padding: "8px 12px", fontSize: 12, color: "#666", fontFamily: "monospace", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {ct.codeName}
              </td>
              <td style={{ padding: "8px 12px" }}>
                <Badge ns={ns} />
              </td>
              <td style={{ padding: "8px 12px", fontSize: 13, color: "#888", textAlign: "right" }}>
                {ct.fields.length}
              </td>
            </tr>
          );
        })}
        {items.length === 0 && (
          <tr>
            <td colSpan={checkable ? 5 : 4} style={{ padding: 28, textAlign: "center", color: "#aaa", fontSize: 13 }}>
              No content types found
            </td>
          </tr>
        )}
      </tbody>
    </table>
  </div>
);

// ── useContentTypes hook ──────────────────────────────────────────────────

function useContentTypes() {
  const [items, setItems] = useState<ContentType[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${API}/list`);
      const json: ApiResponse<ContentType[]> = await res.json();
      if (!json.success) throw new Error(json.error ?? "API error");
      setItems(json.data ?? []);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load content types");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  return { items, loading, error, reload: load };
}

// ════════════════════════════════════════════════════════════════════════════
// PAGE COMPONENTS
// ════════════════════════════════════════════════════════════════════════════

// ── Overview Page ─────────────────────────────────────────────────────────

export function OverviewPage() {
  const { items, loading, error } = useContentTypes();
  const [search, setSearch] = useState("");

  const filtered = items.filter(
    (c) =>
      c.name.toLowerCase().includes(search.toLowerCase()) ||
      c.codeName.toLowerCase().includes(search.toLowerCase())
  );

  const hxcCount = items.filter((c) => getNamespace(c.codeName) === "HXC").length;
  const kspCount = items.filter((c) => getNamespace(c.codeName) === "KSP").length;
  const avgFields = items.length
    ? Math.round(items.reduce((a, c) => a + c.fields.length, 0) / items.length)
    : 0;

  return (
    <div style={{ padding: 24 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 20 }}>
        <div>
          <h2 style={{ margin: 0, fontSize: 17, fontWeight: 500 }}>Content Type Overview</h2>
          <p style={{ margin: "3px 0 0", fontSize: 12, color: "#666" }}>
            Manage export and import of content type definitions
          </p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          {/* In XbyK admin, navigation is handled via the sidebar — these are informational */}
          <span style={{ fontSize: 12, color: "#888", alignSelf: "center" }}>
            Use sidebar to navigate
          </span>
        </div>
      </div>

      {/* Stats */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(4,1fr)", gap: 10, marginBottom: 20 }}>
        {[
          ["Total types", loading ? "—" : items.length],
          ["HXC namespace", loading ? "—" : hxcCount],
          ["KSP namespace", loading ? "—" : kspCount],
          ["Avg fields", loading ? "—" : avgFields],
        ].map(([label, value]) => (
          <div key={String(label)} style={{ background: "#f6f6f4", borderRadius: 7, padding: "12px 14px" }}>
            <div style={{ fontSize: 11, color: "#888", marginBottom: 4 }}>{label}</div>
            <div style={{ fontSize: 22, fontWeight: 500 }}>{value}</div>
          </div>
        ))}
      </div>

      {error && <Alert type="error">{error}</Alert>}

      <input
        type="text"
        placeholder="Search by name or code name..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        style={{ width: "100%", marginBottom: 12, fontSize: 13, padding: "7px 10px", borderRadius: 6, border: "0.5px solid #d0d0d0", boxSizing: "border-box" }}
      />

      {loading ? (
        <div style={{ padding: 40, textAlign: "center", color: "#aaa", fontSize: 13 }}>Loading content types...</div>
      ) : (
        <ContentTypeTable items={filtered} />
      )}
    </div>
  );
}

// ── Export Page ───────────────────────────────────────────────────────────

export function ExportPage() {
  const { items, loading, error } = useContentTypes();
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [status, setStatus] = useState<"idle" | "loading" | "success" | "error">("idle");

  const filtered = items.filter(
    (c) =>
      c.name.toLowerCase().includes(search.toLowerCase()) ||
      c.codeName.toLowerCase().includes(search.toLowerCase())
  );

  const allSelected = filtered.length > 0 && filtered.every((c) => selected.has(c.codeName));

  const toggleAll = () => {
    const next = new Set(selected);
    if (allSelected) filtered.forEach((c) => next.delete(c.codeName));
    else filtered.forEach((c) => next.add(c.codeName));
    setSelected(next);
  };

  const toggleOne = (codeName: string) => {
    const next = new Set(selected);
    next.has(codeName) ? next.delete(codeName) : next.add(codeName);
    setSelected(next);
  };

  const handleExport = async () => {
    if (selected.size === 0 || status === "loading") return;
    setStatus("loading");

    try {
      const res = await fetch(`${API}/export`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ codeNames: [...selected] }),
      });

      if (!res.ok) throw new Error(`HTTP ${res.status}`);

      // Trigger browser download
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `content-types-export-${new Date().toISOString().slice(0, 10)}.zip`;
      a.click();
      URL.revokeObjectURL(url);

      setStatus("success");
    } catch {
      setStatus("error");
    }
  };

  return (
    <div style={{ padding: 24 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 18 }}>
        <div>
          <h2 style={{ margin: 0, fontSize: 17, fontWeight: 500 }}>Export Content Types</h2>
          <p style={{ margin: "3px 0 0", fontSize: 12, color: "#666" }}>Select content types to export as .zip</p>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <button onClick={toggleAll} disabled={loading} style={{ padding: "7px 14px", borderRadius: 6, border: "0.5px solid #bbb", background: "#fff", fontSize: 13, cursor: "pointer" }}>
            {allSelected ? "Deselect all" : "Select all"}
          </button>
          <button
            onClick={handleExport}
            disabled={selected.size === 0 || status === "loading"}
            style={{
              padding: "7px 14px", borderRadius: 6, border: "0.5px solid #0f6e56",
              background: "#0f6e56", color: "#fff", fontSize: 13, fontWeight: 500,
              cursor: selected.size === 0 ? "not-allowed" : "pointer",
              opacity: selected.size === 0 ? 0.45 : 1,
            }}
          >
            {status === "loading" ? "Exporting..." : `Export${selected.size > 0 ? ` (${selected.size})` : ""}`}
          </button>
        </div>
      </div>

      {status === "success" && <Alert type="success">Export complete — .zip file downloaded successfully.</Alert>}
      {status === "error"   && <Alert type="error">Export failed. Check API connection and try again.</Alert>}
      {error && <Alert type="error">{error}</Alert>}

      <div style={{ display: "flex", gap: 10, marginBottom: 12, alignItems: "center" }}>
        <input
          type="text"
          placeholder="Filter content types..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{ flex: 1, fontSize: 13, padding: "7px 10px", borderRadius: 6, border: "0.5px solid #d0d0d0" }}
        />
        {selected.size > 0 && (
          <span style={{ fontSize: 12, color: "#666", whiteSpace: "nowrap" }}>
            {selected.size} selected
          </span>
        )}
      </div>

      {loading ? (
        <div style={{ padding: 40, textAlign: "center", color: "#aaa", fontSize: 13 }}>Loading...</div>
      ) : (
        <ContentTypeTable
          items={filtered}
          checkable
          selected={selected}
          onToggle={toggleOne}
        />
      )}
    </div>
  );
}

// ── Import Page ───────────────────────────────────────────────────────────

export function ImportPage() {
  const fileRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [status, setStatus] = useState<"idle" | "loading" | "success" | "error">("idle");
  const [result, setResult] = useState<ImportResult | null>(null);

  const handleFile = (f: File | null) => {
    if (!f) return;
    setFile(f);
    setStatus("idle");
    setResult(null);
  };

  const handleImport = async () => {
    if (!file || status === "loading") return;
    setStatus("loading");

    const form = new FormData();
    form.append("file", file);

    try {
      const res = await fetch(`${API}/import`, { method: "POST", body: form });
      const json: ApiResponse<ImportResult> = await res.json();

      if (!json.success && !json.data) throw new Error(json.error ?? "Import failed");

      setResult(json.data ?? null);
      setStatus(json.success ? "success" : "error");
    } catch {
      setStatus("error");
    }
  };

  return (
    <div style={{ padding: 24 }}>
      <div style={{ marginBottom: 18 }}>
        <h2 style={{ margin: 0, fontSize: 17, fontWeight: 500 }}>Import Content Types</h2>
        <p style={{ margin: "3px 0 0", fontSize: 12, color: "#666" }}>
          Upload a .zip file to import content type definitions into XbyK
        </p>
      </div>

      {/* Drop zone */}
      <div
        onClick={() => fileRef.current?.click()}
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => { e.preventDefault(); handleFile(e.dataTransfer.files[0]); }}
        style={{
          border: `1.5px dashed ${file ? "#9fe1cb" : "#d0d0d0"}`,
          borderRadius: 10,
          background: file ? "#f0faf6" : "#fafafa",
          padding: "36px 20px",
          textAlign: "center",
          cursor: "pointer",
          marginBottom: 18,
        }}
      >
        <input
          ref={fileRef}
          type="file"
          accept=".zip"
          style={{ display: "none" }}
          onChange={(e) => handleFile(e.target.files?.[0] ?? null)}
        />
        {file ? (
          <>
            <div style={{ fontSize: 14, fontWeight: 500, color: "#0f6e56" }}>{file.name}</div>
            <div style={{ fontSize: 12, color: "#3b8c6a", marginTop: 3 }}>
              {(file.size / 1024).toFixed(1)} KB — click to change
            </div>
          </>
        ) : (
          <>
            <div style={{ fontSize: 14, fontWeight: 500, color: "#444" }}>
              Drop .zip file here or click to browse
            </div>
            <div style={{ fontSize: 12, color: "#aaa", marginTop: 3 }}>
              Supports content type export packages
            </div>
          </>
        )}
      </div>

      {/* Result */}
      {status === "success" && result && (
        <Alert type="success">
          <strong>Import complete</strong> — {result.created} created, {result.updated} updated.
          {result.errors.length > 0 && (
            <ul style={{ marginTop: 6, paddingLeft: 18 }}>
              {result.errors.map((e, i) => <li key={i}>{e}</li>)}
            </ul>
          )}
        </Alert>
      )}
      {status === "error" && (
        <Alert type="error">
          Import failed.{result?.errors.length ? ` Errors: ${result.errors.join(", ")}` : " Check API connection."}
        </Alert>
      )}
      {status === "loading" && (
        <Alert type="info">Processing — reading .zip and calling API...</Alert>
      )}

      <div style={{ display: "flex", gap: 8 }}>
        <button
          onClick={handleImport}
          disabled={!file || status === "loading"}
          style={{
            padding: "7px 16px", borderRadius: 6, border: "0.5px solid #185fa5",
            background: "#185fa5", color: "#fff", fontSize: 13, fontWeight: 500,
            cursor: !file ? "not-allowed" : "pointer",
            opacity: !file ? 0.45 : 1,
          }}
        >
          {status === "loading" ? "Importing..." : "Import"}
        </button>
        {file && (
          <button
            onClick={() => { setFile(null); setStatus("idle"); setResult(null); }}
            style={{ padding: "7px 14px", borderRadius: 6, border: "0.5px solid #bbb", background: "#fff", fontSize: 13, cursor: "pointer" }}
          >
            Clear
          </button>
        )}
      </div>
    </div>
  );
}

// ── Default export — used when rendering outside XbyK admin (e.g. Storybook) ──

export default function App() {
  const [page, setPage] = useState<"overview" | "export" | "import">("overview");
  return (
    <div style={{ display: "flex", minHeight: 500, border: "0.5px solid #e0e0e0", borderRadius: 10, overflow: "hidden", fontFamily: "system-ui,sans-serif" }}>
      <div style={{ width: 160, background: "#f6f6f4", borderRight: "0.5px solid #e0e0e0", padding: "16px 0" }}>
        {(["overview", "export", "import"] as const).map((p) => (
          <button
            key={p}
            onClick={() => setPage(p)}
            style={{
              display: "block", width: "100%", padding: "8px 14px", border: "none",
              background: page === p ? "#fff" : "transparent",
              borderLeft: page === p ? "2px solid #185fa5" : "2px solid transparent",
              fontSize: 13, cursor: "pointer", textAlign: "left",
              fontWeight: page === p ? 500 : 400, color: page === p ? "#1a1a1a" : "#666",
            }}
          >
            {p.charAt(0).toUpperCase() + p.slice(1)}
          </button>
        ))}
      </div>
      <div style={{ flex: 1, overflow: "auto" }}>
        {page === "overview" && <OverviewPage />}
        {page === "export"   && <ExportPage />}
        {page === "import"   && <ImportPage />}
      </div>
    </div>
  );
}
