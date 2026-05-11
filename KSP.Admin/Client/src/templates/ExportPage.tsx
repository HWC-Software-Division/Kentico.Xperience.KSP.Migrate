import React, { useState, useEffect, useCallback } from "react";
import { BasePageProps, ContentType, FieldSchema, SchemaDep, ApiResponse } from "../types";

const getNs = (c: string) => c.split(".")[0] ?? "";

const STYLE = `
  .ksp-ct, .ksp-ct h2, .ksp-ct p, .ksp-ct td, .ksp-ct th, .ksp-ct div { color: inherit !important; }
  .ksp-ct { color: #1a1a1a !important; }
  .ksp-ct .ksp-muted  { color: #888 !important; }
  .ksp-ct .ksp-subtle { color: #666 !important; }
  .ksp-ct .ksp-mono   { color: #666 !important; }
`;

interface DepItem { codeName: string; name: string; }
interface DepsModalData { reusable: DepItem[]; schemas: SchemaDep[]; }

export function ExportPage(props: BasePageProps) {
  const [items,           setItems]           = useState<ContentType[]>([]);
  const [reusableItems,   setReusableItems]   = useState<ContentType[]>([]);
  const [schemaItems,     setSchemaItems]     = useState<FieldSchema[]>([]);
  const [loading,         setLoading]         = useState(true);
  const [error,           setError]           = useState<string | null>(null);
  const [search,          setSearch]          = useState("");
  const [selected,        setSelected]        = useState<Set<string>>(new Set());
  const [selectedReusable,setSelectedReusable]= useState<Set<string>>(new Set());
  const [selectedSchemas, setSelectedSchemas] = useState<Set<string>>(new Set());
  const [status,          setStatus]          = useState<"idle"|"loading"|"success"|"error">("idle");
  const [depsModal,       setDepsModal]       = useState<DepsModalData | null>(null);

  // ── load ──────────────────────────────────────────────────────────────────
  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const [r1, r2, r3] = await Promise.all([
        fetch(`${props.apiBaseUrl}/list`),
        fetch(`${props.apiBaseUrl}/list-reusable?includeLegacy=true`),
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
      setReusableItems(j2.data ?? []);
      setSchemaItems(j3.data ?? []);
    } catch (e) { setError(e instanceof Error ? e.message : "Failed"); }
    finally { setLoading(false); }
  }, [props.apiBaseUrl]);

  useEffect(() => { void load(); }, [load]);

  // ── #3: check both reusable AND schema dependencies ───────────────────────
  const checkDeps = useCallback(async (
    codeNames: string[],
    currentReusable: Set<string>,
    currentSchemas: Set<string>
  ) => {
    if (codeNames.length === 0) return;
    try {
      const body = JSON.stringify(codeNames);
      const headers = { "Content-Type": "application/json" };

      const [r1, r2] = await Promise.all([
        fetch(`${props.apiBaseUrl}/reusable-deps`, { method: "POST", headers, body }),
        fetch(`${props.apiBaseUrl}/schema-deps`,   { method: "POST", headers, body }),
      ]);
      const [j1, j2]: [ApiResponse<string[]>, ApiResponse<SchemaDep[]>] =
        await Promise.all([r1.json(), r2.json()]);

      const newReusable = (j1.data ?? []).filter(cn => !currentReusable.has(cn));
      const newSchemas  = (j2.data ?? []).filter(s  => !currentSchemas.has(s.name));

      if (newReusable.length === 0 && newSchemas.length === 0) return;

      setDepsModal({
        reusable: newReusable.map(cn => ({
          codeName: cn,
          name: reusableItems.find(r => r.codeName === cn)?.name ?? cn,
        })),
        schemas: newSchemas,
      });
    } catch {}
  }, [props.apiBaseUrl, reusableItems]);

  // ── filter / toggles ──────────────────────────────────────────────────────
  const q                = search.toLowerCase();
  const filtered         = items.filter(c =>
    c.name.toLowerCase().includes(q) || c.codeName.toLowerCase().includes(q));
  const filteredReusable = reusableItems.filter(c =>
    c.name.toLowerCase().includes(q) || c.codeName.toLowerCase().includes(q));
  const filteredSchemas  = schemaItems.filter(s =>
    s.displayName.toLowerCase().includes(q) || s.name.toLowerCase().includes(q));

  const allCtSelected  = filtered.length > 0 && filtered.every(c => selected.has(c.codeName));
  const allRfSelected  = filteredReusable.length > 0 && filteredReusable.every(c => selectedReusable.has(c.codeName));
  const allSchSelected = filteredSchemas.length > 0 && filteredSchemas.every(s => selectedSchemas.has(s.name));

  const toggleOne = (codeName: string) => {
    const isAdding = !selected.has(codeName);
    setSelected(prev => { const s = new Set(prev); isAdding ? s.add(codeName) : s.delete(codeName); return s; });
    if (isAdding) void checkDeps([codeName], selectedReusable, selectedSchemas);
  };

  const toggleAll = () => {
    const adding  = !allCtSelected;
    const newOnes = adding ? filtered.filter(c => !selected.has(c.codeName)).map(c => c.codeName) : [];
    setSelected(prev => {
      const s = new Set(prev);
      adding ? filtered.forEach(c => s.add(c.codeName)) : filtered.forEach(c => s.delete(c.codeName));
      return s;
    });
    if (adding && newOnes.length > 0) void checkDeps(newOnes, selectedReusable, selectedSchemas);
  };

  const toggleOneReusable = (codeName: string) =>
    setSelectedReusable(prev => { const s = new Set(prev); s.has(codeName) ? s.delete(codeName) : s.add(codeName); return s; });

  const toggleAllReusable = () =>
    setSelectedReusable(prev => {
      const s = new Set(prev);
      allRfSelected ? filteredReusable.forEach(c => s.delete(c.codeName)) : filteredReusable.forEach(c => s.add(c.codeName));
      return s;
    });

  const toggleOneSchema = (name: string) =>
    setSelectedSchemas(prev => { const s = new Set(prev); s.has(name) ? s.delete(name) : s.add(name); return s; });

  const toggleAllSchemas = () =>
    setSelectedSchemas(prev => {
      const s = new Set(prev);
      allSchSelected ? filteredSchemas.forEach(sc => s.delete(sc.name)) : filteredSchemas.forEach(sc => s.add(sc.name));
      return s;
    });

  // ── export ────────────────────────────────────────────────────────────────
  const handleExport = async () => {
    if ((selected.size === 0 && selectedReusable.size === 0 && selectedSchemas.size === 0) || status === "loading") return;
    setStatus("loading");
    try {
      const res = await fetch(`${props.apiBaseUrl}/export`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          codeNames:         [...selected],
          reusableCodeNames: [...selectedReusable],
          schemaNames:       [...selectedSchemas],
        }),
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

      const a = document.createElement("a");
      a.href = url; a.download = fileName; a.click();
      URL.revokeObjectURL(url);
      setStatus("success");
    } catch { setStatus("error"); }
  };

  const totalSelected = selected.size + selectedReusable.size + selectedSchemas.size;
  const btnBase = { padding: "7px 14px", borderRadius: 6, fontSize: 13, fontWeight: 500 as const, cursor: "pointer" };

  return (
    <div className="ksp-ct" style={{ padding: 24, fontFamily: "system-ui, sans-serif" }}>
      <style>{STYLE}</style>

      {/* ── Combined dependency modal (#3) ── */}
      {depsModal && (
        <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.4)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 1000 }}>
          <div style={{ background: "#fff", borderRadius: 12, padding: 24, width: 460, maxWidth: "90vw", maxHeight: "80vh", overflow: "auto", boxShadow: "0 8px 32px rgba(0,0,0,0.18)" }}>
            <h3 style={{ margin: "0 0 6px", fontSize: 15, fontWeight: 600, color: "#1a1a1a" }}>Dependencies Detected</h3>
            <p style={{ margin: "0 0 14px", fontSize: 13, color: "#666" }}>
              The selected content types reference the following. Include them in the export?
            </p>

            {/* Reusable fields section */}
            {depsModal.reusable.length > 0 && (
              <div style={{ marginBottom: 12 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: "#7c4f00", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 6 }}>
                  Reusable Fields ({depsModal.reusable.length})
                </div>
                <div style={{ background: "#fdf3e6", borderRadius: 7, padding: "10px 12px" }}>
                  {depsModal.reusable.map(d => (
                    <div key={d.codeName} style={{ display: "flex", gap: 8, alignItems: "baseline", padding: "2px 0" }}>
                      <span style={{ fontSize: 13, color: "#1a1a1a" }}>{d.name}</span>
                      <span style={{ fontSize: 11, fontFamily: "monospace", color: "#888" }}>{d.codeName}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Field schemas section */}
            {depsModal.schemas.length > 0 && (
              <div style={{ marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: "#4f46e5", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 6 }}>
                  Field Schemas ({depsModal.schemas.length})
                </div>
                <div style={{ background: "#eeebff", borderRadius: 7, padding: "10px 12px" }}>
                  {depsModal.schemas.map(s => (
                    <div key={s.name} style={{ display: "flex", gap: 8, alignItems: "baseline", padding: "2px 0" }}>
                      <span style={{ fontSize: 13, color: "#1a1a1a" }}>{s.displayName}</span>
                      <span style={{ fontSize: 11, fontFamily: "monospace", color: "#888" }}>{s.name}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
              <button onClick={() => setDepsModal(null)}
                style={{ ...btnBase, background: "#fff", border: "0.5px solid #bbb", color: "#1a1a1a" }}>
                Skip
              </button>
              <button onClick={() => {
                setSelectedReusable(prev => {
                  const s = new Set(prev);
                  depsModal.reusable.forEach(d => s.add(d.codeName));
                  return s;
                });
                setSelectedSchemas(prev => {
                  const s = new Set(prev);
                  depsModal.schemas.forEach(d => s.add(d.name));
                  return s;
                });
                setDepsModal(null);
              }} style={{ ...btnBase, background: "#185fa5", border: "0.5px solid #185fa5", color: "#fff" }}>
                Auto-select & Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Header ── */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 16 }}>
        <div>
          <h2 style={{ margin: 0, fontSize: 17, fontWeight: 500, color: "#1a1a1a" }}>Export Content Types</h2>
          <p className="ksp-subtle" style={{ margin: "3px 0 0", fontSize: 12 }}>Select types to export as a .zip package</p>
        </div>
        <button onClick={handleExport}
          disabled={totalSelected === 0 || status === "loading"}
          style={{ ...btnBase, background: "#0f6e56", color: "#fff", border: "0.5px solid #0f6e56", opacity: totalSelected === 0 ? 0.45 : 1 }}>
          {status === "loading" ? "Exporting…" : `Export${totalSelected > 0 ? ` (${totalSelected})` : ""}`}
        </button>
      </div>

      {status === "success" && <div style={{ padding: "11px 14px", marginBottom: 14, borderRadius: 7, background: "#eaf3de", border: "0.5px solid #c0dd97", color: "#3b6d11", fontSize: 13 }}>Export complete — .zip downloaded.</div>}
      {status === "error"   && <div style={{ padding: "11px 14px", marginBottom: 14, borderRadius: 7, background: "#fcebeb", border: "0.5px solid #f7c1c1", color: "#a32d2d", fontSize: 13 }}>Export failed. Check API connection.</div>}
      {error != null        && <div style={{ padding: "11px 14px", marginBottom: 14, borderRadius: 7, background: "#fcebeb", border: "0.5px solid #f7c1c1", color: "#a32d2d", fontSize: 13 }}>{error}</div>}

      <input type="text" placeholder="Filter by name…" value={search}
        onChange={e => setSearch(e.target.value)}
        style={{ width: "100%", marginBottom: 18, fontSize: 13, boxSizing: "border-box", padding: "7px 10px", borderRadius: 6, border: "0.5px solid #d0d0d0" }}
      />

      {loading ? (
        <div style={{ padding: 40, textAlign: "center", color: "#aaa", fontSize: 13 }}>Loading…</div>
      ) : (<>

        {/* ── Content Types ── */}
        <div style={{ marginBottom: 22 }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
            <span style={{ fontSize: 13, fontWeight: 500, color: "#1a1a1a" }}>
              Content Types
              {selected.size > 0 && <span className="ksp-muted" style={{ fontWeight: 400, marginLeft: 6 }}>({selected.size} selected)</span>}
            </span>
            <button onClick={toggleAll} disabled={filtered.length === 0}
              style={{ ...btnBase, padding: "5px 12px", background: "#fff", color: "#1a1a1a", border: "0.5px solid #bbb", fontSize: 12 }}>
              {allCtSelected ? "Deselect all" : "Select all"}
            </button>
          </div>
          <div style={{ border: "0.5px solid #e0e0e0", borderRadius: 10, overflow: "hidden" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
              <thead>
                <tr style={{ background: "#fafafa", borderBottom: "0.5px solid #e0e0e0" }}>
                  <th style={{ width: 36, padding: "8px 10px" }}><input type="checkbox" checked={allCtSelected} onChange={toggleAll} style={{ accentColor: "#185fa5" }} /></th>
                  <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "34%" }}>Name</th>
                  <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "28%" }}>Code name</th>
                  <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "18%" }}>Namespace</th>
                  <th style={{ padding: "8px 12px", textAlign: "right", fontSize: 11, fontWeight: 500, color: "#888", width: "14%" }}>Fields</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((ct, i) => {
                  const checked = selected.has(ct.codeName);
                  return (
                    <tr key={ct.codeName} onClick={() => toggleOne(ct.codeName)}
                      style={{ borderTop: "0.5px solid #e0e0e0", background: checked ? "#eef5fc" : i % 2 ? "#fafafa" : "#fff", cursor: "pointer" }}>
                      <td style={{ padding: "8px 10px" }}>
                        <input type="checkbox" checked={checked} onChange={() => toggleOne(ct.codeName)}
                          onClick={e => e.stopPropagation()} style={{ accentColor: "#185fa5" }} />
                      </td>
                      <td style={{ padding: "8px 12px", fontSize: 13, color: "#1a1a1a", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{ct.name}</td>
                      <td className="ksp-mono" style={{ padding: "8px 12px", fontSize: 12, fontFamily: "monospace", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{ct.codeName}</td>
                      <td style={{ padding: "8px 12px", fontSize: 13 }}>{getNs(ct.codeName)}</td>
                      <td style={{ padding: "8px 12px", fontSize: 13, textAlign: "right", color: "#888" }}>{ct.fields.length}</td>
                    </tr>
                  );
                })}
                {filtered.length === 0 && <tr><td colSpan={5} style={{ padding: 28, textAlign: "center", color: "#aaa", fontSize: 13 }}>No content types found</td></tr>}
              </tbody>
            </table>
          </div>
        </div>

        {/* ── Reusable Fields ── */}
        <div style={{ marginBottom: 22 }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
            <span style={{ fontSize: 13, fontWeight: 500, color: "#1a1a1a" }}>
              Reusable Fields
              {selectedReusable.size > 0 && <span className="ksp-muted" style={{ fontWeight: 400, marginLeft: 6 }}>({selectedReusable.size} selected)</span>}
            </span>
            <button onClick={toggleAllReusable} disabled={filteredReusable.length === 0}
              style={{ ...btnBase, padding: "5px 12px", background: "#fff", color: "#1a1a1a", border: "0.5px solid #bbb", fontSize: 12 }}>
              {allRfSelected ? "Deselect all" : "Select all"}
            </button>
          </div>
          <div style={{ border: "0.5px solid #e0e0e0", borderRadius: 10, overflow: "hidden" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
              <thead>
                <tr style={{ background: "#fafafa", borderBottom: "0.5px solid #e0e0e0" }}>
                  <th style={{ width: 36, padding: "8px 10px" }}><input type="checkbox" checked={allRfSelected} onChange={toggleAllReusable} style={{ accentColor: "#7c4f00" }} /></th>
                  <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "38%" }}>Name</th>
                  <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "34%" }}>Code name</th>
                  <th style={{ padding: "8px 12px", textAlign: "right", fontSize: 11, fontWeight: 500, color: "#888", width: "22%" }}>Fields</th>
                </tr>
              </thead>
              <tbody>
                {filteredReusable.map((ct, i) => {
                  const checked = selectedReusable.has(ct.codeName);
                  return (
                    <tr key={ct.codeName} onClick={() => toggleOneReusable(ct.codeName)}
                      style={{ borderTop: "0.5px solid #e0e0e0", background: checked ? "#fdf3e6" : i % 2 ? "#fafafa" : "#fff", cursor: "pointer" }}>
                      <td style={{ padding: "8px 10px" }}>
                        <input type="checkbox" checked={checked} onChange={() => toggleOneReusable(ct.codeName)}
                          onClick={e => e.stopPropagation()} style={{ accentColor: "#7c4f00" }} />
                      </td>
                      <td style={{ padding: "8px 12px", fontSize: 13, color: "#1a1a1a", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{ct.name}</td>
                      <td className="ksp-mono" style={{ padding: "8px 12px", fontSize: 12, fontFamily: "monospace", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{ct.codeName}</td>
                      <td style={{ padding: "8px 12px", fontSize: 13, textAlign: "right", color: "#888" }}>{ct.fields.length}</td>
                    </tr>
                  );
                })}
                {filteredReusable.length === 0 && <tr><td colSpan={4} style={{ padding: 20, textAlign: "center", color: "#aaa", fontSize: 13 }}>No reusable fields found</td></tr>}
              </tbody>
            </table>
          </div>
        </div>

        {/* ── Field Schemas ── */}
        <div>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
            <span style={{ fontSize: 13, fontWeight: 500, color: "#1a1a1a" }}>
              Field Schemas
              {selectedSchemas.size > 0 && <span className="ksp-muted" style={{ fontWeight: 400, marginLeft: 6 }}>({selectedSchemas.size} selected)</span>}
            </span>
            <button onClick={toggleAllSchemas} disabled={filteredSchemas.length === 0}
              style={{ ...btnBase, padding: "5px 12px", background: "#fff", color: "#1a1a1a", border: "0.5px solid #bbb", fontSize: 12 }}>
              {allSchSelected ? "Deselect all" : "Select all"}
            </button>
          </div>
          <div style={{ border: "0.5px solid #e0e0e0", borderRadius: 10, overflow: "hidden" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
              <thead>
                <tr style={{ background: "#fafafa", borderBottom: "0.5px solid #e0e0e0" }}>
                  <th style={{ width: 36, padding: "8px 10px" }}><input type="checkbox" checked={allSchSelected} onChange={toggleAllSchemas} style={{ accentColor: "#4f46e5" }} /></th>
                  <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "38%" }}>Display name</th>
                  <th style={{ padding: "8px 12px", textAlign: "left", fontSize: 11, fontWeight: 500, color: "#888", width: "34%" }}>Name</th>
                  <th style={{ padding: "8px 12px", textAlign: "right", fontSize: 11, fontWeight: 500, color: "#888", width: "22%" }}>Fields</th>
                </tr>
              </thead>
              <tbody>
                {filteredSchemas.map((s, i) => {
                  const checked = selectedSchemas.has(s.name);
                  return (
                    <tr key={s.name} onClick={() => toggleOneSchema(s.name)}
                      style={{ borderTop: "0.5px solid #e0e0e0", background: checked ? "#eeebff" : i % 2 ? "#fafafa" : "#fff", cursor: "pointer" }}>
                      <td style={{ padding: "8px 10px" }}>
                        <input type="checkbox" checked={checked} onChange={() => toggleOneSchema(s.name)}
                          onClick={e => e.stopPropagation()} style={{ accentColor: "#4f46e5" }} />
                      </td>
                      <td style={{ padding: "8px 12px", fontSize: 13, color: "#1a1a1a", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{s.displayName}</td>
                      <td className="ksp-mono" style={{ padding: "8px 12px", fontSize: 12, fontFamily: "monospace", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{s.name}</td>
                      <td style={{ padding: "8px 12px", fontSize: 13, textAlign: "right", color: "#888" }}>{s.fieldCount}</td>
                    </tr>
                  );
                })}
                {filteredSchemas.length === 0 && <tr><td colSpan={4} style={{ padding: 20, textAlign: "center", color: "#aaa", fontSize: 13 }}>No field schemas found</td></tr>}
              </tbody>
            </table>
          </div>
        </div>

      </>)}
    </div>
  );
}
