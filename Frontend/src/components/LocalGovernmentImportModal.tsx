import React, { useMemo, useRef, useState } from "react";
import { AlertCircle, Check, FileSpreadsheet, Upload, X } from "lucide-react";
import { apiService } from "../domain/apiService";
import type {
  LocalGovernmentImportConfirmResult,
  LocalGovernmentImportIssue,
  LocalGovernmentImportPreviewSummary,
  LocalGovernmentImportRowPreview
} from "../domain/types";

interface LocalGovernmentImportModalProps {
  isOpen: boolean;
  onClose: () => void;
  onImportSuccess: () => void;
  showToast: (msg: string, type: "success" | "error" | "info") => void;
}

const PREVIEW_PAGE_SIZES = [10, 25, 50] as const;
const ISSUE_DISPLAY_LIMIT = 20;

const SummaryItem = ({ label, value }: { label: string; value: number }) => (
  <div className="metric-card" style={{ minHeight: "auto", padding: "12px" }}>
    <div>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  </div>
);

const IssueList = ({ title, issues }: { title: string; issues: LocalGovernmentImportIssue[] }) => {
  if (issues.length === 0) return null;

  const visibleIssues = issues.slice(0, ISSUE_DISPLAY_LIMIT);

  return (
    <div className="tech-readout" style={{ maxHeight: "180px", overflowY: "auto" }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "12px" }}>
        <strong style={{ color: "#cbd5e1", fontSize: "12px" }}>{title}</strong>
        <span style={{ color: "#94a3b8", fontSize: "11px" }}>
          Showing {visibleIssues.length} of {issues.length}
        </span>
      </div>
      <ul style={{ margin: "8px 0 0", paddingLeft: "18px", color: "#cbd5e1", fontSize: "12px" }}>
        {visibleIssues.map((issue, index) => (
          <li key={`${issue.rowNumber ?? "global"}-${issue.field}-${index}`}>
            {issue.rowNumber ? `แถว ${issue.rowNumber}: ` : ""}
            {issue.field}: {issue.message}
          </li>
        ))}
      </ul>
    </div>
  );
};

const getRowStatus = (row: LocalGovernmentImportRowPreview) => {
  if (row.errors.length > 0) return "ข้อมูลผิดพลาด";
  if (row.isDuplicate) return "ข้อมูลซ้ำ";
  return "พร้อมเพิ่ม";
};

const getRowIssueSummary = (row: LocalGovernmentImportRowPreview) => {
  const issues = [...row.errors, ...row.warnings];
  if (issues.length === 0) return "-";
  return issues.map((issue) => issue.message).join("; ");
};

