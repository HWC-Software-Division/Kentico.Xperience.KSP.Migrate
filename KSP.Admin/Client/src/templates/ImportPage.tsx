import React, { useRef, useState } from "react";
import { ApiResponse, BasePageProps, ImportResult } from "../types";

const STYLE = `
  .ksp-ct, .ksp-ct h2, .ksp-ct h3, .ksp-ct p, .ksp-ct td, .ksp-ct div { color: inherit !important; }
  .ksp-ct { color: #1a1a1a !important; }
  .ksp-ct .ksp-subtle { color: #666 !important; }
`;

function NameList({ names, emptyText }: { names: string[]; emptyText: string }) {
  if (names.length === 0)
    return <span style={{ fontSize: 12, color: "#aaa" }}>{emptyText}</span>;
  return (
    <ul style={{ margin: "4px 0 0 0", padding: "0 0 0 16px" }}>
      {names.map(n => (
        <li key={n} style={{ fontSize: 12, color: "#1a1a1a", padding: "1px 0" }}>{n}</li>
      ))}
    </ul>
  );
}

function SectionGrid({ created, updated, createdNames, updatedNames, errors, accent }: {
  created: number; updated: number;
  createdNames: string[]; updatedNames: string[];
  errors: string[];
  accent: { label: string; createdBg: string; createdColor: string; updatedBg: string; updatedColor: string; errorBg: string; errorColor: string; };
}) {
  return (
    <>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
        <div style={{ background: accent.createdBg, borderRadius: 7, padding: "10px 12px" }}>
          <div style={{ fontSize: 11, color: accent.createdColor, marginBottom: 4 }}>Created ({created})</div>
          <NameList names={createdNames} emptyText="None" />
        </div>
        <div style={{ background: accent.updatedBg, borderRadius: 7, padding: "10px 12px" }}>
          <div style={{ fontSize: 11, color: accent.updatedColor, marginBottom: 4 }}>Updated ({updated})</div>
          <NameList names={updatedNames} emptyText="None" />
        </div>
      </div>
      {errors.length > 0 && (
        <div style={{ marginTop: 8, background: accent.errorBg, borderRadius: 7, padding: "10px 12px" }}>
          <div style={{ fontSize: 11, color: accent.errorColor, marginBottom: 4 }}>Errors ({errors.length})</div>
          <NameList names={errors} emptyText="" />
        </div>
      )}
    </>
  );
}

