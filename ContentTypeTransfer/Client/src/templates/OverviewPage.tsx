import React, { useState } from "react";
import { useContentTypes } from "../useContentTypes";
import { BasePageProps, ContentType } from "../types";

const getNs = (codeName: string) => codeName.split(".")[0] ?? "";

const NS_COLORS: Record<string, { bg: string; color: string; border: string }> = {
  HXC:       { bg: "#e6f1fb", color: "#185fa5", border: "#b5d4f4" },
  KSP:       { bg: "#e1f5ee", color: "#0f6e56", border: "#9fe1cb" },
  Ecommerce: { bg: "#faeeda", color: "#854f0b", border: "#fac775" },
  Legacy:    { bg: "#f1efe8", color: "#5f5e5a", border: "#d3d1c7" },
};

const Badge = ({ ns }: { ns: string }) => {
  const c = NS_COLORS[ns] ?? { bg: "#f1efe8", color: "#5f5e5a", border: "#d3d1c7" };
  return (
    <span style={{
      fontSize: 11, padding: "2px 8px", borderRadius: 4, fontWeight: 500,
      background: c.bg, color: c.color, border: `0.5px solid ${c.border}`,
    }}>
      {ns}
    </span>
  );
};

// ── Component ──────────────────────────────────────────────────────────────
export const OverviewPage = (props: BasePageProps) => {
  const { items, loading, error } = useContentTypes(props.apiBaseUrl);
  const [search, setSearch]       = useState("");

  const filtered = items.filter(
    (c) =>
      c.name.toLowerCase().includes(search.toLowerCase()) ||
      c.codeName.toLowerCase().includes(search.toLowerCase())
  );

  const countNs = (ns: string) => items.filter((c) => getNs(c.codeName) === ns).length;
  const avgFields = items.length
    ? Math.round(items.reduce((a, c) => a + c.fields.length, 0) / items.length)
    : 0;

  return (
    <div style={{ padding: 24, fontFamily: "system-ui, sans-serif" }}>
      {/* Header */}
      <div style={{ marginBottom: 18 }}>
        <h2 style={{ margin: 0, fontSize: 17, fontWeight: 500 }}>Content Type Overview</h2>
        <p style={{ margin: "3px 0 0", fontSize: 12, color: "#666" }}>
          All content types from the local XbyK database
        </p>
      </div>

      {/* Stats */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(4,1fr)", gap: 10, marginBottom: 18 }}>
        {[
          { label: "Total types",   value: loading ? "—" : items.length },
          { label: "HXC namespace", value: loading ? "—" : countNs("HXC") },
          { label: "KSP namespace", value: loading ? "—" : countNs("KSP") },
          { label: "Avg fields",    value: loading ? "—" : avgFields },
        ].map((s) => (
          <div key={s.label} style={{ background: "#f6f6f4", borderRadius: 7, padding: "12px 14px" }}>
            <div style={{ fontSize: 11, color: "#888", marginBottom: 4 }}>{s.label}</div>
            <div style={{ fontSize: 22, fontWeight: 500 }}>{String(s.value)}</div>
          </div>
        ))}
      </div>

      {/* Error */}
      {error && (
        <div style={{
          padding: "11px 14px", marginBottom: 14, borderRadius: 7,
          background: "#fcebeb", border: "0.5px solid #f7c1c1", color: "#a32d2d", fontSize: 13,
        }}>
          {error}
        </div>
      )}

      {/* Search */}
      <input
        type="text"
        placeholder="Search by name or code name..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        style={{
          width: "100%", marginBottom: 12, fontSize: 13, boxSizing: "border-box",
          padding: "7px 10px", borderRadius: 6, border: "0.5px solid #d0d0d0",
        }}
      />

      {/* Table */}
      {loading ? (
        <div style={{ padding: 40, textAlign: "center", color: "#aaa", fontSize: 13 }}>
          Loading from database...
        </div>
      ) : (
        <div style={{ border: "0.5px solid #e0e0e0", borderRadius: 10, overflow: "hidden" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
            <thead>
              <tr style={{ background: "#fafafa", borderBottom: "0.5px solid #e0e0e0" }}>
                <th style={thStyle("38%")}>Name</th>
                <th style={thStyle("32%")}>Code name</th>
                <th style={thStyle("16%")}>Namespace</th>
                <th style={{ ...thStyle("14%"), textAlign: "right" }}>Fields</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((ct, i) => (
                <tr key={ct.codeName} style={{ borderTop: "0.5px solid #e0e0e0", background: i % 2 ? "#fafafa" : "#fff" }}>
                  <td style={tdStyle()}>{ct.name}</td>
                  <td style={{ ...tdStyle(), fontSize: 12, color: "#666", fontFamily: "monospace" }}>{ct.codeName}</td>
                  <td style={tdStyle()}><Badge ns={getNs(ct.codeName)} /></td>
                  <td style={{ ...tdStyle(), textAlign: "right", color: "#888" }}>{ct.fields.length}</td>
                </tr>
              ))}
              {filtered.length === 0 && (
                <tr>
                  <td colSpan={4} style={{ padding: 28, textAlign: "center", color: "#aaa", fontSize: 13 }}>
                    No content types found
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

const thStyle = (width: string): React.CSSProperties => ({
  padding: "8px 12px", textAlign: "left", fontSize: 11,
  fontWeight: 500, color: "#888", width,
});
const tdStyle = (): React.CSSProperties => ({
  padding: "8px 12px", fontSize: 13,
  overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
});