export const LocalGovernmentImportModal: React.FC<LocalGovernmentImportModalProps> = ({
  isOpen,
  onClose,
  onImportSuccess,
  showToast
}) => {
  const [file, setFile] = useState<File | null>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [preview, setPreview] = useState<LocalGovernmentImportPreviewSummary | null>(null);
  const [result, setResult] = useState<LocalGovernmentImportConfirmResult | null>(null);
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isConfirming, setIsConfirming] = useState(false);
  const [previewPage, setPreviewPage] = useState(1);
  const [previewPageSize, setPreviewPageSize] = useState<(typeof PREVIEW_PAGE_SIZES)[number]>(10);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const totalPreviewRows = preview?.rows.length ?? 0;
  const totalPreviewPages = Math.max(1, Math.ceil(totalPreviewRows / previewPageSize));
  const previewStartIndex = (previewPage - 1) * previewPageSize;
  const paginatedRows = useMemo(
    () => preview?.rows.slice(previewStartIndex, previewStartIndex + previewPageSize) ?? [],
    [preview, previewStartIndex, previewPageSize]
  );

  if (!isOpen) return null;

  const canCommit = Boolean(file && preview && preview.estimatedCustomersToInsert > 0 && !result);

  const resetPreviewState = () => {
    setPreview(null);
    setResult(null);
    setPreviewPage(1);
  };

  const handleFileChange = (selectedFile: File | null) => {
    if (!selectedFile) return;

    const ext = selectedFile.name.split(".").pop()?.toLowerCase();
    if (ext !== "csv" && ext !== "xlsx") {
      showToast("รองรับเฉพาะไฟล์ .csv หรือ .xlsx", "error");
      return;
    }

    if (selectedFile.size > 10 * 1024 * 1024) {
      showToast("ไฟล์มีขนาดเกิน 10MB", "error");
      return;
    }

    setFile(selectedFile);
    resetPreviewState();
  };

  const handleDragOver = (event: React.DragEvent) => {
    event.preventDefault();
    setIsDragOver(true);
  };

  const handleDragLeave = () => {
    setIsDragOver(false);
  };

  const handleDrop = (event: React.DragEvent) => {
    event.preventDefault();
    setIsDragOver(false);
    handleFileChange(event.dataTransfer.files?.[0] ?? null);
  };

  const handleClear = () => {
    setFile(null);
    resetPreviewState();
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  const handlePreview = async () => {
    if (!file) return;

    try {
      setIsPreviewing(true);
      setResult(null);
      const response = await apiService.previewLocalGovernmentImport(file);
      setPreview(response);
      setPreviewPage(1);
      if (response.estimatedCustomersToInsert > 0) {
        showToast("File parsed and validated successfully.", "success");
      } else if (response.duplicateRows > 0 || response.errorRows > 0) {
        showToast("No rows are ready to import.", "info");
      }
    } catch (err: any) {
      showToast(err.message || "Failed to preview file.", "error");
    } finally {
      setIsPreviewing(false);
    }
  };

  const handleCommit = async () => {
    if (!file || !canCommit) return;

    try {
      setIsConfirming(true);
      const response = await apiService.confirmLocalGovernmentImport(file);
      setResult(response);
      showToast("Import completed successfully.", "success");
      onImportSuccess();
    } catch (err: any) {
      showToast(err.message || "Failed to commit import.", "error");
    } finally {
      setIsConfirming(false);
    }
  };

  const handleDownloadTemplate = async () => {
    try {
      await apiService.downloadTemplate("local-government", "csv");
    } catch (err: any) {
      showToast(err.message || "Failed to download template.", "error");
    }
  };

  const handleClose = () => {
    handleClear();
    onClose();
  };

  const handlePageSizeChange = (nextPageSize: number) => {
    const safePageSize = PREVIEW_PAGE_SIZES.includes(nextPageSize as any)
      ? (nextPageSize as (typeof PREVIEW_PAGE_SIZES)[number])
      : 10;
    setPreviewPageSize(safePageSize);
    setPreviewPage(1);
  };

  const startRecord = totalPreviewRows === 0 ? 0 : previewStartIndex + 1;
  const endRecord = Math.min(previewStartIndex + previewPageSize, totalPreviewRows);

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div
        className="modal-content-box"
        role="dialog"
        aria-modal="true"
        aria-labelledby="local-government-import-title"
        onClick={(event) => event.stopPropagation()}
        style={{ maxWidth: "840px", width: "95%", maxHeight: "90vh", display: "flex", flexDirection: "column" }}
      >
        <header className="modal-header tech-modal-header">
          <h3 id="local-government-import-title">
            <Upload size={18} />
            Import เทศบาล/อปท.
          </h3>
          <button className="modal-close" onClick={handleClose} aria-label="Close dialog" type="button">
            <X size={18} />
          </button>
        </header>

        <div className="modal-body" style={{ display: "flex", flexDirection: "column", gap: "20px", padding: "24px", overflowY: "auto" }}>
          <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
            <p style={{ margin: 0, fontSize: "13px", color: "var(--text-muted)", lineHeight: "1.5" }}>
              Select the local government import template, then load your Google Sheet export for system diagnostics.
            </p>
            <div className="tech-template-grid">
              <div className="tech-template-card" onClick={handleDownloadTemplate}>
                <div className="tech-template-card-info">
                  <div className="tech-template-icon-container">
                    <FileSpreadsheet size={16} />
                  </div>
                  <div>
                    <div style={{ fontSize: "13px", fontWeight: 700, color: "var(--iic-navy)" }}>CSV Schematic</div>
                    <div style={{ fontSize: "11px", color: "var(--text-muted)" }}>Download municipality template</div>
                  </div>
                </div>
                <span className="tech-template-format-badge">.csv</span>
              </div>
            </div>
          </div>

          <div
            className={`tech-dropzone ${isDragOver ? "drag-over" : ""}`}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            onClick={() => fileInputRef.current?.click()}
          >
            <span className="tech-bracket top-left"></span>
            <span className="tech-bracket top-right"></span>
            <span className="tech-bracket bottom-left"></span>
            <span className="tech-bracket bottom-right"></span>
            {(isPreviewing || isConfirming) && <div className="scanner-line"></div>}

            <input
              ref={fileInputRef}
              aria-label="เลือกไฟล์เทศบาล/อปท."
              type="file"
              accept=".csv,.xlsx"
              onChange={(event) => handleFileChange(event.target.files?.[0] || null)}
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
                  {file ? file.name : isDragOver ? "Drop spreadsheet here..." : "Drag & Drop spreadsheet or click to browse"}
                </strong>
                <span style={{ fontSize: "12px", color: "var(--text-muted)" }}>
                  Supports CSV & XLSX from Google Sheet exports (Max size 10MB)
                </span>
              </div>
            </div>
          </div>

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
                <span className="tech-readout-label">PARSING_STATUS:</span>
                <span className="tech-readout-val" style={{ color: preview ? (canCommit ? "#4ade80" : "#f87171") : "#f59e0b" }}>
                  {preview ? (canCommit ? "VERIFIED_OK" : "NO_IMPORTABLE_ROWS") : "AWAITING_ANALYSIS"}
                </span>
              </div>
            </div>
          )}

          {preview && (
            <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
              <div className={`tech-alert ${canCommit ? "success" : "error"}`}>
                <div style={{ marginTop: "2px" }}>
                  {canCommit ? (
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
                  <strong style={{ display: "block", fontSize: "14px", color: canCommit ? "var(--iic-success)" : "var(--iic-danger)", marginBottom: "4px" }}>
                    {canCommit ? "DIAGNOSTICS PASSED: READY FOR COMMIT" : "DIAGNOSTICS FAILED: NO IMPORTABLE ROWS"}
                  </strong>
                  <span style={{ fontSize: "12px", color: "var(--iic-muted)" }}>
                    Total parsed rows: <strong>{preview.totalRows}</strong>. Ready rows: <strong>{preview.estimatedCustomersToInsert}</strong>.
                  </span>
                </div>
              </div>

              <div className="metrics-grid" style={{ gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))" }}>
                <SummaryItem label="แถวทั้งหมด" value={preview.totalRows} />
                <SummaryItem label="แถวพร้อมนำเข้า" value={preview.estimatedCustomersToInsert} />
                <SummaryItem label="ข้อมูลซ้ำ" value={preview.duplicateRows} />
                <SummaryItem label="ข้อมูลผิดพลาด" value={preview.errorRows} />
                <SummaryItem label="ผู้ติดต่อที่จะเพิ่ม" value={preview.estimatedDetailsToInsert} />
              </div>

              <IssueList title="คำเตือน" issues={preview.warnings} />
              <IssueList title="ข้อผิดพลาด" issues={preview.errors} />

              {totalPreviewRows > 0 && (
                <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                  <h4 style={{ margin: 0, fontSize: "13px", color: "var(--iic-navy)", fontWeight: 700 }}>
                    Spreadsheet Data Preview
                  </h4>
                  <div className="tech-preview-container">
                    <table className="tech-table">
                      <thead>
                        <tr>
                          <th style={{ width: "10%" }} className="row-num">Row</th>
                          <th style={{ width: "34%" }}>ชื่อหน่วยงาน</th>
                          <th style={{ width: "18%" }}>สถานะ</th>
                          <th style={{ width: "16%" }}>ผู้ติดต่อที่จะเพิ่ม</th>
                          <th style={{ width: "22%" }}>คำเตือน/ข้อผิดพลาด</th>
                        </tr>
                      </thead>
                      <tbody>
                        {paginatedRows.map((row) => (
                          <tr key={row.rowNumber} style={{ background: row.errors.length > 0 ? "rgba(185, 28, 28, 0.05)" : undefined }}>
                            <td className="row-num">{row.rowNumber}</td>
                            <td style={{ fontWeight: 600 }}>{row.organizationName || <span style={{ color: "var(--iic-danger)", fontSize: "11px", fontWeight: "bold" }}>[MISSING]</span>}</td>
                            <td>{getRowStatus(row)}</td>
                            <td>{row.isDuplicate || row.errors.length > 0 ? 0 : row.detailPreviews.length}</td>
                            <td>{getRowIssueSummary(row)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  <div className="pagination-container" style={{ paddingTop: "6px" }}>
                    <div className="pagination-info">
                      Showing <span className="pagination-highlight">{startRecord}-{endRecord}</span> of{" "}
                      <span className="pagination-highlight">{totalPreviewRows}</span> records
                    </div>
                    <div className="pagination-controls">
                      <div className="pagination-size-select">
                        <label htmlFor="local-government-preview-page-size">Rows per page:</label>
                        <select
                          id="local-government-preview-page-size"
                          value={previewPageSize}
                          onChange={(event) => handlePageSizeChange(Number(event.target.value))}
                          className="pagination-select"
                        >
                          {PREVIEW_PAGE_SIZES.map((size) => (
                            <option key={size} value={size}>{size}</option>
                          ))}
                        </select>
                      </div>
                      <div className="pagination-buttons">
                        <button
                          className="pagination-btn"
                          type="button"
                          aria-label="Previous page"
                          disabled={previewPage === 1}
                          onClick={() => setPreviewPage((page) => Math.max(1, page - 1))}
                        >
                          Prev
                        </button>
                        <button className="pagination-btn active" type="button" aria-label={`Page ${previewPage}`}>
                          {previewPage}
                        </button>
                        <button
                          className="pagination-btn"
                          type="button"
                          aria-label="Next page"
                          disabled={previewPage >= totalPreviewPages}
                          onClick={() => setPreviewPage((page) => Math.min(totalPreviewPages, page + 1))}
                        >
                          Next
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}

          {result && (
            <div className="tech-alert success">
              <Check size={18} />
              <div>
                <strong>นำเข้าสำเร็จ</strong>
                <div style={{ fontSize: "12px", marginTop: "4px" }}>
                  เพิ่มรายชื่อ {result.insertedCustomers} รายการ, เพิ่มผู้ติดต่อ {result.insertedDetails} รายการ, ข้อมูลซ้ำ {result.skippedDuplicates} รายการ, ข้อมูลผิดพลาด {result.errorRows} รายการ
                </div>
              </div>
            </div>
          )}
        </div>

        <footer className="modal-footer" style={{ display: "flex", justifyContent: "flex-end", gap: "10px", padding: "16px 24px", borderTop: "1px solid var(--iic-border)" }}>
          <button className="ghost-button" type="button" onClick={handleClose} disabled={isPreviewing || isConfirming}>
            Cancel
          </button>
          {!preview ? (
            <button
              className="primary-button"
              type="button"
              onClick={handlePreview}
              disabled={!file || isPreviewing}
            >
              {isPreviewing ? "Running System Diagnostics..." : "Analyze & Preview"}
            </button>
          ) : (
            <button
              className="primary-button"
              type="button"
              onClick={handleCommit}
              disabled={!canCommit || isConfirming}
            >
              {isConfirming ? "Integrating Database..." : "Commit Import"}
            </button>
          )}
        </footer>
      </div>
    </div>
  );
};
