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
  .ksp-ct .ksp-mono   { color: #555 !important; }
  .ksp-ct .ksp-row:hover td { background: #f6f5f3 !important; }
  .ksp-ct .ksp-search:focus { outline: none; border-color: #185fa5 !important; }
`;

const SearchIcon = () => (
  <svg width="16" height="16" viewBox="0 0 16 16" fill="none" style={{ display: "block" }}>
    <circle cx="6.5" cy="6.5" r="4.5" stroke="#aaa" strokeWidth="1.4"/>
    <path d="M10 10L13.5 13.5" stroke="#aaa" strokeWidth="1.4" strokeLinecap="round"/>
  </svg>
);

function FlatTable({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ background: "#fff", borderRadius: 8, overflow: "hidden", border: "1px solid #e8e8e8" }}>
      <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
        {children}
      </table>
    </div>
  );
}

function Th({ children, style }: { children?: React.ReactNode; style?: React.CSSProperties }) {
  return (
    <th style={{ padding: "10px 16px", textAlign: "left", fontSize: 12, fontWeight: 500, color: "#888", borderBottom: "1px solid #e0e0e0", background: "#fafafa", ...style }}>
      {children}
    </th>
  );
}

function Td({ children, style }: { children?: React.ReactNode; style?: React.CSSProperties }) {
  return (
    <td style={{ padding: "13px 16px", fontSize: 14, color: "#1a1a1a", borderBottom: "1px solid #e8e8e8", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", ...style }}>
      {children}
    </td>
  );
}

function TdName({ children, style }: { children?: React.ReactNode; style?: React.CSSProperties }) {
  return (
    <td style={{ padding: "13px 16px", fontSize: 14, fontWeight: 600, color: "#1a1a1a", borderBottom: "1px solid #e8e8e8", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", ...style }}>
      {children}
    </td>
  );
}

export function OverviewPage(props: BasePageProps) {
  const [items,    setItems]    = useState<ContentType[]>([]);
  const [reusable, setReusable] = useState<ContentType[]>([]);
  const [schemas,  setSchemas]  = useState<FieldSchema[]>([]);
  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState<string | null>(null);
  const [search,   setSearch]   = useState("");
  const [nsFilter, setNsFilter] = useState("");

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const [r1, r2, r3] = await Promise.all([
        fetch(`${props.apiBaseUrl}/list`),
        fetch(`${props.apiBaseUrl}/list-reusable?includeLegacy=true`),
        fetch(`${props.apiBaseUrl}/list-field-schemas`),
      ]);
      const [j1, j2, j3]: [ApiResponse<ContentType[]>, ApiResponse<ContentType[]>, ApiResponse<FieldSchema[]>] =
        await Promise.all([r1.json(), r2.json(), r3.json()]);
      if (!j1.success) throw new Error(j1.error ?? "API error");
      if (!j2.success) throw new Error(j2.error ?? "API error");
      if (!j3.success) throw new Error(j3.error ?? "API error");
      setItems(j1.data ?? []);
      setReusable(j2.data ?? []);
      setSchemas(j3.data ?? []);
    } catch (e) { setError(e instanceof Error ? e.message : "Failed"); }
    finally { setLoading(false); }
  }, [props.apiBaseUrl]);

  useEffect(() => { void load(); }, [load]);

  const q = search.toLowerCase();
  const filteredBySearch = items.filter(c => c.name.toLowerCase().includes(q) || c.codeName.toLowerCase().includes(q));
  const filtered         = nsFilter ? filteredBySearch.filter(c => getNs(c.codeName) === nsFilter) : filteredBySearch;
  const filteredReusable = reusable.filter(c => c.name.toLowerCase().includes(q) || c.codeName.toLowerCase().includes(q));
  const filteredSchemas  = schemas.filter(s => s.displayName.toLowerCase().includes(q) || s.name.toLowerCase().includes(q));

  const hxcCount = items.filter(c => getNs(c.codeName) === "HXC").length;
  const kspCount = items.filter(c => getNs(c.codeName) === "KSP").length;

  return (
    <div className="ksp-ct" style={{ padding: 24, fontFamily: "system-ui, sans-serif" }}>
      <style>{STYLE}</style>

      {/* Header */}
      <h2 style={{ margin: "0 0 20px", fontSize: 18, fontWeight: 600, color: "#1a1a1a" }}>Content Type Overview</h2>

      {/* Stat cards */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(5,1fr)", gap: 10, marginBottom: 24 }}>
        {([
          { label: "Total types",   value: items.length,    ns: null  },
          { label: "HXC namespace", value: hxcCount,        ns: "HXC" },
          { label: "KSP namespace", value: kspCount,        ns: "KSP" },
          { label: "Reusable",      value: reusable.length, ns: null  },
          { label: "Field Schemas", value: schemas.length,  ns: null  },
        ] as { label: string; value: number; ns: string | null }[]).map(card => {
          const isActive = card.ns !== null && nsFilter === card.ns;
          const c = card.ns ? NS_COLORS[card.ns] : null;
          const isClickable = card.ns !== null;
          return (
            <div key={card.label}
              onClick={isClickable && !loading ? () => setNsFilter(p => p === card.ns ? "" : card.ns!) : undefined}
              style={{
                padding: "14px 16px", borderRadius: 8,
                background: isActive ? (c?.bg ?? "#f6f6f4") : "#fff",
                border: `1px solid ${isActive ? (c?.color ?? "#185fa5") : "#e8e8e8"}`,
                cursor: isClickable ? "pointer" : "default",
                transition: "border-color 0.15s, background 0.15s",
              }}>
              <div style={{ fontSize: 11, color: isActive ? c?.color : "#999", marginBottom: 6, fontWeight: 500, textTransform: "uppercase", letterSpacing: "0.04em" }}>
                {card.label}
              </div>
              <div style={{ fontSize: 24, fontWeight: 600, color: isActive ? c?.color : "#1a1a1a", lineHeight: 1 }}>
                {loading ? "—" : card.value}
              </div>
              {isClickable && !loading && (
                <div style={{ fontSize: 10, marginTop: 5, color: isActive ? c?.color : "#bbb" }}>
                  {isActive ? "click to clear" : "click to filter"}
                </div>
              )}
            </div>
          );
        })}
      </div>

      {error && (
        <div style={{ padding: "11px 16px", marginBottom: 16, borderRadius: 6, background: "#fcebeb", border: "1px solid #f7c1c1", color: "#a32d2d", fontSize: 13 }}>{error}</div>
      )}

      {/* Search */}
      <div style={{ position: "relative", marginBottom: 20 }}>
        <input className="ksp-search" type="text" placeholder="Search by name or code name…" value={search}
          onChange={e => setSearch(e.target.value)}
          style={{ width: "100%", fontSize: 14, boxSizing: "border-box", padding: "10px 42px 10px 16px", borderRadius: 24, border: "1px solid #d8d8d8", background: "#fff", fontFamily: "inherit" }}
        />
        <span style={{ position: "absolute", right: 14, top: "50%", transform: "translateY(-50%)", pointerEvents: "none" }}>
          <SearchIcon />
        </span>
      </div>

      {loading ? (
        <div style={{ padding: 48, textAlign: "center", color: "#aaa", fontSize: 14 }}>Loading from database…</div>
      ) : (
        <>
          {/* ── Content Types ── */}
          <div style={{ marginBottom: 28 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12 }}>
              <span style={{ fontSize: 14, fontWeight: 600, color: "#1a1a1a" }}>
                Content Types
                <span style={{ fontWeight: 400, color: "#999", marginLeft: 6 }}>
                  {nsFilter ? `${filtered.length} of ${items.length}` : filtered.length}
                </span>
              </span>
              {nsFilter && (() => {
                const c = NS_COLORS[nsFilter] ?? { bg: "#f1f1f1", color: "#444", border: "#d0d0d0" };
                return (
                  <span style={{ display: "inline-flex", alignItems: "center", gap: 5, fontSize: 11, padding: "3px 10px 3px 8px", borderRadius: 12, fontWeight: 500, background: c.bg, color: c.color, border: `1px solid ${c.border}` }}>
                    {nsFilter}
                    <button onClick={() => setNsFilter("")}
                      style={{ background: "none", border: "none", cursor: "pointer", padding: 0, color: c.color, fontSize: 14, lineHeight: 1, marginTop: -1 }}>×</button>
                  </span>
                );
              })()}
            </div>
            <FlatTable>
              <thead>
                <tr>
                  <Th style={{ width: "36%" }}>Content type name</Th>
                  <Th style={{ width: "30%" }}>Code name</Th>
                  <Th style={{ width: "20%" }}>Namespace</Th>
                  <Th style={{ width: "14%", textAlign: "right" }}>Fields</Th>
                </tr>
              </thead>
              <tbody>
                {filtered.map(ct => {
                  const ns = getNs(ct.codeName);
                  const c  = NS_COLORS[ns] ?? { bg: "#f1efe8", color: "#5f5e5a", border: "#d3d1c7" };
                  return (
                    <tr key={ct.codeName} className="ksp-row">
                      <TdName><span title={ct.name}>{ct.name}</span></TdName>
                      <Td><span className="ksp-mono" style={{ fontFamily: "monospace", fontSize: 12 }} title={ct.codeName}>{ct.codeName}</span></Td>
                      <Td>
                        <span style={{ fontSize: 11, padding: "2px 8px", borderRadius: 4, fontWeight: 500, background: c.bg, color: c.color, border: `1px solid ${c.border}` }}>{ns}</span>
                      </Td>
                      <Td style={{ textAlign: "right", color: "#888" }}>{ct.fields.length}</Td>
                    </tr>
                  );
                })}
                {filtered.length === 0 && (
                  <tr><td colSpan={4} style={{ padding: "32px 16px", textAlign: "center", color: "#bbb", fontSize: 14, borderBottom: "1px solid #eee" }}>No content types found</td></tr>
                )}
              </tbody>
            </FlatTable>
          </div>

          {/* ── Secondary tables: 2-col ── */}
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 24 }}>

            <div>
              <div style={{ fontSize: 14, fontWeight: 600, color: "#1a1a1a", marginBottom: 12 }}>
                Reusable Fields
                <span style={{ fontWeight: 400, color: "#999", marginLeft: 6 }}>{filteredReusable.length}</span>
              </div>
              <FlatTable>
                <thead>
                  <tr>
                    <Th style={{ width: "44%" }}>Name</Th>
                    <Th style={{ width: "38%" }}>Code name</Th>
                    <Th style={{ width: "18%", textAlign: "right" }}>Fields</Th>
                  </tr>
                </thead>
                <tbody>
                  {filteredReusable.map(ct => (
                    <tr key={ct.codeName} className="ksp-row">
                      <TdName><span title={ct.name}>{ct.name}</span></TdName>
                      <Td><span className="ksp-mono" style={{ fontFamily: "monospace", fontSize: 12 }} title={ct.codeName}>{ct.codeName}</span></Td>
                      <Td style={{ textAlign: "right", color: "#888" }}>{ct.fields.length}</Td>
                    </tr>
                  ))}
                  {filteredReusable.length === 0 && (
                    <tr><td colSpan={3} style={{ padding: "24px 16px", textAlign: "center", color: "#bbb", fontSize: 13, borderBottom: "1px solid #eee" }}>No reusable fields found</td></tr>
                  )}
                </tbody>
              </FlatTable>
            </div>

            <div>
              <div style={{ fontSize: 14, fontWeight: 600, color: "#1a1a1a", marginBottom: 12 }}>
                Field Schemas
                <span style={{ fontWeight: 400, color: "#999", marginLeft: 6 }}>{filteredSchemas.length}</span>
              </div>
              <FlatTable>
                <thead>
                  <tr>
                    <Th style={{ width: "44%" }}>Display name</Th>
                    <Th style={{ width: "38%" }}>Name</Th>
                    <Th style={{ width: "18%", textAlign: "right" }}>Fields</Th>
                  </tr>
                </thead>
                <tbody>
                  {filteredSchemas.map(s => (
                    <tr key={s.name} className="ksp-row">
                      <TdName><span title={s.displayName}>{s.displayName}</span></TdName>
                      <Td><span className="ksp-mono" style={{ fontFamily: "monospace", fontSize: 12 }} title={s.name}>{s.name}</span></Td>
                      <Td style={{ textAlign: "right", color: "#888" }}>{s.fieldCount}</Td>
                    </tr>
                  ))}
                  {filteredSchemas.length === 0 && (
                    <tr><td colSpan={3} style={{ padding: "24px 16px", textAlign: "center", color: "#bbb", fontSize: 13, borderBottom: "1px solid #eee" }}>No field schemas found</td></tr>
                  )}
                </tbody>
              </FlatTable>
            </div>

          </div>
        </>
      )}
    </div>
  );
}
