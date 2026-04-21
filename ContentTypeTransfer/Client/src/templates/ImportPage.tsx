import React, { useRef, useState } from "react";
import { ApiResponse, BasePageProps, ImportResult } from "../types";

// ── Component ──────────────────────────────────────────────────────────────
export const ImportPage = (props: BasePageProps) => {
  const fileRef                   = useRef<HTMLInputElement>(null);
  const [file, setFile]           = useState<File | null>(null);
  const [status, setStatus]       = useState<"idle" | "loading" | "success" | "error">("idle");
  const [result, setResult]       = useState<ImportResult | null>(null);
  const [isDragOver, setDragOver] = useState(false);

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
      const res  = await fetch(`${props.apiBaseUrl}/import`, { method: "POST", body: form });
      const json: ApiResponse<ImportResult> = await res.json();
      if (!json.success && !json.data) throw new Error(json.error ?? "Import failed");
      setResult(json.data ?? null);
      setStatus(json.success ? "success" : "error");
    } catch (e) {
      setStatus("error");
    }
  };

  return (
    <div style={{ padding: 24, fontFamily: "system-ui, sans-serif" }}>
      {/* Header */}
      <div style={{ marginBottom: 18 }}>
        <h2 style={{ margin: 0, fontSize: 17, fontWeight: 500 }}>Import Content Types</h2>
        <p style={{ margin: "3px 0 0", fontSize: 12, color: "#666" }}>
          Upload a .zip file — content types will be created or updated via the migration API
        </p>
      </div>

      {/* Drop zone */}
      <div
        onClick={() => fileRef.current?.click()}
        onDragEnter={() => setDragOver(true)}
        onDragLeave={() => setDragOver(false)}
        onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
        onDrop={(e) => {
          e.preventDefault();
          setDragOver(false);
          handleFile(e.dataTransfer.files[0] ?? null);
        }}
        style={{
          border: `1.5px dashed ${isDragOver ? "#185fa5" : file ? "#9fe1cb" : "#d0d0d0"}`,
          borderRadius: 10,
          background: isDragOver ? "#e6f1fb" : file ? "#f0faf6" : "#fafafa",
          padding: "36px 20px",
          textAlign: "center",
          cursor: "pointer",
          marginBottom: 18,
          transition: "all 0.15s",
        }}
      >
        <input
          ref={fileRef}
          type="file"
          accept=".zip"
          style={{ display: "none" }}
          onChange={(e) => handleFile(e.target.files?.[0] ?? null)}
        />
        {/* Icon */}
        <svg width="28" height="28" viewBox="0 0 24 24" fill="none"
          style={{ display: "block", margin: "0 auto 10px", color: file ? "#0f6e56" : "#aaa" }}>
          <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6z"
            stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/>
          <path d="M14 2v6h6M12 12v6M9 15l3 3 3-3"
            stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
        </svg>

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
              Drop .zip here or click to browse
            </div>
            <div style={{ fontSize: 12, color: "#aaa", marginTop: 3 }}>
              Only .zip files exported from this module are supported
            </div>
          </>
        )}
      </div>

      {/* Flow info */}
      <div style={{
        background: "#fafafa", border: "0.5px solid #e0e0e0",
        borderRadius: 8, padding: "14px 16px", marginBottom: 18,
      }}>
        <div style={{ fontSize: 13, fontWeight: 500, marginBottom: 10 }}>Import flow</div>
        {[
          "Module reads .zip → extracts content-types.json",
          "Validates structure of each content type",
          "Calls POST /api/migrate/content-type for each type",
          "Migration API creates or updates the DataClassInfo in XbyK",
        ].map((step, i) => (
          <div key={i} style={{ display: "flex", gap: 9, alignItems: "flex-start", marginBottom: 6 }}>
            <span style={{
              flexShrink: 0, width: 18, height: 18, borderRadius: "50%",
              background: "#e6f1fb", color: "#185fa5",
              display: "inline-flex", alignItems: "center", justifyContent: "center",
              fontSize: 10, fontWeight: 500,
            }}>{i + 1}</span>
            <span style={{ fontSize: 12, color: "#666", lineHeight: 1.5 }}>{step}</span>
          </div>
        ))}
      </div>

      {/* Status */}
      {status === "loading" && (
        <Alert type="info">Processing — reading .zip and calling migration API...</Alert>
      )}
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
          Import failed.
          {result?.errors.length ? ` Errors: ${result.errors.join(", ")}` : " Check API connection."}
        </Alert>
      )}

      {/* Actions */}
      <div style={{ display: "flex", gap: 8 }}>
        <button
          onClick={handleImport}
          disabled={!file || status === "loading"}
          style={{
            padding: "7px 16px", borderRadius: 6, fontSize: 13, fontWeight: 500,
            border: "0.5px solid #185fa5", background: "#185fa5", color: "#fff",
            cursor: !file ? "not-allowed" : "pointer",
            opacity: !file ? 0.45 : 1,
          }}
        >
          {status === "loading" ? "Importing..." : "Import"}
        </button>
        {file && (
          <button
            onClick={() => { setFile(null); setStatus("idle"); setResult(null); }}
            style={{
              padding: "7px 14px", borderRadius: 6, fontSize: 13,
              border: "0.5px solid #bbb", background: "#fff", cursor: "pointer",
            }}
          >
            Clear
          </button>
        )}
      </div>
    </div>
  );
};

// ── Shared Alert ───────────────────────────────────────────────────────────
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