function ResultModal({ result, onClose }: { result: ImportResult; onClose: () => void }) {
  const total = result.created + result.updated
              + result.reusableCreated + result.reusableUpdated
              + result.schemaCreated + result.schemaUpdated;

  const hasReusable = (result.reusableCreated + result.reusableUpdated + result.reusableErrors.length) > 0;
  const hasSchemas  = (result.schemaCreated  + result.schemaUpdated  + result.schemaErrors.length)  > 0;

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.45)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 1000 }}>
      <div style={{ background: "#fff", borderRadius: 12, padding: 24, width: 500, maxWidth: "90vw", maxHeight: "82vh", overflow: "auto", boxShadow: "0 8px 32px rgba(0,0,0,0.18)" }}>
        <h3 style={{ margin: "0 0 4px", fontSize: 15, fontWeight: 600, color: "#1a1a1a" }}>Import Complete</h3>
        <p style={{ margin: "0 0 18px", fontSize: 13, color: "#666" }}>
          {total} item{total !== 1 ? "s" : ""} imported successfully.
        </p>

        {/* ── Content Types ── */}
        <div style={{ marginBottom: 16 }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: "#185fa5", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 10 }}>
            Content Types
          </div>
          <SectionGrid
            created={result.created} updated={result.updated}
            createdNames={result.createdNames} updatedNames={result.updatedNames}
            errors={result.errors}
            accent={{
              label: "Content Types",
              createdBg: "#eaf3de", createdColor: "#3b6d11",
              updatedBg: "#e6f1fb", updatedColor: "#185fa5",
              errorBg: "#fcebeb",  errorColor: "#a32d2d",
            }}
          />
        </div>

        {/* ── Reusable Fields ── */}
        {hasReusable && (
          <div style={{ marginBottom: 16 }}>
            <div style={{ fontSize: 12, fontWeight: 600, color: "#7c4f00", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 10 }}>
              Reusable Fields
            </div>
            <SectionGrid
              created={result.reusableCreated} updated={result.reusableUpdated}
              createdNames={result.reusableCreatedNames} updatedNames={result.reusableUpdatedNames}
              errors={result.reusableErrors}
              accent={{
                label: "Reusable Fields",
                createdBg: "#fdf3e6", createdColor: "#7c4f00",
                updatedBg: "#fdf3e6", updatedColor: "#7c4f00",
                errorBg: "#fcebeb",  errorColor: "#a32d2d",
              }}
            />
          </div>
        )}

        {/* ── Field Schemas ── */}
        {hasSchemas && (
          <div style={{ marginBottom: 16 }}>
            <div style={{ fontSize: 12, fontWeight: 600, color: "#4f46e5", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 10 }}>
              Field Schemas
            </div>
            <SectionGrid
              created={result.schemaCreated} updated={result.schemaUpdated}
              createdNames={result.schemaCreatedNames} updatedNames={result.schemaUpdatedNames}
              errors={result.schemaErrors}
              accent={{
                label: "Field Schemas",
                createdBg: "#eeebff", createdColor: "#4f46e5",
                updatedBg: "#eeebff", updatedColor: "#4f46e5",
                errorBg: "#fcebeb",  errorColor: "#a32d2d",
              }}
            />
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <button onClick={onClose}
            style={{ padding: "7px 20px", borderRadius: 6, fontSize: 13, fontWeight: 500, background: "#185fa5", color: "#fff", border: "0.5px solid #185fa5", cursor: "pointer" }}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

export function ImportPage(props: BasePageProps) {
  const [file,       setFile]       = useState<File | null>(null);
  const [status,     setStatus]     = useState<"idle"|"loading"|"success"|"error">("idle");
  const [result,     setResult]     = useState<ImportResult | null>(null);
  const [showModal,  setShowModal]  = useState(false);
  const [isDragOver, setIsDragOver] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);

  const handleFile = (f: File | null) => {
    if (!f) return;
    setFile(f); setStatus("idle"); setResult(null); setShowModal(false);
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
      if (json.data) setShowModal(true);
    } catch { setStatus("error"); }
  };

  const steps = [
    "Reads .zip → extracts content-types.json, reusable-fields.json, reusable-field-schemas.json",
    "Imports Field Schemas first (may be referenced by content types)",
    "Imports Reusable Fields second (dependencies of content types)",
    "Imports Content Types last (creates or updates DataClassInfo)",
    "Returns detailed result with names of all imported items",
  ];

  const totalImported = result
    ? result.created + result.updated + result.reusableCreated + result.reusableUpdated
      + result.schemaCreated + result.schemaUpdated
    : 0;

  return (
    <div className="ksp-ct" style={{ padding: 24, fontFamily: "system-ui, sans-serif" }}>
      <style>{STYLE}</style>

      {showModal && result && (
        <ResultModal result={result} onClose={() => setShowModal(false)} />
      )}

      <div style={{ marginBottom: 18 }}>
        <h2 style={{ margin: 0, fontSize: 17, fontWeight: 500, color: "#1a1a1a" }}>Import Content Types</h2>
        <p className="ksp-subtle" style={{ margin: "3px 0 0", fontSize: 12 }}>
          Upload a .zip file — content types, reusable fields, and field schemas will be created or updated
        </p>
      </div>

      {/* Drop zone */}
      <div
        onClick={() => fileRef.current?.click()}
        onDragEnter={() => setIsDragOver(true)}
        onDragLeave={() => setIsDragOver(false)}
        onDragOver={e => { e.preventDefault(); setIsDragOver(true); }}
        onDrop={e => { e.preventDefault(); setIsDragOver(false); handleFile(e.dataTransfer.files[0] ?? null); }}
        style={{ border: `1.5px dashed ${isDragOver ? "#185fa5" : file ? "#9fe1cb" : "#d0d0d0"}`, borderRadius: 10, background: isDragOver ? "#e6f1fb" : file ? "#f0faf6" : "#fafafa", padding: "36px 20px", textAlign: "center", cursor: "pointer", marginBottom: 18 }}>
        <input ref={fileRef} type="file" accept=".zip" style={{ display: "none" }}
          onChange={e => handleFile(e.target.files?.[0] ?? null)} />
        {file ? (
          <>
            <div style={{ fontSize: 14, fontWeight: 500, color: "#0f6e56" }}>{file.name}</div>
            <div style={{ fontSize: 12, color: "#3b8c6a", marginTop: 3 }}>{(file.size / 1024).toFixed(1)} KB — click to change</div>
          </>
        ) : (
          <>
            <div style={{ fontSize: 14, fontWeight: 500, color: "#444" }}>Drop .zip here or click to browse</div>
            <div style={{ fontSize: 12, color: "#aaa", marginTop: 3 }}>Only .zip files exported from this module are supported</div>
          </>
        )}
      </div>

      {/* Import flow */}
      <div style={{ background: "#fafafa", border: "0.5px solid #e0e0e0", borderRadius: 8, padding: "14px 16px", marginBottom: 18 }}>
        <div style={{ fontSize: 13, fontWeight: 500, marginBottom: 10, color: "#1a1a1a" }}>Import flow</div>
        {steps.map((s, i) => (
          <div key={i} style={{ display: "flex", gap: 9, alignItems: "flex-start", marginBottom: 6 }}>
            <span style={{ flexShrink: 0, width: 18, height: 18, borderRadius: "50%", background: "#e6f1fb", color: "#185fa5", display: "inline-flex", alignItems: "center", justifyContent: "center", fontSize: 10, fontWeight: 500 }}>{i + 1}</span>
            <span style={{ fontSize: 12, color: "#666", lineHeight: 1.5 }}>{s}</span>
          </div>
        ))}
      </div>

      {/* Status banners */}
      {status === "loading" && <div style={{ padding: "11px 14px", marginBottom: 14, borderRadius: 7, background: "#e6f1fb", border: "0.5px solid #b5d4f4", color: "#185fa5", fontSize: 13 }}>Processing…</div>}
      {status === "success" && result != null && (
        <div style={{ padding: "11px 14px", marginBottom: 14, borderRadius: 7, background: "#eaf3de", border: "0.5px solid #c0dd97", color: "#3b6d11", fontSize: 13, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span>
            Import complete —{" "}
            {result.created + result.reusableCreated + result.schemaCreated} created,{" "}
            {result.updated + result.reusableUpdated + result.schemaUpdated} updated.
          </span>
          <button onClick={() => setShowModal(true)}
            style={{ padding: "3px 10px", fontSize: 12, borderRadius: 5, border: "0.5px solid #3b6d11", background: "transparent", color: "#3b6d11", cursor: "pointer" }}>
            Details
          </button>
        </div>
      )}
      {status === "error" && !result && <div style={{ padding: "11px 14px", marginBottom: 14, borderRadius: 7, background: "#fcebeb", border: "0.5px solid #f7c1c1", color: "#a32d2d", fontSize: 13 }}>Import failed. Check API connection.</div>}

      {/* Buttons */}
      <div style={{ display: "flex", gap: 8 }}>
        <button onClick={handleImport} disabled={!file || status === "loading"}
          style={{ padding: "7px 16px", borderRadius: 6, fontSize: 13, fontWeight: 500, border: "0.5px solid #185fa5", background: "#185fa5", color: "#fff", cursor: !file ? "not-allowed" : "pointer", opacity: !file ? 0.45 : 1 }}>
          {status === "loading" ? "Importing…" : "Import"}
        </button>
        {file != null && (
          <button onClick={() => { setFile(null); setStatus("idle"); setResult(null); setShowModal(false); }}
            style={{ padding: "7px 14px", borderRadius: 6, fontSize: 13, border: "0.5px solid #bbb", background: "#fff", cursor: "pointer" }}>
            Clear
          </button>
        )}
        {result != null && status === "success" && (
          <button onClick={() => setShowModal(true)}
            style={{ padding: "7px 14px", borderRadius: 6, fontSize: 13, border: "0.5px solid #185fa5", background: "#fff", color: "#185fa5", cursor: "pointer" }}>
            View Results
          </button>
        )}
      </div>
    </div>
  );
}
