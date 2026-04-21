import React, { useState } from "react";
import { useContentTypes } from "../useContentTypes";
import { BasePageProps } from "../types";

const getNs = (codeName: string) => codeName.split(".")[0] ?? "";

// ── Component ──────────────────────────────────────────────────────────────
export const ExportPage = (props: BasePageProps) => {
  const { items, loading, error } = useContentTypes(props.apiBaseUrl);
  const [search, setSearch]       = useState("");
  const [selected, setSelected]   = useState<Set<string>>(new Set());
  const [status, setStatus]       = useState<"idle" | "loading" | "success" | "error">("idle");

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
      const res = await fetch(`${props.apiBaseUrl}/export`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ codeNames: [...selected] }),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);

      // Trigger browser download
      const blob     = await res.blob();
      const url      = URL.createObjectURL(blob);
      const a        = document.createElement("a");
      a.href         = url;
      a.download     = `content-types-${new Date().toISOString().slice(0, 10)}.zip`;
      a.click();
      URL.revokeObjectURL(url);
      setStatus("success");
    } catch {
      setStatus("error");
    }
  };

  return (
    <div style={{ padding: 24, fontFamily: "system-ui, sans-serif" }}>
      {/* Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 18 }}>
        <div>
          <h2 style={{ margin: 0, fontSize: 17, fontWeight: 500 }}>Export Content Types</h2>
          <p style={{ margin: "3px 0 0", fontSize: 12, color: "#666" }}>
            Select types to export as a .zip package
          </p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button onClick={toggleAll} disabled={loading} style={btnStyle("default")}>
            {allSelected ? "Deselect all" : "Select all"}
          </button>
          <button
            onClick={handleExport}
            disabled={selected.size === 0 || status === "loading"}
            style={btnStyle("success", selected.size === 0 || status === "loading")}
          >
            {status === "loading" ? "Exporting..." : `Export${selected.size > 0 ? ` (${selected.size})` : ""}`}
          </button>
        </div>
      </div>

      {/* Alerts */}
      {status === "success" && <Alert type="success">Export complete — .zip downloaded successfully.</Alert>}
      {status === "error"   && <Alert type="error">Export failed. Check API connection.</Alert>}
      {error                && <Alert type="error">{error}</Alert>}

      {/* Search + count */}
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

      {/* Table */}
      {loading ? (
        <div style={{ padding: 40, textAlign: "center", color: "#aaa", fontSize: 13 }}>Loading...</div>
      ) : (
        <div style={{ border: "0.5px solid #e0e0e0", borderRadius: 10, overflow: "hidden" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
            <thead>
              <tr style={{ background: "#fafafa", borderBottom: "0.5px solid #e0e0e0" }}>
                <th style={{ width: 36, padding: "8px 10px" }}>
                  <input type="checkbox" checked={allSelected} onChange={toggleAll} style={{ accentColor: "#185fa5" }} />
                </th>
                <th style={thStyle("34%")}>Name</th>
                <th style={thStyle("30%")}>Code name</th>
                <th style={thStyle("18%")}>Namespace</th>
                <th style={{ ...thStyle("12%"), textAlign: "right" }}>Fields</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((ct, i) => {
                const checked = selected.has(ct.codeName);
                return (
                  <tr
                    key={ct.codeName}
                    onClick={() => toggleOne(ct.codeName)}
                    style={{
                      borderTop: "0.5px solid #e0e0e0",
                      background: checked ? "#eef5fc" : i % 2 ? "#fafafa" : "#fff",
                      cursor: "pointer",
                    }}
                  >
                    <td style={{ padding: "8px 10px" }}>
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggleOne(ct.codeName)}
                        onClick={(e) => e.stopPropagation()}
                        style={{ accentColor: "#185fa5" }}
                      />
                    </td>
                    <td style={tdStyle()}>{ct.name}</td>
                    <td style={{ ...tdStyle(), fontSize: 12, color: "#666", fontFamily: "monospace" }}>{ct.codeName}</td>
                    <td style={tdStyle()}>{getNs(ct.codeName)}</td>
                    <td style={{ ...tdStyle(), textAlign: "right", color: "#888" }}>{ct.fields.length}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

// ── Helpers ────────────────────────────────────────────────────────────────
const btnStyle = (variant: "default" | "success", disabled = false): React.CSSProperties => {
  const v = variant === "success"
    ? { background: "#0f6e56", color: "#fff", border: "0.5px solid #0f6e56" }
    : { background: "#fff",    color: "#1a1a1a", border: "0.5px solid #bbb" };
  return {
    ...v, padding: "7px 14px", borderRadius: 6, fontSize: 13,
    fontWeight: 500, cursor: disabled ? "not-allowed" : "pointer",
    opacity: disabled ? 0.45 : 1,
  };
};
const thStyle = (width: string): React.CSSProperties => ({
  padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width,
});
const tdStyle = (): React.CSSProperties => ({
  padding: "8px 12px", fontSize: 13, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
});

const Alert = ({ type, children }: { type: "success" | "error" | "info"; children: React.ReactNode }) => {
  const map = {
    success: { bg: "#eaf3de", border: "#c0dd97", color: "#3b6d11" },
    error:   { bg: "#fcebeb", border: "#f7c1c1", color: "#a32d2d" },
    info:    { bg: "#e6f1fb", border: "#b5d4f4", color: "#185fa5" },
  };
  const c = map[type];
  return (
    <div style={{
      padding: "11px 14px", marginBottom: 14, borderRadius: 7, fontSize: 13,
      background: c.bg, border: `0.5px solid ${c.border}`, color: c.color,
    }}>
      {children}
    </div>
  );
};
