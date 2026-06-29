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

  const handleDownloadTemplate = async (format: "xlsx" | "csv" = "xlsx") => {
    try {
      await apiService.downloadTemplate(templateType, format);
    } catch (err: any) {
      showToast(err.message || "Failed to download template.", "error");
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal-content-box"
        onClick={(e) => e.stopPropagation()}
        style={{ maxWidth: "800px", width: "95%", maxHeight: "90vh", display: "flex", flexDirection: "column" }}
      >
        <header className="modal-header tech-modal-header">
          <h3>
            <Upload size={18} />
            Import {typeLabel}
          </h3>
          <button className="modal-close" onClick={onClose} aria-label="Close dialog" type="button">
            <X size={18} />
          </button>
        </header>

        <div className="modal-body" style={{ display: "flex", flexDirection: "column", gap: "20px", padding: "24px", overflowY: "auto" }}>
          {/* Top Info & Download Template Grid */}
          <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
            <p style={{ margin: 0, fontSize: "13px", color: "var(--text-muted)", lineHeight: "1.5" }}>
              Select a spreadsheet schematic template below to ensure proper column configurations, then load your file for system diagnostics.
            </p>
            <div className="tech-template-grid">
              <div className="tech-template-card" onClick={() => handleDownloadTemplate("xlsx")}>
                <div className="tech-template-card-info">
                  <div className="tech-template-icon-container">
                    <FileSpreadsheet size={16} />
                  </div>
                  <div>
                    <div style={{ fontSize: "13px", fontWeight: 700, color: "var(--iic-navy)" }}>Excel Schematic</div>
                    <div style={{ fontSize: "11px", color: "var(--text-muted)" }}>Download template worksheet</div>
                  </div>
                </div>
                <span className="tech-template-format-badge">.xlsx</span>
              </div>
              <div className="tech-template-card" onClick={() => handleDownloadTemplate("csv")}>
                <div className="tech-template-card-info">
                  <div className="tech-template-icon-container">
                    <FileSpreadsheet size={16} />
                  </div>
                  <div>
                    <div style={{ fontSize: "13px", fontWeight: 700, color: "var(--iic-navy)" }}>CSV Schematic</div>
                    <div style={{ fontSize: "11px", color: "var(--text-muted)" }}>Download flat data layout</div>
                  </div>
                </div>
                <span className="tech-template-format-badge">.csv</span>
              </div>
            </div>
          </div>

          {/* Advanced Drop Zone */}
          <div
            className={`tech-dropzone ${isDragOver ? "drag-over" : ""}`}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            onClick={triggerFileSelect}
          >
            {/* Tech Corner Brackets */}
            <span className="tech-bracket top-left"></span>
            <span className="tech-bracket top-right"></span>
            <span className="tech-bracket bottom-left"></span>
            <span className="tech-bracket bottom-right"></span>

            {/* Scanner line active during loading states */}
            {(isLoading || isImporting) && <div className="scanner-line"></div>}

            <input
              type="file"
              ref={fileInputRef}
              onChange={(e) => handleFileChange(e.target.files?.[0] || null)}
              accept=".csv, .xlsx"
              style={{ display: "none" }}
            />
            
            <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "12px" }}>
              <div style={{
                width: "48px",
                height: "48px",
                borderRadius: "50%",
                background: isDragOver ? "rgba(0, 91, 187, 0.1)" : "rgba(0, 91, 187, 0.05)",
                color: "var(--iic-blue)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                transition: "all 0.2s"
              }}>
                <Upload size={22} />
              </div>
              <div>
                <strong style={{ display: "block", fontSize: "14px", color: "var(--iic-navy)", marginBottom: "4px" }}>
                  {isDragOver ? "Drop spreadsheet here..." : "Drag & Drop spreadsheet or click to browse"}
                </strong>
                <span style={{ fontSize: "12px", color: "var(--text-muted)" }}>
                  Supports CSV & XLSX (Max size 10MB)
                </span>
              </div>
            </div>
          </div>

          {/* Selected File Monospace Readout */}
          {file && (
            <div className="tech-readout" style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderBottom: "1px solid rgba(255, 255, 255, 0.15)", paddingBottom: "8px", marginBottom: "8px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                  <span className="tech-status-dot active pulse" style={{ color: "#38bdf8" }}></span>
                  <strong style={{ color: "#38bdf8", textTransform: "uppercase", letterSpacing: "1px", fontSize: "11px" }}>
                    Diagnostic Console: File Loaded
                  </strong>
                </div>
                <button 
                  onClick={handleClear} 
                  type="button" 
                  style={{ 
                    background: "rgba(255, 255, 255, 0.08)", 
                    border: "none", 
                    borderRadius: "4px", 
                    color: "#cbd5e1", 
                    padding: "4px 8px", 
                    fontSize: "11px", 
                    cursor: "pointer",
                    display: "flex",
                    alignItems: "center",
                    gap: "4px"
                  }}
                >
                  <X size={12} /> Clear File
                </button>
              </div>
              <div className="tech-readout-line">
                <span className="tech-readout-label">FILE_NAME:</span>
                <span className="tech-readout-val" style={{ fontWeight: 600 }}>{file.name}</span>
              </div>
              <div className="tech-readout-line">
                <span className="tech-readout-label">FILE_SIZE:</span>
                <span className="tech-readout-val">{(file.size / 1024).toFixed(1)} KB</span>
              </div>
              <div className="tech-readout-line">
                <span className="tech-readout-label">FILE_TYPE:</span>
                <span className="tech-readout-val">{file.name.split(".").pop()?.toUpperCase()} Spreadsheet</span>
              </div>
              <div className="tech-readout-line">
                <span className="tech-readout-label">PARSING_STATUS:</span>
                <span className="tech-readout-val" style={{ color: previewResult ? (previewResult.isValid ? "#4ade80" : "#f87171") : "#f59e0b" }}>
                  {previewResult ? (previewResult.isValid ? "VERIFIED_OK" : "VALIDATION_FAILED") : "AWAITING_ANALYSIS"}
                </span>
              </div>
            </div>
          )}

          {/* Validation Status & Diagnostic Reports */}
          {previewResult && (
            <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
              <div className={`tech-alert ${previewResult.isValid ? "success" : "error"}`}>
                <div style={{ marginTop: "2px" }}>
                  {previewResult.isValid ? (
                    <div style={{ color: "var(--iic-success)", display: "flex", alignItems: "center", justifyContent: "center", width: "24px", height: "24px", borderRadius: "50%", background: "rgba(21, 128, 61, 0.1)" }}>
                      <Check size={16} />
                    </div>
                  ) : (
                    <div style={{ color: "var(--iic-danger)", display: "flex", alignItems: "center", justifyContent: "center", width: "24px", height: "24px", borderRadius: "50%", background: "rgba(185, 28, 28, 0.1)" }}>
                      <AlertCircle size={16} />
                    </div>
                  )}
                </div>
                <div>
                  <strong style={{ display: "block", fontSize: "14px", color: previewResult.isValid ? "var(--iic-success)" : "var(--iic-danger)", marginBottom: "4px" }}>
                    {previewResult.isValid ? "DIAGNOSTICS PASSED: READY FOR COMMIT" : "DIAGNOSTICS FAILED: ERRORS DETECTED"}
                  </strong>
                  <span style={{ fontSize: "12px", color: "var(--iic-muted)" }}>
                    Total parsed rows: <strong>{previewResult.totalRows}</strong>. {previewResult.isValid ? "Data integrity check completed successfully. Ready to import into database." : `Database parser rejected the sheet due to ${previewResult.errors.length} validation errors.`}
                  </span>
                </div>
              </div>

              {/* Error Details */}
              {!previewResult.isValid && previewResult.errors.length > 0 && (
                <div className="tech-readout" style={{ maxHeight: "200px", overflowY: "auto" }}>
                  <div style={{ display: "flex", alignItems: "center", gap: "8px", borderBottom: "1px solid rgba(255, 255, 255, 0.15)", paddingBottom: "6px", marginBottom: "8px" }}>
                    <span className="tech-status-dot error pulse" style={{ color: "#f87171" }}></span>
                    <strong style={{ color: "#f87171", textTransform: "uppercase", letterSpacing: "1px", fontSize: "11px" }}>
                      Diagnostic Report: Issues Found
                    </strong>
                  </div>
                  <ul style={{ margin: 0, paddingLeft: "16px", fontSize: "12px", display: "flex", flexDirection: "column", gap: "6px", listStyleType: "square", color: "#cbd5e1" }}>
                    {previewResult.errors.map((err: any, idx: number) => (
                      <li key={idx}>
                        <strong style={{ color: "#f87171" }}>Row {err.row}:</strong> {err.issues.join(", ")}
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Data Preview Table */}
              <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                <h4 style={{ margin: 0, fontSize: "13px", color: "var(--iic-navy)", fontWeight: 700 }}>
                  Spreadsheet Data Preview (First 50 Rows):
                </h4>
                <div className="tech-preview-container">
                  <table className="tech-table">
                    <thead>
                      {tableType === "manage" ? (
                        <tr>
                          <th style={{ width: "10%" }} className="row-num">Row</th>
                          <th style={{ width: "25%" }}>Company Name</th>
                          <th style={{ width: "25%" }}>Address</th>
                          <th style={{ width: "15%" }}>Contact Name</th>
                          <th style={{ width: "15%" }}>Email</th>
                          <th style={{ width: "10%" }}>Phone</th>
                        </tr>
                      ) : tableType === "profiles" ? (
                        <tr>
                          <th style={{ width: "10%" }} className="row-num">Row</th>
                          <th style={{ width: "30%" }}>Name</th>
                          <th style={{ width: "20%" }}>Type</th>
                          <th style={{ width: "20%" }}>Items</th>
                          <th style={{ width: "20%" }}>Editions</th>
                        </tr>
                      ) : (
                        <tr>
                          <th style={{ width: "10%" }} className="row-num">Row</th>
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
                          <tr key={idx} style={{ background: hasRowError ? "rgba(185, 28, 28, 0.05)" : undefined }}>
                            <td className="row-num">{r.row}</td>
                            {tableType === "manage" ? (
                              <>
                                <td style={{ fontWeight: 600 }}>{r.companyName || <span style={{ color: "var(--iic-danger)", fontSize: "11px", fontWeight: "bold" }}>[MISSING]</span>}</td>
                                <td>{r.address || <span style={{ color: "var(--iic-danger)", fontSize: "11px", fontWeight: "bold" }}>[MISSING]</span>}</td>
                                <td>{r.contactName || "-"}</td>
                                <td>{r.email || "-"}</td>
                                <td>{r.phone || "-"}</td>
                              </>
                            ) : tableType === "profiles" ? (
                              <>
                                <td style={{ fontWeight: 600 }}>{r.name || <span style={{ color: "var(--iic-danger)", fontSize: "11px", fontWeight: "bold" }}>[MISSING]</span>}</td>
                                <td>{r.type || <span style={{ color: "var(--iic-danger)", fontSize: "11px", fontWeight: "bold" }}>[MISSING]</span>}</td>
                                <td>{r.items || "-"}</td>
                                <td>{r.editions || "-"}</td>
                              </>
                            ) : (
                              <>
                                <td style={{ fontFamily: "monospace" }}>{r.code || <span style={{ color: "var(--iic-danger)", fontSize: "11px", fontWeight: "bold" }}>[MISSING]</span>}</td>
                                <td>{r.start || <span style={{ color: "var(--iic-danger)", fontSize: "11px", fontWeight: "bold" }}>[MISSING]</span>}</td>
                                <td>{r.end || <span style={{ color: "var(--iic-danger)", fontSize: "11px", fontWeight: "bold" }}>[MISSING]</span>}</td>
                                <td>{r.cost ? `${Number(r.cost).toLocaleString()} THB` : <span style={{ color: "var(--iic-danger)", fontSize: "11px", fontWeight: "bold" }}>[MISSING]</span>}</td>
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

        <footer className="modal-footer" style={{ display: "flex", justifyContent: "flex-end", gap: "10px", padding: "16px 24px", borderTop: "1px solid var(--iic-border)" }}>
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
              {isLoading ? "Running System Diagnostics..." : "Analyze & Preview"}
            </button>
          ) : (
            <button
              className="primary-button"
              type="button"
              onClick={handleImport}
              disabled={!previewResult.isValid || isImporting}
            >
              {isImporting ? "Integrating Database..." : "Commit Import"}
            </button>
          )}
        </footer>
      </div>
    </div>
  );
};
