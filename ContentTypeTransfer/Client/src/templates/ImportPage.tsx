import React from "react";
import { ApiResponse, BasePageProps, ImportResult } from "../types";

interface State {
  file: File|null; status: "idle"|"loading"|"success"|"error";
  result: ImportResult|null; isDragOver: boolean;
}

export class ImportPage extends React.Component<BasePageProps, State> {
  state: State = { file:null, status:"idle", result:null, isDragOver:false };
  fileRef = React.createRef<HTMLInputElement>();

  handleFile = (f: File|null) => {
    if (!f) return;
    this.setState({ file:f, status:"idle", result:null });
  };

  handleImport = async () => {
    const { file, status } = this.state;
    if (!file || status === "loading") return;
    this.setState({ status: "loading" });
    const form = new FormData();
    form.append("file", file);
    try {
      const res = await fetch(`${this.props.apiBaseUrl}/import`, { method:"POST", body:form });
      const json: ApiResponse<ImportResult> = await res.json();
      if (!json.success && !json.data) throw new Error(json.error ?? "Import failed");
      this.setState({ result: json.data ?? null, status: json.success ? "success" : "error" });
    } catch { this.setState({ status:"error" }); }
  };

  render() {
    const { file, status, result, isDragOver } = this.state;
    const steps = ["Module reads .zip → extracts content-types.json","Validates structure of each content type","Calls POST /api/migrate/content-type for each type","Migration API creates or updates the DataClassInfo in XbyK"];
    return (
      <div style={{padding:24, fontFamily:"system-ui, sans-serif"}}>
        <div style={{marginBottom:18}}>
          <h2 style={{margin:0, fontSize:17, fontWeight:500}}>Import Content Types</h2>
          <p style={{margin:"3px 0 0", fontSize:12, color:"#666"}}>Upload a .zip file — content types will be created or updated via the migration API</p>
        </div>
        <div onClick={()=>this.fileRef.current?.click()}
          onDragEnter={()=>this.setState({isDragOver:true})}
          onDragLeave={()=>this.setState({isDragOver:false})}
          onDragOver={e=>{e.preventDefault();this.setState({isDragOver:true})}}
          onDrop={e=>{e.preventDefault();this.setState({isDragOver:false});this.handleFile(e.dataTransfer.files[0]??null)}}
          style={{border:`1.5px dashed ${isDragOver?"#185fa5":file?"#9fe1cb":"#d0d0d0"}`,borderRadius:10,background:isDragOver?"#e6f1fb":file?"#f0faf6":"#fafafa",padding:"36px 20px",textAlign:"center",cursor:"pointer",marginBottom:18}}>
          <input ref={this.fileRef} type="file" accept=".zip" style={{display:"none"}} onChange={e=>this.handleFile(e.target.files?.[0]??null)}/>
          {file ? (
            <>
              <div style={{fontSize:14, fontWeight:500, color:"#0f6e56"}}>{file.name}</div>
              <div style={{fontSize:12, color:"#3b8c6a", marginTop:3}}>{(file.size/1024).toFixed(1)} KB — click to change</div>
            </>
          ) : (
            <>
              <div style={{fontSize:14, fontWeight:500, color:"#444"}}>Drop .zip here or click to browse</div>
              <div style={{fontSize:12, color:"#aaa", marginTop:3}}>Only .zip files exported from this module are supported</div>
            </>
          )}
        </div>
        <div style={{background:"#fafafa", border:"0.5px solid #e0e0e0", borderRadius:8, padding:"14px 16px", marginBottom:18}}>
          <div style={{fontSize:13, fontWeight:500, marginBottom:10}}>Import flow</div>
          {steps.map((s,i) => (
            <div key={i} style={{display:"flex", gap:9, alignItems:"flex-start", marginBottom:6}}>
              <span style={{flexShrink:0, width:18, height:18, borderRadius:"50%", background:"#e6f1fb", color:"#185fa5", display:"inline-flex", alignItems:"center", justifyContent:"center", fontSize:10, fontWeight:500}}>{i+1}</span>
              <span style={{fontSize:12, color:"#666", lineHeight:1.5}}>{s}</span>
            </div>
          ))}
        </div>
        {status==="loading" && <div style={{padding:"11px 14px", marginBottom:14, borderRadius:7, background:"#e6f1fb", border:"0.5px solid #b5d4f4", color:"#185fa5", fontSize:13}}>Processing...</div>}
        {status==="success" && result && <div style={{padding:"11px 14px", marginBottom:14, borderRadius:7, background:"#eaf3de", border:"0.5px solid #c0dd97", color:"#3b6d11", fontSize:13}}>Import complete — {result.created} created, {result.updated} updated.</div>}
        {status==="error" && <div style={{padding:"11px 14px", marginBottom:14, borderRadius:7, background:"#fcebeb", border:"0.5px solid #f7c1c1", color:"#a32d2d", fontSize:13}}>Import failed. Check API connection.</div>}
        <div style={{display:"flex", gap:8}}>
          <button onClick={this.handleImport} disabled={!file || status==="loading"}
            style={{padding:"7px 16px", borderRadius:6, fontSize:13, fontWeight:500, border:"0.5px solid #185fa5", background:"#185fa5", color:"#fff", cursor:!file?"not-allowed":"pointer", opacity:!file?0.45:1}}>
            {status==="loading" ? "Importing..." : "Import"}
          </button>
          {file && <button onClick={()=>this.setState({file:null, status:"idle", result:null})}
            style={{padding:"7px 14px", borderRadius:6, fontSize:13, border:"0.5px solid #bbb", background:"#fff", cursor:"pointer"}}>Clear</button>}
        </div>
      </div>
    );
  }
}
