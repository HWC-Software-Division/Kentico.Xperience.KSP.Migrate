import React, { useState, useEffect, useCallback } from "react";
import { BasePageProps, ContentType, FieldSchema, ApiResponse } from "../types";

const getNs = (c: string) => c.split(".")[0] ?? "";
const NS_COLORS: Record<string, { bg: string; color: string; border: string }> = {
  HXC:    { bg: "#e6f1fb", color: "#185fa5", border: "#b5d4f4" },
  KSP:    { bg: "#e1f5ee", color: "#0f6e56", border: "#9fe1cb" },
  Legacy: { bg: "#fdf3e6", color: "#7c4f00", border: "#f5d79e" },
};

const STYLE = `
  .ksp-ct, .ksp-ct h2, .ksp-ct p, .ksp-ct td, .ksp-ct th, .ksp-ct div { color: inherit !important; }
  .ksp-ct { color: #1a1a1a !important; }
  .ksp-ct .ksp-muted  { color: #888 !important; }
  .ksp-ct .ksp-subtle { color: #666 !important; }
  .ksp-ct .ksp-mono   { color: #666 !important; }
`;

function SmallTable({ items, emptyMsg }: { items: ContentType[]; emptyMsg: string }) {
  return (
    <div style={{ border: "0.5px solid #e0e0e0", borderRadius: 10, overflow: "hidden" }}>
      <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
        <thead>
          <tr style={{ background: "#fafafa", borderBottom: "0.5px solid #e0e0e0" }}>
            <th style={{ padding: "7px 10px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "44%" }}>Name</th>
            <th style={{ padding: "7px 10px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "38%" }}>Code name</th>
            <th style={{ padding: "7px 10px", textAlign: "right", fontSize: 11, fontWeight: 500, color: "#888", width: "18%" }}>Fields</th>
          </tr>
        </thead>
        <tbody>
          {items.map((ct, i) => (
            <tr key={ct.codeName} style={{ borderTop: "0.5px solid #e0e0e0", background: i % 2 ? "#fafafa" : "#fff" }}>
              <td style={{ padding: "7px 10px", fontSize: 12, color: "#1a1a1a", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{ct.name}</td>
              <td className="ksp-mono" style={{ padding: "7px 10px", fontSize: 11, fontFamily: "monospace", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{ct.codeName}</td>
              <td style={{ padding: "7px 10px", fontSize: 12, textAlign: "right", color: "#888" }}>{ct.fields.length}</td>
            </tr>
          ))}
          {items.length === 0 && (
            <tr><td colSpan={3} style={{ padding: 20, textAlign: "center", color: "#aaa", fontSize: 12 }}>{emptyMsg}</td></tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

function SchemaTable({ items, emptyMsg }: { items: FieldSchema[]; emptyMsg: string }) {
  return (
    <div style={{ border: "0.5px solid #e0e0e0", borderRadius: 10, overflow: "hidden" }}>
      <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
        <thead>
          <tr style={{ background: "#fafafa", borderBottom: "0.5px solid #e0e0e0" }}>
            <th style={{ padding: "7px 10px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "44%" }}>Display name</th>
            <th style={{ padding: "7px 10px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "38%" }}>Name</th>
            <th style={{ padding: "7px 10px", textAlign: "right", fontSize: 11, fontWeight: 500, color: "#888", width: "18%" }}>Fields</th>
          </tr>
        </thead>
        <tbody>
          {items.map((s, i) => (
            <tr key={s.name} style={{ borderTop: "0.5px solid #e0e0e0", background: i % 2 ? "#fafafa" : "#fff" }}>
              <td style={{ padding: "7px 10px", fontSize: 12, color: "#1a1a1a", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{s.displayName}</td>
              <td className="ksp-mono" style={{ padding: "7px 10px", fontSize: 11, fontFamily: "monospace", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{s.name}</td>
              <td style={{ padding: "7px 10px", fontSize: 12, textAlign: "right", color: "#888" }}>{s.fieldCount}</td>
            </tr>
          ))}
          {items.length === 0 && (
            <tr><td colSpan={3} style={{ padding: 20, textAlign: "center", color: "#aaa", fontSize: 12 }}>{emptyMsg}</td></tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

export function OverviewPage(props: BasePageProps) {
  const [items,    setItems]    = useState<ContentType[]>([]);
  const [reusable, setReusable] = useState<ContentType[]>([]);
  const [schemas,  setSchemas]  = useState<FieldSchema[]>([]);
  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState<string | null>(null);
  const [search,   setSearch]   = useState("");

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const [r1, r2, r3] = await Promise.all([
        fetch(`${props.apiBaseUrl}/list`),
        fetch(`${props.apiBaseUrl}/list-reusable`),
        fetch(`${props.apiBaseUrl}/list-field-schemas`),
      ]);
      const [j1, j2, j3]: [
        ApiResponse<ContentType[]>,
        ApiResponse<ContentType[]>,
        ApiResponse<FieldSchema[]>
      ] = await Promise.all([r1.json(), r2.json(), r3.json()]);

      if (!j1.success) throw new Error(j1.error ?? "API error");
      if (!j2.success) throw new Error(j2.error ?? "API error");
      if (!j3.success) throw new Error(j3.error ?? "API error");

      setItems(j1.data ?? []);
      setReusable(j2.data ?? []);
      setSchemas(j3.data ?? []);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed");
    } finally { setLoading(false); }
  }, [props.apiBaseUrl]);

  useEffect(() => { void load(); }, [load]);

  const q = search.toLowerCase();
  const filtered         = items.filter(c =>
    c.name.toLowerCase().includes(q) || c.codeName.toLowerCase().includes(q));
  const filteredReusable = reusable.filter(c =>
    c.name.toLowerCase().includes(q) || c.codeName.toLowerCase().includes(q));
  const filteredSchemas  = schemas.filter(s =>
    s.displayName.toLowerCase().includes(q) || s.name.toLowerCase().includes(q));

  const hxcCount = items.filter(c => getNs(c.codeName) === "HXC").length;
  const kspCount = items.filter(c => getNs(c.codeName) === "KSP").length;

  return (
    <div className="ksp-ct" style={{ padding: 24, fontFamily: "system-ui, sans-serif" }}>
      <style>{STYLE}</style>

      {/* Header */}
      <div style={{ marginBottom: 18 }}>
        <h2 style={{ margin: 0, fontSize: 17, fontWeight: 500, color: "#1a1a1a" }}>Content Type Overview</h2>
        <p className="ksp-subtle" style={{ margin: "3px 0 0", fontSize: 12 }}>All content types from the local XbyK database</p>
      </div>

      {/* Stat cards */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(5,1fr)", gap: 10, marginBottom: 18 }}>
        {[
          { label: "Total types",    value: loading ? "—" : String(items.length) },
          { label: "HXC namespace",  value: loading ? "—" : String(hxcCount) },
          { label: "KSP namespace",  value: loading ? "—" : String(kspCount) },
          { label: "Reusable",       value: loading ? "—" : String(reusable.length) },
          { label: "Field Schemas",  value: loading ? "—" : String(schemas.length) },
        ].map(s => (
          <div key={s.label} style={{ background: "#f6f6f4", borderRadius: 7, padding: "12px 14px" }}>
            <div className="ksp-muted" style={{ fontSize: 11, marginBottom: 4 }}>{s.label}</div>
            <div style={{ fontSize: 22, fontWeight: 500 }}>{s.value}</div>
          </div>
        ))}
      </div>

      {error != null && (
        <div style={{ padding: "11px 14px", marginBottom: 14, borderRadius: 7, background: "#fcebeb", border: "0.5px solid #f7c1c1", color: "#a32d2d", fontSize: 13 }}>{error}</div>
      )}

      {/* Search */}
      <input
        type="text" placeholder="Search by name or code name..." value={search}
        onChange={e => setSearch(e.target.value)}
        style={{ width: "100%", marginBottom: 14, fontSize: 13, boxSizing: "border-box", padding: "7px 10px", borderRadius: 6, border: "0.5px solid #d0d0d0" }}
      />

      {loading ? (
        <div style={{ padding: 40, textAlign: "center", color: "#aaa", fontSize: 13 }}>Loading from database...</div>
      ) : (
        <div style={{ display: "flex", gap: 14, alignItems: "flex-start" }}>

          {/* ── 70% Content Types ── */}
          <div style={{ flex: 7, minWidth: 0 }}>
            <div style={{ fontSize: 13, fontWeight: 500, marginBottom: 8, color: "#1a1a1a" }}>
              Content Types
              <span className="ksp-muted" style={{ fontWeight: 400, marginLeft: 6 }}>({filtered.length})</span>
            </div>
            <div style={{ border: "0.5px solid #e0e0e0", borderRadius: 10, overflow: "hidden" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
                <thead>
                  <tr style={{ background: "#fafafa", borderBottom: "0.5px solid #e0e0e0" }}>
                    <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "36%" }}>Name</th>
                    <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "30%" }}>Code name</th>
                    <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "20%" }}>Namespace</th>
                    <th style={{ padding: "8px 12px", textAlign: "right", fontSize: 11, fontWeight: 500, color: "#888", width: "14%" }}>Fields</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((ct, i) => {
                    const ns = getNs(ct.codeName);
                    const c  = NS_COLORS[ns] ?? { bg: "#f1efe8", color: "#5f5e5a", border: "#d3d1c7" };
                    return (
                      <tr key={ct.codeName} style={{ borderTop: "0.5px solid #e0e0e0", background: i % 2 ? "#fafafa" : "#fff" }}>
                        <td style={{ padding: "8px 12px", fontSize: 13, color: "#1a1a1a", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{ct.name}</td>
                        <td className="ksp-mono" style={{ padding: "8px 12px", fontSize: 12, fontFamily: "monospace", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{ct.codeName}</td>
                        <td style={{ padding: "8px 12px", fontSize: 13 }}>
                          <span style={{ fontSize: 11, padding: "2px 7px", borderRadius: 4, fontWeight: 500, background: c.bg, color: c.color, border: `0.5px solid ${c.border}` }}>{ns}</span>
                        </td>
                        <td style={{ padding: "8px 12px", fontSize: 13, textAlign: "right", color: "#888" }}>{ct.fields.length}</td>
                      </tr>
                    );
                  })}
                  {filtered.length === 0 && (
                    <tr><td colSpan={4} style={{ padding: 28, textAlign: "center", color: "#aaa", fontSize: 13 }}>No content types found</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {/* ── 30% right panel: Reusable + Schemas ── */}
          <div style={{ flex: 3, minWidth: 0, display: "flex", flexDirection: "column", gap: 14 }}>

            {/* Reusable Fields */}
            <div>
              <div style={{ fontSize: 13, fontWeight: 500, marginBottom: 8, color: "#1a1a1a" }}>
                Reusable Fields
                <span className="ksp-muted" style={{ fontWeight: 400, marginLeft: 6 }}>({filteredReusable.length})</span>
              </div>
              <SmallTable items={filteredReusable} emptyMsg="No reusable fields found" />
            </div>

            {/* Field Schemas */}
            <div>
              <div style={{ fontSize: 13, fontWeight: 500, marginBottom: 8, color: "#1a1a1a" }}>
                Field Schemas
                <span className="ksp-muted" style={{ fontWeight: 400, marginLeft: 6 }}>({filteredSchemas.length})</span>
              </div>
              <SchemaTable items={filteredSchemas} emptyMsg="No field schemas found" />
            </div>

          </div>

        </div>
      )}
    </div>
  );
}
