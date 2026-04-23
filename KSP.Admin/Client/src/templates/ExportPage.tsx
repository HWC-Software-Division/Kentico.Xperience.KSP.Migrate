import React, { useState, useEffect, useCallback } from "react";
import { BasePageProps, ContentType, ApiResponse } from "../types";

const getNs = (c: string) => c.split(".")[0] ?? "";

export function ExportPage(props: BasePageProps) {
  const [items, setItems]       = useState<ContentType[]>([]);
  const [loading, setLoading]   = useState(true);
  const [error, setError]       = useState<string | null>(null);
  const [search, setSearch]     = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [status, setStatus]     = useState<"idle"|"loading"|"success"|"error">("idle");

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const r = await fetch(`${props.apiBaseUrl}/list`);
      const j: ApiResponse<ContentType[]> = await r.json();
      if (!j.success) throw new Error(j.error ?? "API error");
      setItems(j.data ?? []);
    } catch (e) { setError(e instanceof Error ? e.message : "Failed"); }
    finally { setLoading(false); }
  }, [props.apiBaseUrl]);

  useEffect(() => { void load(); }, [load]);

  const filtered = items.filter(c =>
    c.name.toLowerCase().includes(search.toLowerCase()) ||
    c.codeName.toLowerCase().includes(search.toLowerCase())
  );
  const allSelected = filtered.length > 0 && filtered.every(c => selected.has(c.codeName));

  const toggleOne = (codeName: string) => {
    setSelected(prev => {
      const s = new Set(prev);
      s.has(codeName) ? s.delete(codeName) : s.add(codeName);
      return s;
    });
  };

  const toggleAll = () => {
    setSelected(prev => {
      const s = new Set(prev);
      if (allSelected) filtered.forEach(c => s.delete(c.codeName));
      else filtered.forEach(c => s.add(c.codeName));
      return s;
    });
  };

  const handleExport = async () => {
    if (selected.size === 0 || status === "loading") return;
    setStatus("loading");
    try {
      const res = await fetch(`${props.apiBaseUrl}/export`, {
        method: "POST", headers: {"Content-Type":"application/json"},
        body: JSON.stringify({ codeNames: [...selected] }),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url; a.download = `content-types-${new Date().toISOString().slice(0,10)}.zip`;
      a.click(); URL.revokeObjectURL(url);
      setStatus("success");
    } catch { setStatus("error"); }
  };

  const btnBase = {padding:"7px 14px", borderRadius:6, fontSize:13, fontWeight:500 as const, cursor:"pointer"};
  return (
    <div className="ksp-ct" style={{padding:24, fontFamily:"system-ui, sans-serif"}}>
      <style>{`
        .ksp-ct, .ksp-ct h2, .ksp-ct p, .ksp-ct td, .ksp-ct div { color: inherit !important; }
        .ksp-ct { color: #1a1a1a !important; }
        .ksp-ct .ksp-muted  { color: #888 !important; }
        .ksp-ct .ksp-subtle { color: #666 !important; }
        .ksp-ct .ksp-mono   { color: #666 !important; }
      `}</style>
      <div style={{display:"flex", justifyContent:"space-between", alignItems:"flex-start", marginBottom:18}}>
        <div>
          <h2 style={{margin:0, fontSize:17, fontWeight:500, color:"#1a1a1a"}}>Export Content Types</h2>
          <p className="ksp-subtle" style={{margin:"3px 0 0", fontSize:12}}>Select types to export as a .zip package</p>
        </div>
        <div style={{display:"flex", gap:8}}>
          <button onClick={toggleAll} disabled={loading} style={{...btnBase, background:"#fff", color:"#1a1a1a", border:"0.5px solid #bbb"}}>
            {allSelected ? "Deselect all" : "Select all"}
          </button>
          <button onClick={handleExport} disabled={selected.size===0 || status==="loading"}
            style={{...btnBase, background:"#0f6e56", color:"#fff", border:"0.5px solid #0f6e56", opacity: selected.size===0?0.45:1}}>
            {status==="loading" ? "Exporting..." : `Export${selected.size>0 ? ` (${selected.size})` : ""}`}
          </button>
        </div>
      </div>
      {status==="success" && <div style={{padding:"11px 14px", marginBottom:14, borderRadius:7, background:"#eaf3de", border:"0.5px solid #c0dd97", color:"#3b6d11", fontSize:13}}>Export complete — .zip downloaded.</div>}
      {status==="error"   && <div style={{padding:"11px 14px", marginBottom:14, borderRadius:7, background:"#fcebeb", border:"0.5px solid #f7c1c1", color:"#a32d2d", fontSize:13}}>Export failed. Check API connection.</div>}
      {error != null && <div style={{padding:"11px 14px", marginBottom:14, borderRadius:7, background:"#fcebeb", border:"0.5px solid #f7c1c1", color:"#a32d2d", fontSize:13}}>{error}</div>}
      <div style={{display:"flex", gap:10, marginBottom:12, alignItems:"center"}}>
        <input type="text" placeholder="Filter content types..." value={search}
          onChange={e => setSearch(e.target.value)}
          style={{flex:1, fontSize:13, padding:"7px 10px", borderRadius:6, border:"0.5px solid #d0d0d0"}}
        />
        {selected.size > 0 && <span style={{fontSize:12, color:"#666", whiteSpace:"nowrap"}}>{selected.size} selected</span>}
      </div>
      {loading ? <div style={{padding:40, textAlign:"center", color:"#aaa", fontSize:13}}>Loading...</div> : (
        <div style={{border:"0.5px solid #e0e0e0", borderRadius:10, overflow:"hidden"}}>
          <table style={{width:"100%", borderCollapse:"collapse", tableLayout:"fixed"}}>
            <thead>
              <tr style={{background:"#fafafa", borderBottom:"0.5px solid #e0e0e0"}}>
                <th style={{width:36, padding:"8px 10px"}}><input type="checkbox" checked={allSelected} onChange={toggleAll} style={{accentColor:"#185fa5"}}/></th>
                <th style={{padding:"8px 12px", textAlign:"left", fontSize:11, fontWeight:500, color:"#888", width:"34%"}}>Name</th>
                <th style={{padding:"8px 12px", textAlign:"left", fontSize:11, fontWeight:500, color:"#888", width:"30%"}}>Code name</th>
                <th style={{padding:"8px 12px", textAlign:"left", fontSize:11, fontWeight:500, color:"#888", width:"18%"}}>Namespace</th>
                <th style={{padding:"8px 12px", textAlign:"right", fontSize:11, fontWeight:500, color:"#888", width:"12%"}}>Fields</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((ct, i) => {
                const checked = selected.has(ct.codeName);
                return (
                  <tr key={ct.codeName} onClick={() => toggleOne(ct.codeName)}
                    style={{borderTop:"0.5px solid #e0e0e0", background: checked?"#eef5fc":i%2?"#fafafa":"#fff", cursor:"pointer"}}>
                    <td style={{padding:"8px 10px"}}><input type="checkbox" checked={checked} onChange={()=>toggleOne(ct.codeName)} onClick={e=>e.stopPropagation()} style={{accentColor:"#185fa5"}}/></td>
                    <td style={{padding:"8px 12px", fontSize:13, color:"#1a1a1a", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap"}}>{ct.name}</td>
                    <td className="ksp-mono" style={{padding:"8px 12px", fontSize:12, fontFamily:"monospace", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap"}}>{ct.codeName}</td>
                    <td style={{padding:"8px 12px", fontSize:13}}>{getNs(ct.codeName)}</td>
                    <td style={{padding:"8px 12px", fontSize:13, textAlign:"right", color:"#888"}}>{ct.fields.length}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
