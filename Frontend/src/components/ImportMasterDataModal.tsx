import React, { useState, useRef } from "react";
import { Upload, FileSpreadsheet, X, AlertCircle, Check } from "lucide-react";
import { apiService } from "../domain/apiService";

interface ImportMasterDataModalProps {
  isOpen: boolean;
  onClose: () => void;
  tableType: "manage" | "profiles" | "antiviruspricelist";
  onImportSuccess: () => void;
  showToast: (msg: string, type: "success" | "error" | "info") => void;
}

export const ImportMasterDataModal: React.FC<ImportMasterDataModalProps> = ({
  isOpen,
  onClose,
  tableType,
  onImportSuccess,
  showToast
}) => {
  const [file, setFile] = useState<File | null>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [previewResult, setPreviewResult] = useState<{
    isValid: boolean;
    totalRows: number;
    errors: any[];
    previewRows: any[];
  } | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  if (!isOpen) return null;

  const typeLabel = tableType === "manage" ? "Manage Customers" : tableType === "profiles" ? "Profiles" : "Antivirus Price List";
  const templateType = tableType === "manage" ? "manage" : tableType === "profiles" ? "profile" : "antivirus-price-list";

  const handleFileChange = (selectedFile: File | null) => {
    if (!selectedFile) return;

    const ext = selectedFile.name.split(".").pop()?.toLowerCase();
    if (ext !== "csv" && ext !== "xlsx") {
      showToast("Unsupported file type. Please select a .csv or .xlsx file.", "error");
      return;
    }

    if (selectedFile.size > 10 * 1024 * 1024) {
      showToast("File is too large. Maximum size is 10MB.", "error");
      return;
    }

    setFile(selectedFile);
    setPreviewResult(null);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  };

  const handleDragLeave = () => {
    setIsDragOver(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      handleFileChange(e.dataTransfer.files[0]);
    }
  };

  const triggerFileSelect = () => {
    fileInputRef.current?.click();
  };

  const handleClear = () => {
    setFile(null);
    setPreviewResult(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  const handlePreview = async () => {
    if (!file) return;

    try {
      setIsLoading(true);
      let res;
      if (tableType === "manage") {
        res = await apiService.importManage(file, false);
      } else if (tableType === "profiles") {
        res = await apiService.importProfile(file, false);
      } else {
        res = await apiService.importAntivirusPriceList(file, false);
      }
      setPreviewResult(res);
      if (res.isValid) {
        showToast("File parsed and validated successfully. No errors found.", "success");
      } else {
        showToast(`Parsed with ${res.errors.length} validation errors. Please fix them before importing.`, "error");
      }
    } catch (err: any) {
      showToast(err.message || "Failed to preview file.", "error");
    } finally {
      setIsLoading(false);
    }
  };

  const handleImport = async () => {
    if (!file || !previewResult || !previewResult.isValid) return;

    try {
      setIsImporting(true);
      if (tableType === "manage") {
        await apiService.importManage(file, true);
      } else if (tableType === "profiles") {
        await apiService.importProfile(file, true);
      } else {
        await apiService.importAntivirusPriceList(file, true);
      }
      showToast(`Successfully imported ${previewResult.totalRows} records!`, "success");
      onImportSuccess();
      onClose();
      handleClear();
    } catch (err: any) {
      showToast(err.message || "Failed to commit import.", "error");
    } finally {
      setIsImporting(false);
    }
  };

  const handleDownloadTemplate = async () => {
    try {
      await apiService.downloadTemplate(templateType);
    } catch (err: any) {
      showToast(err.message || "Failed to download template.", "error");
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-content-box"
        onClick={(e) => e.stopPropagation()}
        style={{ maxWidth: "800px", width: "95%" }}
      >
        <header className="modal-header">
          <h3>Import {typeLabel}</h3>
          <button className="modal-close" onClick={onClose} aria-label="Close dialog" type="button">
            <X size={18} />
          </button>
        </header>

        <div className="modal-body" style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
          {/* Top Info & Download Template */}
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "10px" }}>
            <p style={{ margin: 0, fontSize: "13px", color: "var(--text-muted)" }}>
              Upload your Excel (.xlsx) or CSV file matching the required template columns.
            </p>
            <button
              className="ghost-button"
              type="button"
              onClick={handleDownloadTemplate}
              style={{ display: "flex", alignItems: "center", gap: "6px", border: "1px solid var(--border-color)", padding: "6px 12px" }}
            >
              <FileSpreadsheet size={15} />
              Download Template
            </button>
          </div>

          {/* Upload Area */}
          <div
            className={`drop-zone ${isDragOver ? "drag-over" : ""}`}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            onClick={triggerFileSelect}
            style={{
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              justifyContent: "center",
              gap: "10px",
              padding: "24px",
              border: "2px dashed var(--border-color)",
              borderRadius: "var(--border-radius)",
              cursor: "pointer",
              background: "var(--bg-active)"
            }}
          >
            <input
              type="file"
              ref={fileInputRef}
              onChange={(e) => handleFileChange(e.target.files?.[0] || null)}
              accept=".csv, .xlsx"
              style={{ display: "none" }}
            />
            <Upload size={32} style={{ color: "var(--primary-color)" }} />
            <div style={{ textAlign: "center" }}>
              <strong style={{ display: "block", fontSize: "14px", marginBottom: "2px" }}>
                Drag & Drop file or <span style={{ color: "var(--primary-color)", textDecoration: "underline" }}>browse</span>
              </strong>
              <span style={{ fontSize: "11px", color: "var(--text-muted)" }}>
                CSV & XLSX (Max 10MB)
              </span>
            </div>
          </div>

          {/* Selected File Details */}
          {file && (
            <div
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                padding: "10px 14px",
                background: "var(--bg-active)",
                border: "1px solid var(--border-color)",
                borderRadius: "var(--border-radius)"
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                <FileSpreadsheet style={{ color: "var(--success-color)" }} size={20} />
                <div>
                  <strong style={{ display: "block", fontSize: "13px" }}>{file.name}</strong>
                  <span style={{ fontSize: "11px", color: "var(--text-muted)" }}>
                    {(file.size / 1024).toFixed(1)} KB
                  </span>
                </div>
              </div>
              <button onClick={handleClear} type="button" className="ghost-button" style={{ padding: "4px" }}>
                <X size={16} />
              </button>
            </div>
          )}

          {/* Validation Status & Error Messages */}
          {previewResult && (
            <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "10px",
                  padding: "12px",
                  borderRadius: "var(--border-radius)",
                  background: previewResult.isValid ? "rgba(46, 204, 113, 0.1)" : "rgba(231, 76, 60, 0.1)",
                  border: `1px solid ${previewResult.isValid ? "var(--success-color)" : "var(--error-color)"}`
                }}
              >
                {previewResult.isValid ? (
                  <Check size={20} style={{ color: "var(--success-color)" }} />
                ) : (
                  <AlertCircle size={20} style={{ color: "var(--error-color)" }} />
                )}
                <div>
                  <strong style={{ display: "block", fontSize: "14px", color: previewResult.isValid ? "var(--success-color)" : "var(--error-color)" }}>
                    {previewResult.isValid ? "Validation Passed" : "Validation Failed"}
                  </strong>
                  <span style={{ fontSize: "12px", color: "var(--text-muted)" }}>
                    Total parsed rows: {previewResult.totalRows}. {previewResult.isValid ? "All rows are valid and ready to import." : `Found ${previewResult.errors.length} rows with errors.`}
                  </span>
                </div>
              </div>

              {/* Error Details */}
              {!previewResult.isValid && previewResult.errors.length > 0 && (
                <div style={{ maxHeight: "200px", overflowY: "auto", border: "1px solid var(--border-color)", borderRadius: "var(--border-radius)", padding: "10px", background: "var(--bg-active)" }}>
                  <h4 style={{ margin: "0 0 8px 0", fontSize: "13px", color: "var(--error-color)" }}>Error Details:</h4>
                  <ul style={{ margin: 0, paddingLeft: "16px", fontSize: "12px", display: "flex", flexDirection: "column", gap: "4px" }}>
                    {previewResult.errors.map((err: any, idx: number) => (
                      <li key={idx} style={{ color: "var(--text-color)" }}>
                        <strong>Row {err.row}:</strong> {err.issues.join(", ")}
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Data Preview Table */}
              <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
                <h4 style={{ margin: 0, fontSize: "13px" }}>Data Preview (First 50 Rows):</h4>
                <div style={{ overflowX: "auto", border: "1px solid var(--border-color)", borderRadius: "var(--border-radius)" }}>
                  <table className="corporate-table" style={{ width: "100%", margin: 0, fontSize: "12px" }}>
                    <thead>
                      {tableType === "manage" ? (
                        <tr>
                          <th style={{ width: "10%" }}>Row</th>
                          <th style={{ width: "25%" }}>Company Name</th>
                          <th style={{ width: "25%" }}>Address</th>
                          <th style={{ width: "15%" }}>Contact Name</th>
                          <th style={{ width: "15%" }}>Email</th>
                          <th style={{ width: "10%" }}>Phone</th>
                        </tr>
                      ) : tableType === "profiles" ? (
                        <tr>
                          <th style={{ width: "10%" }}>Row</th>
                          <th style={{ width: "30%" }}>Name</th>
                          <th style={{ width: "20%" }}>Type</th>
                          <th style={{ width: "20%" }}>Items</th>
                          <th style={{ width: "20%" }}>Editions</th>
                        </tr>
                      ) : (
                        <tr>
                          <th style={{ width: "10%" }}>Row</th>
                          <th style={{ width: "25%" }}>Code</th>
                          <th style={{ width: "20%" }}>Start</th>
                          <th style={{ width: "20%" }}>End</th>
                          <th style={{ width: "25%" }}>Cost</th>
                        </tr>
                      )}
                    </thead>
                    <tbody>
                      {previewResult.previewRows.map((r: any, idx: number) => {
                        const hasRowError = !previewResult.isValid && previewResult.errors.some((e: any) => e.row === r.row);
                        return (
                          <tr key={idx} style={{ background: hasRowError ? "rgba(231, 76, 60, 0.05)" : undefined }}>
                            <td>{r.row}</td>
                            {tableType === "manage" ? (
                              <>
                                <td style={{ fontWeight: 600 }}>{r.companyName || <span style={{ color: "var(--error-color)" }}>[Missing]</span>}</td>
                                <td>{r.address || <span style={{ color: "var(--error-color)" }}>[Missing]</span>}</td>
                                <td>{r.contactName || "-"}</td>
                                <td>{r.email || "-"}</td>
                                <td>{r.phone || "-"}</td>
                              </>
                            ) : tableType === "profiles" ? (
                              <>
                                <td style={{ fontWeight: 600 }}>{r.name || <span style={{ color: "var(--error-color)" }}>[Missing]</span>}</td>
                                <td>{r.type || <span style={{ color: "var(--error-color)" }}>[Missing]</span>}</td>
                                <td>{r.items || "-"}</td>
                                <td>{r.editions || "-"}</td>
                              </>
                            ) : (
                              <>
                                <td style={{ fontFamily: "monospace" }}>{r.code || <span style={{ color: "var(--error-color)" }}>[Missing]</span>}</td>
                                <td>{r.start || <span style={{ color: "var(--error-color)" }}>[Missing]</span>}</td>
                                <td>{r.end || <span style={{ color: "var(--error-color)" }}>[Missing]</span>}</td>
                                <td>{r.cost ? `${Number(r.cost).toLocaleString()} THB` : <span style={{ color: "var(--error-color)" }}>[Missing]</span>}</td>
                              </>
                            )}
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}
        </div>

        <footer className="modal-footer" style={{ display: "flex", justifyContent: "flex-end", gap: "10px", marginTop: "24px" }}>
          <button className="ghost-button" type="button" onClick={onClose} disabled={isLoading || isImporting}>
            Cancel
          </button>
          {!previewResult ? (
            <button
              className="primary-button"
              type="button"
              onClick={handlePreview}
              disabled={!file || isLoading}
            >
              {isLoading ? "Analyzing..." : "Analyze & Preview"}
            </button>
          ) : (
            <button
              className="primary-button"
              type="button"
              onClick={handleImport}
              disabled={!previewResult.isValid || isImporting}
            >
              {isImporting ? "Importing..." : "Commit Import"}
            </button>
          )}
        </footer>
      </div>
    </div>
  );
};
