import React, { useRef, useState } from "react";
import { AlertCircle, Check, Download, FileSpreadsheet, Upload, X } from "lucide-react";
import { apiService } from "../domain/apiService";
import type {
  LocalGovernmentImportConfirmResult,
  LocalGovernmentImportIssue,
  LocalGovernmentImportPreviewSummary
} from "../domain/types";

interface LocalGovernmentImportModalProps {
  isOpen: boolean;
  onClose: () => void;
  onImportSuccess: () => void;
  showToast: (msg: string, type: "success" | "error" | "info") => void;
}

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
  return (
    <div className="tech-readout" style={{ maxHeight: "140px", overflowY: "auto" }}>
      <strong style={{ color: "#cbd5e1", fontSize: "12px" }}>{title}</strong>
      <ul style={{ margin: "8px 0 0", paddingLeft: "18px", color: "#cbd5e1", fontSize: "12px" }}>
        {issues.slice(0, 12).map((issue, index) => (
          <li key={`${issue.rowNumber ?? "global"}-${issue.field}-${index}`}>
            {issue.rowNumber ? `แถว ${issue.rowNumber}: ` : ""}
            {issue.field}: {issue.message}
          </li>
        ))}
      </ul>
    </div>
  );
};

export const LocalGovernmentImportModal: React.FC<LocalGovernmentImportModalProps> = ({
  isOpen,
  onClose,
  onImportSuccess,
  showToast
}) => {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<LocalGovernmentImportPreviewSummary | null>(null);
  const [result, setResult] = useState<LocalGovernmentImportConfirmResult | null>(null);
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isConfirming, setIsConfirming] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  if (!isOpen) return null;

  const canConfirm = Boolean(file && preview && preview.estimatedCustomersToInsert > 0 && !result);

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
    setPreview(null);
    setResult(null);
  };

  const handlePreview = async () => {
    if (!file) return;
    try {
      setIsPreviewing(true);
      setResult(null);
      const response = await apiService.previewLocalGovernmentImport(file);
      setPreview(response);
      if (response.estimatedCustomersToInsert > 0) {
        showToast("ตรวจสอบไฟล์สำเร็จ", "success");
      } else if (response.duplicateRows > 0 || response.errorRows > 0) {
        showToast("ไม่มีแถวที่พร้อมนำเข้า", "info");
      }
    } catch (err: any) {
      showToast(err.message || "ตรวจสอบไฟล์ไม่สำเร็จ", "error");
    } finally {
      setIsPreviewing(false);
    }
  };

  const handleConfirm = async () => {
    if (!file || !canConfirm) return;
    try {
      setIsConfirming(true);
      const response = await apiService.confirmLocalGovernmentImport(file);
      setResult(response);
      showToast("นำเข้าสำเร็จ", "success");
      onImportSuccess();
    } catch (err: any) {
      showToast(err.message || "นำเข้าไม่สำเร็จ", "error");
    } finally {
      setIsConfirming(false);
    }
  };

  const handleDownloadTemplate = async () => {
    try {
      await apiService.downloadTemplate("local-government", "csv");
    } catch (err: any) {
      showToast(err.message || "ดาวน์โหลด Template ไม่สำเร็จ", "error");
    }
  };

  const handleClose = () => {
    setFile(null);
    setPreview(null);
    setResult(null);
    onClose();
  };

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div
        className="modal-content-box"
        role="dialog"
        aria-modal="true"
        aria-labelledby="local-government-import-title"
        onClick={(event) => event.stopPropagation()}
        style={{ maxWidth: "780px", width: "95%", maxHeight: "90vh", display: "flex", flexDirection: "column" }}
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

        <div className="modal-body" style={{ display: "flex", flexDirection: "column", gap: "16px", padding: "22px", overflowY: "auto" }}>
          <div style={{ display: "flex", gap: "10px", flexWrap: "wrap", alignItems: "center", justifyContent: "space-between" }}>
            <button className="secondary-button" type="button" onClick={handleDownloadTemplate}>
              <Download size={15} />
              ดาวน์โหลด Template เทศบาล/อปท.
            </button>
            <span style={{ color: "var(--text-muted)", fontSize: "12px" }}>รองรับไฟล์ CSV/XLSX จาก Google Sheet</span>
          </div>

          <label className="tech-dropzone" style={{ cursor: "pointer", minHeight: "120px" }}>
            <input
              ref={fileInputRef}
              aria-label="เลือกไฟล์เทศบาล/อปท."
              type="file"
              accept=".csv,.xlsx"
              onChange={(event) => handleFileChange(event.target.files?.[0] || null)}
              style={{ display: "none" }}
            />
            <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "10px" }}>
              <FileSpreadsheet size={28} color="var(--iic-blue)" />
              <strong style={{ color: "var(--iic-navy)" }}>{file ? file.name : "เลือกไฟล์เทศบาล/อปท."}</strong>
              <span style={{ color: "var(--text-muted)", fontSize: "12px" }}>คลิกเพื่อเลือกไฟล์ .csv หรือ .xlsx</span>
            </div>
          </label>

          {preview && (
            <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
              <div className="metrics-grid" style={{ gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))" }}>
                <SummaryItem label="แถวทั้งหมด" value={preview.totalRows} />
                <SummaryItem label="แถวที่จะเพิ่ม" value={preview.estimatedCustomersToInsert} />
                <SummaryItem label="ข้อมูลซ้ำ" value={preview.duplicateRows} />
                <SummaryItem label="ข้อมูลผิดพลาด" value={preview.errorRows} />
                <SummaryItem label="รายชื่อที่จะเพิ่ม" value={preview.estimatedCustomersToInsert} />
                <SummaryItem label="ผู้ติดต่อที่จะเพิ่ม" value={preview.estimatedDetailsToInsert} />
              </div>

              {preview.estimatedCustomersToInsert === 0 && (
                <div className="tech-alert error">
                  <AlertCircle size={18} />
                  <span>ไม่มีแถวที่พร้อมนำเข้า กรุณาตรวจสอบข้อมูลซ้ำหรือข้อผิดพลาด</span>
                </div>
              )}

              {preview.warnings.length > 0 && (
                <div className="tech-alert success">
                  <AlertCircle size={18} />
                  <span>พบคำเตือน {preview.warnings.length} รายการ แต่ยังสามารถนำเข้าแถวที่ถูกต้องได้</span>
                </div>
              )}

              <IssueList title="คำเตือน" issues={preview.warnings} />
              <IssueList title="ข้อผิดพลาด" issues={preview.errors} />

              {preview.rows.length > 0 && (
                <div className="tech-preview-container">
                  <table className="tech-table">
                    <thead>
                      <tr>
                        <th>แถว</th>
                        <th>ชื่อหน่วยงาน</th>
                        <th>สถานะ</th>
                        <th>ผู้ติดต่อที่จะเพิ่ม</th>
                      </tr>
                    </thead>
                    <tbody>
                      {preview.rows.slice(0, 8).map((row) => (
                        <tr key={row.rowNumber}>
                          <td className="row-num">{row.rowNumber}</td>
                          <td>{row.organizationName || "-"}</td>
                          <td>{row.errors.length > 0 ? "ข้อมูลผิดพลาด" : row.isDuplicate ? "ข้อมูลซ้ำ" : "พร้อมเพิ่ม"}</td>
                          <td>{row.isDuplicate || row.errors.length > 0 ? 0 : row.detailPreviews.length}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
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

        <footer className="modal-footer" style={{ display: "flex", justifyContent: "flex-end", gap: "10px", padding: "16px 22px", borderTop: "1px solid var(--iic-border)" }}>
          <button className="ghost-button" type="button" onClick={handleClose} disabled={isPreviewing || isConfirming}>
            ยกเลิก
          </button>
          <button className="secondary-button" type="button" onClick={handlePreview} disabled={!file || isPreviewing || isConfirming}>
            {isPreviewing ? "กำลังตรวจสอบ..." : "ตรวจสอบไฟล์"}
          </button>
          <button className="primary-button" type="button" onClick={handleConfirm} disabled={!canConfirm || isConfirming}>
            {isConfirming ? "กำลังนำเข้า..." : "ยืนยันนำเข้า"}
          </button>
        </footer>
      </div>
    </div>
  );
};
