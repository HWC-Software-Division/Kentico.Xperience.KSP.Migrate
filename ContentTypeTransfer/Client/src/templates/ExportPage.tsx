import React from "react";
import { BasePageProps, ContentType, ApiResponse } from "../types";

const getNs = (c: string) => c.split(".")[0] ?? "";

interface State {
  items: ContentType[]; loading: boolean; error: string|null;
  search: string; selected: Set<string>; status: "idle"|"loading"|"success"|"error";
}

export class ExportPage extends React.Component<BasePageProps, State> {
  state: State = { items:[], loading:true, error:null, search:"", selected:new Set(), status:"idle" };

  componentDidMount() { this.load(); }

  load = async () => {
    this.setState({ loading:true, error:null });
    try {
      const r = await fetch(`${this.props.apiBaseUrl}/list`);
      const j: ApiResponse<ContentType[]> = await r.json();
      if (!j.success) throw new Error(j.error ?? "API error");
      this.setState({ items: j.data ?? [], loading: false });
    } catch(e) { this.setState({ error: e instanceof Error ? e.message : "Failed", loading:false }); }
  };

  toggleOne = (codeName: string) => {
    const s = new Set(this.state.selected);
    s.has(codeName) ? s.delete(codeName) : s.add(codeName);
    this.setState({ selected: s });
  };

  toggleAll = () => {
    const { items, search, selected } = this.state;
    const filtered = items.filter(c => c.name.toLowerCase().includes(search.toLowerCase()) || c.codeName.toLowerCase().includes(search.toLowerCase()));
    const allSelected = filtered.length > 0 && filtered.every(c => selected.has(c.codeName));
    const s = new Set(selected);
    if (allSelected) filtered.forEach(c => s.delete(c.codeName));
    else filtered.forEach(c => s.add(c.codeName));
    this.setState({ selected: s });
  };

  handleExport = async () => {
    const { selected, status } = this.state;
    if (selected.size === 0 || status === "loading") return;
    this.setState({ status: "loading" });
    try {
      const res = await fetch(`${this.props.apiBaseUrl}/export`, {
        method: "POST", headers: {"Content-Type":"application/json"},
        body: JSON.stringify({ codeNames: [...selected] }),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url; a.download = `content-types-${new Date().toISOString().slice(0,10)}.zip`;
      a.click(); URL.revokeObjectURL(url);
      this.setState({ status: "success" });
    } catch { this.setState({ status: "error" }); }
  };

  render() {
    const { items, loading, error, search, selected, status } = this.state;
    const filtered = items.filter(c => c.name.toLowerCase().includes(search.toLowerCase()) || c.codeName.toLowerCase().includes(search.toLowerCase()));
    const allSelected = filtered.length > 0 && filtered.every(c => selected.has(c.codeName));
    const btnBase = {padding:"7px 14px", borderRadius:6, fontSize:13, fontWeight:500 as const, cursor:"pointer"};
    return (
      <div style={{padding:24, fontFamily:"system-ui, sans-serif"}}>
        <div style={{display:"flex", justifyContent:"space-between", alignItems:"flex-start", marginBottom:18}}>
          <div>
            <h2 style={{margin:0, fontSize:17, fontWeight:500}}>Export Content Types</h2>
            <p style={{margin:"3px 0 0", fontSize:12, color:"#666"}}>Select types to export as a .zip package</p>
          </div>
          <div style={{display:"flex", gap:8}}>
            <button onClick={this.toggleAll} disabled={loading} style={{...btnBase, background:"#fff", color:"#1a1a1a", border:"0.5px solid #bbb"}}>
              {allSelected ? "Deselect all" : "Select all"}
            </button>
            <button onClick={this.handleExport} disabled={selected.size===0 || status==="loading"}
              style={{...btnBase, background:"#0f6e56", color:"#fff", border:"0.5px solid #0f6e56", opacity: selected.size===0?0.45:1}}>
              {status==="loading" ? "Exporting..." : `Export${selected.size>0 ? ` (${selected.size})` : ""}`}
            </button>
          </div>
        </div>
        {status==="success" && <div style={{padding:"11px 14px", marginBottom:14, borderRadius:7, background:"#eaf3de", border:"0.5px solid #c0dd97", color:"#3b6d11", fontSize:13}}>Export complete — .zip downloaded.</div>}
        {status==="error"   && <div style={{padding:"11px 14px", marginBottom:14, borderRadius:7, background:"#fcebeb", border:"0.5px solid #f7c1c1", color:"#a32d2d", fontSize:13}}>Export failed. Check API connection.</div>}
        {error && <div style={{padding:"11px 14px", marginBottom:14, borderRadius:7, background:"#fcebeb", border:"0.5px solid #f7c1c1", color:"#a32d2d", fontSize:13}}>{error}</div>}
        <div style={{display:"flex", gap:10, marginBottom:12, alignItems:"center"}}>
          <input type="text" placeholder="Filter content types..." value={search}
            onChange={e => this.setState({search:e.target.value})}
            style={{flex:1, fontSize:13, padding:"7px 10px", borderRadius:6, border:"0.5px solid #d0d0d0"}}
          />
          {selected.size > 0 && <span style={{fontSize:12, color:"#666", whiteSpace:"nowrap"}}>{selected.size} selected</span>}
        </div>
        {loading ? <div style={{padding:40, textAlign:"center", color:"#aaa", fontSize:13}}>Loading...</div> : (
          <div style={{border:"0.5px solid #e0e0e0", borderRadius:10, overflow:"hidden"}}>
            <table style={{width:"100%", borderCollapse:"collapse", tableLayout:"fixed"}}>
              <thead>
                <tr style={{background:"#fafafa", borderBottom:"0.5px solid #e0e0e0"}}>
                  <th style={{width:36, padding:"8px 10px"}}><input type="checkbox" checked={allSelected} onChange={this.toggleAll} style={{accentColor:"#185fa5"}}/></th>
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
                    <tr key={ct.codeName} onClick={() => this.toggleOne(ct.codeName)}
                      style={{borderTop:"0.5px solid #e0e0e0", background: checked?"#eef5fc":i%2?"#fafafa":"#fff", cursor:"pointer"}}>
                      <td style={{padding:"8px 10px"}}><input type="checkbox" checked={checked} onChange={()=>this.toggleOne(ct.codeName)} onClick={e=>e.stopPropagation()} style={{accentColor:"#185fa5"}}/></td>
                      <td style={{padding:"8px 12px", fontSize:13, overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap"}}>{ct.name}</td>
                      <td style={{padding:"8px 12px", fontSize:12, color:"#666", fontFamily:"monospace", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap"}}>{ct.codeName}</td>
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
}
