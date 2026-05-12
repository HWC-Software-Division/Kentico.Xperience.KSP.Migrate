import React, { useState, useEffect, useCallback } from "react";
import { BasePageProps, ContentType, FieldSchema, SchemaDep, ApiResponse } from "../types";

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
  .ksp-ct .ksp-row-sel:hover td { background: #e4eef8 !important; }
  .ksp-ct .ksp-row-rf:hover td  { background: #f5ece0 !important; }
  .ksp-ct .ksp-row-sc:hover td  { background: #e8e6f8 !important; }
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
    <td style={{ padding: "11px 16px", fontSize: 14, color: "#1a1a1a", borderBottom: "1px solid #e8e8e8", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", ...style }}>
      {children}
    </td>
  );
}

function TdName({ children, style }: { children?: React.ReactNode; style?: React.CSSProperties }) {
  return (
    <td style={{ padding: "11px 16px", fontSize: 14, fontWeight: 600, color: "#1a1a1a", borderBottom: "1px solid #e8e8e8", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", ...style }}>
      {children}
    </td>
  );
}

interface DepItem { codeName: string; name: string; }
interface DepsModalData { reusable: DepItem[]; schemas: SchemaDep[]; }

export function ExportPage(props: BasePageProps) {
  const [items,           setItems]           = useState<ContentType[]>([]);
  const [reusableItems,   setReusableItems]   = useState<ContentType[]>([]);
  const [schemaItems,     setSchemaItems]     = useState<FieldSchema[]>([]);
  const [loading,         setLoading]         = useState(true);
  const [error,           setError]           = useState<string | null>(null);
  const [search,          setSearch]          = useState("");
  const [nsFilter,        setNsFilter]        = useState("");
  const [selected,        setSelected]        = useState<Set<string>>(new Set());
  const [selectedReusable,setSelectedReusable]= useState<Set<string>>(new Set());
  const [selectedSchemas, setSelectedSchemas] = useState<Set<string>>(new Set());
  const [status,              setStatus]              = useState<"idle"|"loading"|"success"|"error">("idle");
  const [depsModal,           setDepsModal]           = useState<DepsModalData | null>(null);
  const [modalReusableChecked,setModalReusableChecked]= useState<Set<string>>(new Set());
  const [modalSchemasChecked, setModalSchemasChecked] = useState<Set<string>>(new Set());

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
      setReusableItems(j2.data ?? []);
      setSchemaItems(j3.data ?? []);
    } catch (e) { setError(e instanceof Error ? e.message : "Failed"); }
    finally { setLoading(false); }
  }, [props.apiBaseUrl]);

  useEffect(() => { void load(); }, [load]);

  const checkDeps = useCallback(async (codeNames: string[], currentReusable: Set<string>, currentSchemas: Set<string>) => {
    if (codeNames.length === 0) return;
    try {
      const body = JSON.stringify(codeNames);
      const headers = { "Content-Type": "application/json" };
      const [r1, r2] = await Promise.all([
        fetch(`${props.apiBaseUrl}/reusable-deps`, { method: "POST", headers, body }),
        fetch(`${props.apiBaseUrl}/schema-deps`,   { method: "POST", headers, body }),
      ]);
      const [j1, j2]: [ApiResponse<string[]>, ApiResponse<SchemaDep[]>] = await Promise.all([r1.json(), r2.json()]);
      const newReusable = (j1.data ?? []).filter(cn => !currentReusable.has(cn));
      const newSchemas  = (j2.data ?? []).filter(s  => !currentSchemas.has(s.name));
      if (newReusable.length === 0 && newSchemas.length === 0) return;
      setDepsModal({
        reusable: newReusable.map(cn => ({ codeName: cn, name: reusableItems.find(r => r.codeName === cn)?.name ?? cn })),
        schemas: newSchemas,
      });
      // Pre-check all deps by default
      setModalReusableChecked(new Set(newReusable));
      setModalSchemasChecked(new Set(newSchemas.map(s => s.name)));
    } catch {}
  }, [props.apiBaseUrl, reusableItems]);

  const namespaces = [...new Set(items.map(c => getNs(c.codeName)))].sort();

  const q                = search.toLowerCase();
  const filteredBySearch = items.filter(c => c.name.toLowerCase().includes(q) || c.codeName.toLowerCase().includes(q));
  const filtered         = nsFilter ? filteredBySearch.filter(c => getNs(c.codeName) === nsFilter) : filteredBySearch;
  const filteredReusable = reusableItems.filter(c => c.name.toLowerCase().includes(q) || c.codeName.toLowerCase().includes(q));
  const filteredSchemas  = schemaItems.filter(s => s.displayName.toLowerCase().includes(q) || s.name.toLowerCase().includes(q));

  const allCtSelected  = filtered.length > 0 && filtered.every(c => selected.has(c.codeName));
  const allRfSelected  = filteredReusable.length > 0 && filteredReusable.every(c => selectedReusable.has(c.codeName));
  const allSchSelected = filteredSchemas.length > 0 && filteredSchemas.every(s => selectedSchemas.has(s.name));

  const toggleOne = (codeName: string) => {
    const isAdding = !selected.has(codeName);
    setSelected(prev => { const s = new Set(prev); isAdding ? s.add(codeName) : s.delete(codeName); return s; });
    if (isAdding) void checkDeps([codeName], selectedReusable, selectedSchemas);
  };
  const toggleAll = () => {
    const adding = !allCtSelected;
    const newOnes = adding ? filtered.filter(c => !selected.has(c.codeName)).map(c => c.codeName) : [];
    setSelected(prev => { const s = new Set(prev); adding ? filtered.forEach(c => s.add(c.codeName)) : filtered.forEach(c => s.delete(c.codeName)); return s; });
    if (adding && newOnes.length > 0) void checkDeps(newOnes, selectedReusable, selectedSchemas);
  };
  const toggleOneReusable = (cn: string) =>
    setSelectedReusable(prev => { const s = new Set(prev); s.has(cn) ? s.delete(cn) : s.add(cn); return s; });
  const toggleAllReusable = () =>
    setSelectedReusable(prev => { const s = new Set(prev); allRfSelected ? filteredReusable.forEach(c => s.delete(c.codeName)) : filteredReusable.forEach(c => s.add(c.codeName)); return s; });
  const toggleOneSchema = (name: string) =>
    setSelectedSchemas(prev => { const s = new Set(prev); s.has(name) ? s.delete(name) : s.add(name); return s; });
  const toggleAllSchemas = () =>
    setSelectedSchemas(prev => { const s = new Set(prev); allSchSelected ? filteredSchemas.forEach(sc => s.delete(sc.name)) : filteredSchemas.forEach(sc => s.add(sc.name)); return s; });

  const handleExport = async () => {
    if ((selected.size === 0 && selectedReusable.size === 0 && selectedSchemas.size === 0) || status === "loading") return;
    setStatus("loading");
    try {
      const res = await fetch(`${props.apiBaseUrl}/export`, {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ codeNames: [...selected], reusableCodeNames: [...selectedReusable], schemaNames: [...selectedSchemas] }),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const blob = await res.blob();
      const url  = URL.createObjectURL(blob);
      const cd   = res.headers.get("content-disposition") ?? "";
      let fileName = "export.zip";
      const m1 = cd.match(/filename\*=UTF-8''(.+)/);
      const m2 = cd.match(/filename="?(.+?)"?($|;)/);
      if (m1?.[1]) fileName = decodeURIComponent(m1[1]);
      else if (m2?.[1]) fileName = m2[1];
      const a = document.createElement("a"); a.href = url; a.download = fileName; a.click();
      URL.revokeObjectURL(url);
      setStatus("success");
    } catch { setStatus("error"); }
  };

  const totalSelected = selected.size + selectedReusable.size + selectedSchemas.size;
  const btnSm = { padding: "6px 14px", borderRadius: 6, fontSize: 12, fontWeight: 500 as const, cursor: "pointer", border: "1px solid #d8d8d8", background: "#fff", color: "#444" };

  return (
    <div className="ksp-ct" style={{ padding: 24, fontFamily: "system-ui, sans-serif" }}>
      <style>{STYLE}</style>

      {/* ── Dependency modal ── */}
      {depsModal && (
        <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.4)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 1000 }}>
          <div style={{ background: "#fff", borderRadius: 12, padding: 24, width: 480, maxWidth: "90vw", maxHeight: "80vh", overflow: "auto", boxShadow: "0 8px 32px rgba(0,0,0,0.18)" }}>
            <h3 style={{ margin: "0 0 6px", fontSize: 16, fontWeight: 600, color: "#1a1a1a" }}>Dependencies Detected</h3>
            <p style={{ margin: "0 0 16px", fontSize: 14, color: "#666" }}>The selected content types use the following. Choose which to include in the export.</p>

            {depsModal.reusable.length > 0 && (
              <div style={{ marginBottom: 16 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: "#7c4f00", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 8 }}>
                  Reusable Fields ({modalReusableChecked.size}/{depsModal.reusable.length} selected)
                </div>
                <div style={{ background: "#fdf8f2", border: "1px solid #f0dfc0", borderRadius: 8, overflow: "hidden" }}>
                  {depsModal.reusable.map((d, i) => {
                    const checked = modalReusableChecked.has(d.codeName);
                    return (
                      <label key={d.codeName} onClick={e => e.stopPropagation()}
                        style={{ display: "flex", alignItems: "center", gap: 10, padding: "9px 14px", cursor: "pointer",
                          borderBottom: i < depsModal.reusable.length - 1 ? "1px solid #f0dfc0" : "none",
                          background: checked ? "#fdf3e6" : "#fdf8f2" }}>
                        <input type="checkbox" checked={checked} style={{ accentColor: "#7c4f00", flexShrink: 0 }}
                          onChange={() => setModalReusableChecked(prev => { const s = new Set(prev); s.has(d.codeName) ? s.delete(d.codeName) : s.add(d.codeName); return s; })} />
                        <span style={{ fontSize: 14, fontWeight: 500, color: "#1a1a1a", flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{d.name}</span>
                        <span style={{ fontSize: 11, fontFamily: "monospace", color: "#999", flexShrink: 0 }}>{d.codeName}</span>
                      </label>
                    );
                  })}
                </div>
              </div>
            )}

            {depsModal.schemas.length > 0 && (
              <div style={{ marginBottom: 20 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: "#4f46e5", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 8 }}>
                  Field Schemas ({modalSchemasChecked.size}/{depsModal.schemas.length} selected)
                </div>
                <div style={{ background: "#f5f4ff", border: "1px solid #d4d0f8", borderRadius: 8, overflow: "hidden" }}>
                  {depsModal.schemas.map((s, i) => {
                    const checked = modalSchemasChecked.has(s.name);
                    return (
                      <label key={s.name} onClick={e => e.stopPropagation()}
                        style={{ display: "flex", alignItems: "center", gap: 10, padding: "9px 14px", cursor: "pointer",
                          borderBottom: i < depsModal.schemas.length - 1 ? "1px solid #d4d0f8" : "none",
                          background: checked ? "#eeebff" : "#f5f4ff" }}>
                        <input type="checkbox" checked={checked} style={{ accentColor: "#4f46e5", flexShrink: 0 }}
                          onChange={() => setModalSchemasChecked(prev => { const s2 = new Set(prev); s2.has(s.name) ? s2.delete(s.name) : s2.add(s.name); return s2; })} />
                        <span style={{ fontSize: 14, fontWeight: 500, color: "#1a1a1a", flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{s.displayName}</span>
                        <span style={{ fontSize: 11, fontFamily: "monospace", color: "#999", flexShrink: 0 }}>{s.name}</span>
                      </label>
                    );
                  })}
                </div>
              </div>
            )}

            <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
              <button onClick={() => setDepsModal(null)}
                style={{ padding: "8px 16px", borderRadius: 6, fontSize: 13, fontWeight: 500, cursor: "pointer", border: "1px solid #d8d8d8", background: "#fff", color: "#444" }}>
                Skip all
              </button>
              <button onClick={() => {
                setSelectedReusable(prev => { const s = new Set(prev); modalReusableChecked.forEach(cn => s.add(cn)); return s; });
                setSelectedSchemas(prev => { const s = new Set(prev); modalSchemasChecked.forEach(n => s.add(n)); return s; });
                setDepsModal(null);
              }} style={{ padding: "8px 16px", borderRadius: 6, fontSize: 13, fontWeight: 500, cursor: "pointer", background: "#185fa5", border: "1px solid #185fa5", color: "#fff" }}>
                Add selected ({modalReusableChecked.size + modalSchemasChecked.size})
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Header ── */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 }}>
        <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: "#1a1a1a" }}>Export Content Types</h2>
        <button onClick={handleExport} disabled={totalSelected === 0 || status === "loading"}
          style={{ padding: "9px 20px", borderRadius: 6, fontSize: 14, fontWeight: 600, cursor: totalSelected === 0 ? "not-allowed" : "pointer", background: "#185fa5", color: "#fff", border: "none", opacity: totalSelected === 0 ? 0.45 : 1 }}>
          {status === "loading" ? "Exporting…" : `Export${totalSelected > 0 ? ` (${totalSelected})` : ""}`}
        </button>
      </div>

      {status === "success" && <div style={{ padding: "11px 16px", marginBottom: 14, borderRadius: 6, background: "#eaf3de", border: "1px solid #c0dd97", color: "#3b6d11", fontSize: 13 }}>Export complete — .zip downloaded.</div>}
      {status === "error"   && <div style={{ padding: "11px 16px", marginBottom: 14, borderRadius: 6, background: "#fcebeb", border: "1px solid #f7c1c1", color: "#a32d2d", fontSize: 13 }}>Export failed. Check API connection.</div>}
      {error &&               <div style={{ padding: "11px 16px", marginBottom: 14, borderRadius: 6, background: "#fcebeb", border: "1px solid #f7c1c1", color: "#a32d2d", fontSize: 13 }}>{error}</div>}

      {/* ── Search ── */}
      <div style={{ position: "relative", marginBottom: 14 }}>
        <input className="ksp-search" type="text" placeholder="Filter by name…" value={search}
          onChange={e => setSearch(e.target.value)}
          style={{ width: "100%", fontSize: 14, boxSizing: "border-box", padding: "10px 42px 10px 16px", borderRadius: 24, border: "1px solid #d8d8d8", background: "#fff", fontFamily: "inherit" }}
        />
        <span style={{ position: "absolute", right: 14, top: "50%", transform: "translateY(-50%)", pointerEvents: "none" }}><SearchIcon /></span>
      </div>

      {loading ? (
        <div style={{ padding: 48, textAlign: "center", color: "#aaa", fontSize: 14 }}>Loading…</div>
      ) : (<>

        {/* ── Namespace filter pills ── */}
        {namespaces.length > 1 && (
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap", marginBottom: 20 }}>
            {["All", ...namespaces].map(ns => {
              const isActive = ns === "All" ? nsFilter === "" : nsFilter === ns;
              const c = NS_COLORS[ns];
              return (
                <button key={ns}
                  onClick={() => setNsFilter(ns === "All" ? "" : (nsFilter === ns ? "" : ns))}
                  style={{ padding: "5px 14px", borderRadius: 20, fontSize: 12, fontWeight: 500, cursor: "pointer",
                    background: isActive ? (c?.color ?? "#185fa5") : "#fff",
                    color: isActive ? "#fff" : "#555",
                    border: `1px solid ${isActive ? (c?.color ?? "#185fa5") : "#d8d8d8"}`,
                  }}>
                  {ns}
                </button>
              );
            })}
          </div>
        )}

        {/* ── Content Types ── */}
        <div style={{ marginBottom: 28 }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
            <span style={{ fontSize: 14, fontWeight: 600, color: "#1a1a1a" }}>
              Content Types
              <span style={{ fontWeight: 400, color: "#999", marginLeft: 6 }}>
                {nsFilter ? `${filtered.length} of ${items.length}` : filtered.length}
                {selected.size > 0 && ` · ${selected.size} selected`}
              </span>
            </span>
            <button onClick={toggleAll} disabled={filtered.length === 0} style={btnSm}>
              {allCtSelected ? "Deselect all" : "Select all"}
            </button>
          </div>
          <FlatTable>
            <thead>
              <tr>
                <Th style={{ width: 44 }}><input type="checkbox" checked={allCtSelected} onChange={toggleAll} style={{ accentColor: "#185fa5" }} /></Th>
                <Th style={{ width: "33%" }}>Content type name</Th>
                <Th style={{ width: "27%" }}>Code name</Th>
                <Th style={{ width: "18%" }}>Namespace</Th>
                <Th style={{ width: "12%", textAlign: "right" }}>Fields</Th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(ct => {
                const checked = selected.has(ct.codeName);
                const ns = getNs(ct.codeName);
                const c  = NS_COLORS[ns] ?? { bg: "#f1efe8", color: "#5f5e5a", border: "#d3d1c7" };
                return (
                  <tr key={ct.codeName} className={checked ? "ksp-row-sel" : "ksp-row"}
                    onClick={() => toggleOne(ct.codeName)} style={{ cursor: "pointer", background: checked ? "#eef5fc" : "#fff" }}>
                    <Td style={{ background: "inherit" }}>
                      <input type="checkbox" checked={checked} onChange={() => toggleOne(ct.codeName)}
                        onClick={e => e.stopPropagation()} style={{ accentColor: "#185fa5" }} />
                    </Td>
                    <TdName style={{ background: "inherit" }}><span title={ct.name}>{ct.name}</span></TdName>
                    <Td style={{ background: "inherit" }}><span className="ksp-mono" style={{ fontFamily: "monospace", fontSize: 12 }} title={ct.codeName}>{ct.codeName}</span></Td>
                    <Td style={{ background: "inherit" }}>
                      <span style={{ fontSize: 11, padding: "2px 8px", borderRadius: 4, fontWeight: 500, background: c.bg, color: c.color, border: `1px solid ${c.border}` }}>{ns}</span>
                    </Td>
                    <Td style={{ textAlign: "right", color: "#888", background: "inherit" }}>{ct.fields.length}</Td>
                  </tr>
                );
              })}
              {filtered.length === 0 && <tr><td colSpan={5} style={{ padding: "32px 16px", textAlign: "center", color: "#bbb", fontSize: 14, borderBottom: "1px solid #eee" }}>No content types found</td></tr>}
            </tbody>
          </FlatTable>
        </div>

        {/* ── Reusable Fields ── */}
        <div style={{ marginBottom: 28 }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
            <span style={{ fontSize: 14, fontWeight: 600, color: "#1a1a1a" }}>
              Reusable Fields
              <span style={{ fontWeight: 400, color: "#999", marginLeft: 6 }}>
                {filteredReusable.length}{selectedReusable.size > 0 && ` · ${selectedReusable.size} selected`}
              </span>
            </span>
            <button onClick={toggleAllReusable} disabled={filteredReusable.length === 0} style={btnSm}>
              {allRfSelected ? "Deselect all" : "Select all"}
            </button>
          </div>
          <FlatTable>
            <thead>
              <tr>
                <Th style={{ width: 44 }}><input type="checkbox" checked={allRfSelected} onChange={toggleAllReusable} style={{ accentColor: "#7c4f00" }} /></Th>
                <Th style={{ width: "40%" }}>Name</Th>
                <Th style={{ width: "36%" }}>Code name</Th>
                <Th style={{ width: "14%", textAlign: "right" }}>Fields</Th>
              </tr>
            </thead>
            <tbody>
              {filteredReusable.map(ct => {
                const checked = selectedReusable.has(ct.codeName);
                return (
                  <tr key={ct.codeName} className={checked ? "ksp-row-rf" : "ksp-row"}
                    onClick={() => toggleOneReusable(ct.codeName)} style={{ cursor: "pointer", background: checked ? "#fdf3e6" : "#fff" }}>
                    <Td style={{ background: "inherit" }}>
                      <input type="checkbox" checked={checked} onChange={() => toggleOneReusable(ct.codeName)}
                        onClick={e => e.stopPropagation()} style={{ accentColor: "#7c4f00" }} />
                    </Td>
                    <TdName style={{ background: "inherit" }}><span title={ct.name}>{ct.name}</span></TdName>
                    <Td style={{ background: "inherit" }}><span className="ksp-mono" style={{ fontFamily: "monospace", fontSize: 12 }} title={ct.codeName}>{ct.codeName}</span></Td>
                    <Td style={{ textAlign: "right", color: "#888", background: "inherit" }}>{ct.fields.length}</Td>
                  </tr>
                );
              })}
              {filteredReusable.length === 0 && <tr><td colSpan={4} style={{ padding: "28px 16px", textAlign: "center", color: "#bbb", fontSize: 14, borderBottom: "1px solid #eee" }}>No reusable fields found</td></tr>}
            </tbody>
          </FlatTable>
        </div>

        {/* ── Field Schemas ── */}
        <div>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
            <span style={{ fontSize: 14, fontWeight: 600, color: "#1a1a1a" }}>
              Field Schemas
              <span style={{ fontWeight: 400, color: "#999", marginLeft: 6 }}>
                {filteredSchemas.length}{selectedSchemas.size > 0 && ` · ${selectedSchemas.size} selected`}
              </span>
            </span>
            <button onClick={toggleAllSchemas} disabled={filteredSchemas.length === 0} style={btnSm}>
              {allSchSelected ? "Deselect all" : "Select all"}
            </button>
          </div>
          <FlatTable>
            <thead>
              <tr>
                <Th style={{ width: 44 }}><input type="checkbox" checked={allSchSelected} onChange={toggleAllSchemas} style={{ accentColor: "#4f46e5" }} /></Th>
                <Th style={{ width: "40%" }}>Display name</Th>
                <Th style={{ width: "36%" }}>Name</Th>
                <Th style={{ width: "14%", textAlign: "right" }}>Fields</Th>
              </tr>
            </thead>
            <tbody>
              {filteredSchemas.map(s => {
                const checked = selectedSchemas.has(s.name);
                return (
                  <tr key={s.name} className={checked ? "ksp-row-sc" : "ksp-row"}
                    onClick={() => toggleOneSchema(s.name)} style={{ cursor: "pointer", background: checked ? "#eeebff" : "#fff" }}>
                    <Td style={{ background: "inherit" }}>
                      <input type="checkbox" checked={checked} onChange={() => toggleOneSchema(s.name)}
                        onClick={e => e.stopPropagation()} style={{ accentColor: "#4f46e5" }} />
                    </Td>
                    <TdName style={{ background: "inherit" }}><span title={s.displayName}>{s.displayName}</span></TdName>
                    <Td style={{ background: "inherit" }}><span className="ksp-mono" style={{ fontFamily: "monospace", fontSize: 12 }} title={s.name}>{s.name}</span></Td>
                    <Td style={{ textAlign: "right", color: "#888", background: "inherit" }}>{s.fieldCount}</Td>
                  </tr>
                );
              })}
              {filteredSchemas.length === 0 && <tr><td colSpan={4} style={{ padding: "28px 16px", textAlign: "center", color: "#bbb", fontSize: 14, borderBottom: "1px solid #eee" }}>No field schemas found</td></tr>}
            </tbody>
          </FlatTable>
        </div>

      </>)}
    </div>
  );
}
