import React, { useState, useRef, useEffect } from "react";
import { Upload, FileSpreadsheet, X, Eye, Play, AlertCircle, Database, Sparkles, Check, AlertTriangle, ShieldAlert } from "lucide-react";
import { apiService } from "../domain/apiService";

interface ImportCustomersViewProps {
  showToast: (msg: string, type: "success" | "error" | "info") => void;
}

interface PreviewData {
  fileId: string;
  columns: string[];
  sampleRows: Record<string, string>[];
  totalRows: number;
}

interface MappingField {
  value: string;
  label: string;
}

const TARGET_FIELDS: MappingField[] = [
  { value: "name", label: "Company Name (name)" },
  { value: "address", label: "Address (address)" },
  { value: "phone", label: "Phone (phone)" },
  { value: "capital", label: "Capital (capital)" },
  { value: "business_type", label: "Business Type (business_type)" },
  { value: "contact_name", label: "Contact Name (contact_name)" },
  { value: "contact_email", label: "Contact Email (contact_email)" },
  { value: "contact_tel", label: "Contact Tel (contact_tel)" },
  { value: "contact_position", label: "Contact Position (contact_position)" },
  { value: "unstructured_company_info", label: "Unstructured Company Info (unstructured_company_info)" }
];

interface ExtractedData {
  name: string;
  address: string;
  phone: string;
  capital: string;
  business_type: string;
  contact_name: string;
  contact_email: string;
  contact_tel: string;
  contact_position: string;
  confidence: number;
}

interface ValidationErrorItem {
  field: string;
  message: string;
  severity: "error" | "warning";
}

interface DuplicateInfo {
  status: "duplicate" | "warning" | "unique";
  score: number;
  reason: string;
  matchedCustomerName?: string;
  matchedCustomerCode?: string;
  matchedCustomerId?: number;
}

interface ValidatedRow {
  name?: string;
  address?: string;
  phone?: string;
  capital?: number;
  businessType?: string;
  contactName?: string;
  contactEmail?: string;
  contactTel?: string;
  contactPosition?: string;
  status: "valid" | "warning" | "error";
  issues: ValidationErrorItem[];
  _source?: string;
  duplicate?: DuplicateInfo;
  importAction?: "new" | "skip" | "update";
  confidence?: number;
  extractionMetadata?: Record<string, { source: string; confidence: number }>;
  suggestedStatus?: string;
  suggestedPriority?: string;
  suggestedFollowUpDays?: number;
  suggestedCallAngle?: string;
  suggested_call_angle?: string;
  suggested_priority?: string;
  suggested_follow_up_days?: number;
}

interface ValidationSummary {
  total: number;
  valid: number;
  warning: number;
  error: number;
  duplicateCount: number;
  duplicateWarningCount: number;
  uniqueCount: number;
}

export const ImportCustomersView: React.FC<ImportCustomersViewProps> = ({ showToast }) => {
  // File Upload State
  const [file, setFile] = useState<File | null>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [previewData, setPreviewData] = useState<PreviewData | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Mapping State
  const [mappings, setMappings] = useState<Record<string, string>>({});
  const [mappingConfidence, setMappingConfidence] = useState<number | null>(null);
  const [suggestedMappings, setSuggestedMappings] = useState<any[]>([]);
  const [isMappingLoading, setIsMappingLoading] = useState(false);

  // Policy & Tab states
  const [policy, setPolicy] = useState<string>("Safe");
  const [activeTab, setActiveTab] = useState<string>("autoReady");

  // AI Unstructured Extraction State
  const [unstructuredText, setUnstructuredText] = useState("");
  const [isExtracting, setIsExtracting] = useState(false);
  const [extractedData, setExtractedData] = useState<ExtractedData | null>(null);
  const [manualExtractedRows, setManualExtractedRows] = useState<Record<string, string>[]>([]);

  // Validation State
  const [validatedRows, setValidatedRows] = useState<ValidatedRow[]>([]);
  const [validationSummary, setValidationSummary] = useState<ValidationSummary | null>(null);
  const [isValidating, setIsValidating] = useState(false);
  const [hasValidated, setHasValidated] = useState(false);
  // Paginated Preview & Validation States
  const [fileId, setFileId] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize] = useState(50);

  // Progress and cancellation states
  const [isCommitingStream, setIsCommitingStream] = useState(false);
  const [commitProgress, setCommitProgress] = useState<{ currentProgress: number; totalRows: number; status: string; imported: number; updated: number; skipped: number; errorMessage?: string } | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  // AI Explanation states
  const [explanationText, setExplanationText] = useState<string | null>(null);
  const [isExplaining, setIsExplaining] = useState(false);

  // Assignee states
  const [users, setUsers] = useState<any[]>([]);
  const [selectedSaleId, setSelectedSaleId] = useState<number | null>(null);
  const [selectedTelesaleId, setSelectedTelesaleId] = useState<number | null>(null);

  // Generate raw mapped rows from uploaded file + manual extracts
  const rawDraftRows = React.useMemo<any[]>(() => {
    const fileRows = !previewData ? [] : previewData.sampleRows.map(row => {
      const draft: Record<string, string> = {};
      Object.entries(mappings).forEach(([colName, targetField]) => {
        if (targetField && row[colName]) {
          draft[targetField] = row[colName];
        }
      });
      draft._source = "Uploaded File";
      return draft;
    });

    const manualRows = manualExtractedRows.map(row => ({
      ...row,
      _source: "AI Extracted Data"
    }));

    return [...fileRows, ...manualRows];
  }, [previewData, mappings, manualExtractedRows]);

  // Categorize rows dynamically based on policy
  const categorizedRows = React.useMemo(() => {
    if (!hasValidated || validatedRows.length === 0) {
      return { autoReady: [], needsReview: [], duplicates: [], errors: [] };
    }

    const autoReady: any[] = [];
    const needsReview: any[] = [];
    const duplicates: any[] = [];
    const errors: any[] = [];

    const currentPolicy = policy || "Safe";
    const mappingConf = mappingConfidence ?? 1.0;

    validatedRows.forEach((row) => {
      if (row.status === "error") {
        errors.push(row);
        return;
      }

      if (currentPolicy === "Strict") {
        if (row.duplicate && row.duplicate.status !== "unique") {
          duplicates.push(row);
        } else {
          needsReview.push(row);
        }
      } else if (currentPolicy === "Fast") {
        if (row.duplicate && row.duplicate.status === "warning") {
          duplicates.push(row);
        } else {
          autoReady.push(row);
        }
      } else {
        // Safe Mode
        const isUnique = !row.duplicate || row.duplicate.status === "unique";
        const rowConfidence = row.confidence ?? 1.0;
        if (row.status === "valid" && isUnique && rowConfidence >= 0.90 && mappingConf >= 0.90) {
          autoReady.push(row);
        } else if (row.duplicate && row.duplicate.status !== "unique") {
          duplicates.push(row);
        } else {
          needsReview.push(row);
        }
      }
    });

    return { autoReady, needsReview, duplicates, errors };
  }, [validatedRows, policy, mappingConfidence, hasValidated]);

  // Derived summary counts
  const activeSummary = React.useMemo(() => {
    return {
      total: validatedRows.length,
      autoReadyCount: categorizedRows.autoReady.length,
      needsReviewCount: categorizedRows.needsReview.length,
      duplicateCount: categorizedRows.duplicates.length,
      errorCount: categorizedRows.errors.length
    };
  }, [validatedRows, categorizedRows]);

  // Rows to render in table grid
  const rowsToRender = React.useMemo(() => {
    if (!hasValidated) {
      return rawDraftRows.map((row, idx) => ({ ...row, _originalIdx: idx }));
    }
    
    let targetList = validatedRows;
    if (activeTab === "autoReady") targetList = categorizedRows.autoReady;
    else if (activeTab === "needsReview") targetList = categorizedRows.needsReview;
    else if (activeTab === "duplicates") targetList = categorizedRows.duplicates;
    else if (activeTab === "errors") targetList = categorizedRows.errors;

    return targetList.map(row => {
      const originalIdx = validatedRows.findIndex(vr => 
        vr.name === row.name && 
        vr.phone === row.phone && 
        vr.capital === row.capital
      );
      return { ...row, _originalIdx: originalIdx >= 0 ? originalIdx : 0 };
    });
  }, [hasValidated, validatedRows, rawDraftRows, activeTab, categorizedRows]);

  // Bulk confirm auto ready
  const handleBulkConfirmAutoReady = () => {
    setValidatedRows(prev => {
      return prev.map(row => {
        const isAutoReady = categorizedRows.autoReady.some(ar => 
          ar.name === row.name && 
          ar.phone === row.phone && 
          ar.capital === row.capital
        );
        return {
          ...row,
          importAction: isAutoReady ? "new" : "skip"
        };
      });
    });
    showToast("All auto-ready rows confirmed. Non-ready rows set to 'Skip'. Ready to commit.", "success");
  };

  // Fetch active users on mount for assignee selectors
  useEffect(() => {
    const fetchUsers = async () => {
      try {
        const uList = await apiService.getUsers();
        setUsers(uList);
      } catch (err) {
        console.error("Failed to fetch users for assignees", err);
      }
    };
    fetchUsers();
  }, []);

  const saleUsers = React.useMemo(() => {
    return users.filter(u => u.roles?.toLowerCase() === "sale");
  }, [users]);

  const telesaleUsers = React.useMemo(() => {
    return users.filter(u => u.roles?.toLowerCase() === "tele sale" || u.roles?.toLowerCase() === "telesale");
  }, [users]);

  // Helper to format file size
  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return "0 Bytes";
    const k = 1024;
    const sizes = ["Bytes", "KB", "MB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  };

  const handleFileChange = (selectedFile: File | null) => {
    if (!selectedFile) return;

    const ext = selectedFile.name.split(".").pop()?.toLowerCase();
    if (ext !== "csv" && ext !== "xlsx") {
      showToast("Unsupported file type. Please select a .csv or .xlsx file.", "error");
      return;
    }

    if (selectedFile.size > 5 * 1024 * 1024) {
      showToast("File is too large. Maximum size is 5MB.", "error");
      return;
    }

    setFile(selectedFile);
    setPreviewData(null);
    setMappings({});
    setSuggestedMappings([]);
    setMappingConfidence(null);
    setValidatedRows([]);
    setValidationSummary(null);
    setHasValidated(false);
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
    showToast(`Selected file: ${selectedFile.name}`, "info");
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

  const handleClearFile = () => {
    setFile(null);
    setPreviewData(null);
    setMappings({});
    setSuggestedMappings([]);
    setMappingConfidence(null);
    setValidatedRows([]);
    setValidationSummary(null);
    setHasValidated(false);
    setSelectedSaleId(null);
    setSelectedTelesaleId(null);
    setFileId(null);
    setCurrentPage(1);
    setCommitProgress(null);
    setIsCommitingStream(false);
    setExplanationText(null);
    setIsExplaining(false);
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  // Preview flow
  const handlePreview = async () => {
    if (!file) return;

    try {
      setIsLoading(true);
      const data = await apiService.previewCustomerImport(file);
      setPreviewData(data);
      setFileId(data.fileId);
      setCurrentPage(1);
      showToast("File parsed successfully. Generating suggested mappings...", "success");
      
      // Auto suggest mapping fields
      setIsMappingLoading(true);
      try {
        const suggestion = await apiService.suggestMappings(data.columns);
        const mappedResult: Record<string, string> = {};
        
        data.columns.forEach(col => {
          mappedResult[col] = "";
        });

        suggestion.mappings.forEach(m => {
          if (data.columns.includes(m.column)) {
            mappedResult[m.column] = m.targetField;
          }
        });
        
        setMappings(mappedResult);
        setSuggestedMappings(suggestion.mappings || []);
        setMappingConfidence(suggestion.confidence);
        showToast("Column mapping suggestions applied.", "success");
      } catch (errSuggest) {
        showToast("Could not fetch mapping suggestions automatically. Please map manually.", "info");
        const mappedResult: Record<string, string> = {};
        data.columns.forEach(col => {
          mappedResult[col] = "";
        });
        setMappings(mappedResult);
      } finally {
        setIsMappingLoading(false);
        setHasValidated(false);
        setValidatedRows([]);
        setValidationSummary(null);
      }
    } catch (err: any) {
      showToast(err.message || "Failed to load file preview.", "error");
    } finally {
      setIsLoading(false);
    }
  };

  const handleMappingChange = (column: string, targetField: string) => {
    setMappings(prev => ({
      ...prev,
      [column]: targetField
    }));
    setHasValidated(false);
    setValidatedRows([]);
    setValidationSummary(null);
  };

  // AI Unstructured Extraction Call
  const handleExtractUnstructured = async () => {
    if (!unstructuredText.trim()) {
      showToast("Please enter unstructured text to extract.", "error");
      return;
    }

    try {
      setIsExtracting(true);
      const result = await apiService.extractUnstructuredData(unstructuredText);
      setExtractedData({
        name: result.name ?? "",
        address: result.address ?? "",
        phone: result.phone ?? "",
        capital: result.capital?.toString() ?? "",
        business_type: result.businessType ?? "",
        contact_name: result.contactName ?? "",
        contact_email: result.contactEmail ?? "",
        contact_tel: result.contactTel ?? "",
        contact_position: result.contactPosition ?? "",
        confidence: result.confidence
      });
      showToast("Data extracted successfully.", "success");
    } catch (err: any) {
      showToast(err.message || "Failed to extract unstructured text.", "error");
    } finally {
      setIsExtracting(false);
    }
  };

  const handleAddExtractedRow = () => {
    if (!extractedData) return;
    
    const newRow: Record<string, string> = {
      name: extractedData.name,
      address: extractedData.address,
      phone: extractedData.phone,
      capital: extractedData.capital,
      business_type: extractedData.business_type,
      contact_name: extractedData.contact_name,
      contact_email: extractedData.contact_email,
      contact_tel: extractedData.contact_tel,
      contact_position: extractedData.contact_position,
      _source: "AI Extracted Data"
    };

    setManualExtractedRows(prev => [...prev, newRow]);
    setHasValidated(false);
    setValidatedRows([]);
    setValidationSummary(null);
    showToast("Extracted customer added to draft workspace.", "success");
    
    setUnstructuredText("");
    setExtractedData(null);
  };

  const handleExtractedFieldChange = (field: keyof ExtractedData, value: string) => {
    if (!extractedData) return;
    setExtractedData(prev => prev ? { ...prev, [field]: value } : null);
  };

  // Run Backend Validation & Normalization
  const handleValidate = async () => {
    if (rawDraftRows.length === 0) {
      showToast("No data to validate. Please upload a file or extract data first.", "error");
      return;
    }

    try {
      setIsValidating(true);
      
      let result;
      if (fileId) {
        // Run paginated validation directly using server staged file
        result = await apiService.validateImportFilePage(fileId, currentPage, pageSize, mappings, policy, mappingConfidence ?? 1.0);
      } else {
        // Fallback for purely manually extracted AI drafts
        const rowsToValidate = rawDraftRows.map(({ _source, ...rest }) => rest);
        result = await apiService.validateImportRows(rowsToValidate);
      }
      
      // Inject source metadata back to validated rows for rendering and set default actions based on duplicate scan
      const validatedWithSource = result.rows.map((row, index) => {
        let defaultAction: "new" | "skip" | "update" = "new";
        if (row.duplicate?.status === "duplicate") {
          defaultAction = "skip";
        } else if (row.duplicate?.status === "warning") {
          defaultAction = "update";
        }
        return {
          ...row,
          _source: rawDraftRows[index]?.["_source"] || "AI Extracted Data",
          importAction: defaultAction
        };
      });

      setValidatedRows(validatedWithSource);
      setValidationSummary(result.summary);
      setHasValidated(true);
      showToast("Validation, normalization, and duplicate scan completed.", "success");
    } catch (err: any) {
      showToast(err.message || "Failed to validate customer rows.", "error");
    } finally {
      setIsValidating(false);
    }
  };

  const handlePageChange = async (newPage: number) => {
    if (!fileId) return;
    try {
      setIsLoading(true);
      setCurrentPage(newPage);
      
      if (hasValidated) {
        // Validate the new page
        const result = await apiService.validateImportFilePage(fileId, newPage, pageSize, mappings, policy, mappingConfidence ?? 1.0);
        const validatedWithSource = result.rows.map((row, index) => {
          let defaultAction: "new" | "skip" | "update" = "new";
          if (row.duplicate?.status === "duplicate") {
            defaultAction = "skip";
          } else if (row.duplicate?.status === "warning") {
            defaultAction = "update";
          }
          return {
            ...row,
            _source: "Uploaded File",
            importAction: defaultAction
          };
        });
        setValidatedRows(validatedWithSource);
        setValidationSummary(result.summary);
      } else {
        // Retrieve next raw preview page
        const pageData = await apiService.previewCustomerImportPage(fileId, newPage, pageSize);
        setPreviewData(prev => prev ? { ...prev, sampleRows: pageData } : null);
      }
    } catch (err: any) {
      showToast(err.message || "Failed to load page.", "error");
    } finally {
      setIsLoading(false);
    }
  };

  const handleAskAiExplanation = async (
    issueType: string, 
    fieldName: string, 
    fieldValue: string, 
    issueDetails: string, 
    matchedCustomerDetails?: string
  ) => {
    try {
      setIsExplaining(true);
      setExplanationText(null);
      const res = await apiService.explainIssue(issueType, fieldName, fieldValue, issueDetails, matchedCustomerDetails);
      setExplanationText(res.explanation);
    } catch (err: any) {
      showToast(err.message || "Failed to retrieve AI explanation.", "error");
    } finally {
      setIsExplaining(false);
    }
  };

  const handleActionChange = (index: number, action: "new" | "skip" | "update") => {
    setValidatedRows(prev => {
      const next = [...prev];
      if (next[index]) {
        next[index] = { ...next[index], importAction: action };
      }
      return next;
    });
  };

  const handleCancelCommit = () => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      setIsCommitingStream(false);
      setCommitProgress(null);
      showToast("Cancellation requested. Rolling back transaction...", "info");
    }
  };

  // Commit and Save to Database
  const handleImportToDb = async () => {
    if (validatedRows.length === 0) return;

    if (fileId) {
      // Production Ready staged file streaming commit
      try {
        setIsCommitingStream(true);
        setCommitProgress({
          currentProgress: 0,
          totalRows: previewData?.totalRows || validatedRows.length,
          status: "Processing",
          imported: 0,
          updated: 0,
          skipped: 0
        });

        abortControllerRef.current = new AbortController();

        // Calculate overrides maps
        const rowOverrides: Record<number, string> = {};
        validatedRows.forEach((row, idx) => {
          if (row.importAction) {
            const absoluteIdx = (currentPage - 1) * pageSize + idx;
            rowOverrides[absoluteIdx] = row.importAction;
          }
        });

        await apiService.commitImportRowsStream({
          fileId,
          mappings,
          saleId: selectedSaleId,
          telesaleId: selectedTelesaleId,
          fileName: file?.name || "import.xlsx",
          rowOverrides
        }, (progress) => {
          setCommitProgress(progress);
          if (progress.status === "Completed") {
            showToast(`Import completed successfully: ${progress.imported} imported, ${progress.updated} updated, ${progress.skipped} skipped.`, "success");
            setIsCommitingStream(false);
            handleClearFile();
            setManualExtractedRows([]);
          } else if (progress.status === "Failed") {
            showToast(progress.errorMessage || "Failed to commit import.", "error");
            setIsCommitingStream(false);
          }
        }, abortControllerRef.current.signal);

      } catch (err: any) {
        if (err.name === "AbortError") {
          showToast("Import transaction rolled back.", "info");
        } else {
          showToast(err.message || "Failed to commit imported customers.", "error");
        }
        setIsCommitingStream(false);
      }
    } else {
      // Fallback for manually extracted AI drafts (no fileId)
      try {
        setIsLoading(true);
        const payload = validatedRows.map(row => ({
          name: row.name,
          address: row.address,
          phone: row.phone,
          capital: row.capital,
          businessType: row.businessType,
          contactName: row.contactName,
          contactEmail: row.contactEmail,
          contactTel: row.contactTel,
          contactPosition: row.contactPosition,
          importAction: row.importAction || "new",
          matchedCustomerId: row.duplicate?.matchedCustomerId
        }));

        const res = await apiService.commitImportRows({
          rows: payload,
          saleId: selectedSaleId,
          telesaleId: selectedTelesaleId,
          fileName: "Unstructured Text Extract"
        });
        showToast(`Import completed successfully: ${res.imported} imported, ${res.updated} updated, ${res.skipped} skipped.`, "success");
        handleClearFile();
        setManualExtractedRows([]);
      } catch (err: any) {
        showToast(err.message || "Failed to commit imported customers.", "error");
      } finally {
        setIsLoading(false);
      }
    }
  };

  return (
    <div className="workspace-view">
      <header className="topbar">
        <div>
          <p>Admin / Import Data</p>
          <h1>Import Data</h1>
        </div>
      </header>

      <main className="content animate-fade-in" style={{ display: "flex", flexDirection: "column", gap: "24px" }}>
        <section className="import-step-grid" aria-label="Import workflow steps">
          {[
            ["1", "Upload", "CSV or XLSX source"],
            ["2", "Preview", "Map detected columns"],
            ["3", "Validation", "Scan duplicates and errors"],
            ["4", "Confirm", "Commit approved rows"]
          ].map(([index, title, detail]) => (
            <div className="import-step" key={index}>
              <span className="import-step-index">{index}</span>
              <div>
                <strong>{title}</strong>
                <span>{detail}</span>
              </div>
            </div>
          ))}
        </section>
        
        {/* Step 1: Upload File & AI Extraction Panels */}
        <div className="import-panel-grid">
          
          {/* File Upload Panel */}
          <div className="panel panel-padded" style={{ display: "flex", flexDirection: "column" }}>
            <div className="panel-header" style={{ marginBottom: "20px" }}>
              <h2>Upload Lead Document</h2>
              <p>Upload a customer .csv or .xlsx file to preview, map, and process.</p>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "16px", flexGrow: 1 }}>
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
                  gap: "10px"
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
                <div>
                  <strong style={{ display: "block", fontSize: "14px", marginBottom: "2px" }}>
                    Drag & Drop file or <span style={{ color: "var(--primary-color)", textDecoration: "underline" }}>browse</span>
                  </strong>
                  <span style={{ fontSize: "11px", color: "var(--text-muted)" }}>
                    CSV & XLSX (Max 5MB)
                  </span>
                </div>
              </div>

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
                    <div style={{ maxWidth: "240px", overflow: "hidden", textOverflow: "ellipsis" }}>
                      <strong style={{ display: "block", fontSize: "13px", whiteSpace: "nowrap" }}>{file.name}</strong>
                      <span style={{ fontSize: "11px", color: "var(--text-muted)" }}>
                        {formatFileSize(file.size)}
                      </span>
                    </div>
                  </div>
                  <button onClick={handleClearFile} type="button" className="ghost-button" style={{ padding: "4px" }}>
                    <X size={16} />
                  </button>
                </div>
              )}

              <div style={{ marginTop: "auto", display: "flex", flexDirection: "column", gap: "8px" }}>
                <button
                  className="ghost-button"
                  onClick={async () => {
                    try {
                      await apiService.downloadTemplate("manage");
                    } catch (err: any) {
                      showToast(err.message || "Failed to download template.", "error");
                    }
                  }}
                  type="button"
                  style={{ width: "100%", display: "flex", alignItems: "center", justifyContent: "center", gap: "8px", border: "1px solid var(--border-color)" }}
                >
                  <FileSpreadsheet size={16} />
                  Download Template
                </button>
                <button
                  className="primary-button"
                  onClick={handlePreview}
                  disabled={!file || isLoading}
                  type="button"
                  style={{ width: "100%", display: "flex", alignItems: "center", justifyContent: "center", gap: "8px" }}
                >
                  {isLoading ? (
                    <span className="spinner" style={{ width: "14px", height: "14px" }}></span>
                  ) : (
                    <Eye size={16} />
                  )}
                  {isLoading ? "Reading File..." : "Preview & Map Columns"}
                </button>
              </div>
            </div>
          </div>

          {/* AI Unstructured Text Extraction Panel */}
          <div className="panel panel-padded" style={{ display: "flex", flexDirection: "column" }}>
            <div className="panel-header" style={{ marginBottom: "16px" }}>
              <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                <Sparkles size={18} style={{ color: "var(--primary-color)" }} />
                <h2>AI Extraction Console</h2>
              </div>
              <p>Paste email signatures, registration text, or raw text blocks to extract structured customer drafts.</p>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "16px", flexGrow: 1 }}>
              <textarea
                placeholder="e.g. Acme Corporation, 12 Silom Rd Bangkok, Tel 02-1234567, email contact@acme.com, PIC Mr. John Doe (Manager)..."
                value={unstructuredText}
                onChange={(e) => setUnstructuredText(e.target.value)}
                rows={4}
                style={{
                  width: "100%",
                  padding: "10px",
                  borderRadius: "var(--border-radius)",
                  border: "1px solid var(--border-color)",
                  fontFamily: "var(--font-family)",
                  fontSize: "13px",
                  resize: "none",
                  background: "var(--bg-active)"
                }}
              />

              <button
                className="primary-button"
                onClick={handleExtractUnstructured}
                disabled={!unstructuredText.trim() || isExtracting}
                type="button"
                style={{
                  background: "var(--primary-color)",
                  border: "none",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  gap: "8px"
                }}
              >
                {isExtracting ? (
                  <span className="spinner" style={{ width: "14px", height: "14px" }}></span>
                ) : (
                  <Sparkles size={16} />
                )}
                {isExtracting ? "Extracting Data..." : "Extract with Gemini AI"}
              </button>
            </div>
          </div>
        </div>

        {/* AI Extraction Results (Pre-add edits) */}
        {extractedData && (
          <div className="panel panel-padded animate-fade-in">
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "16px", flexWrap: "wrap", gap: "8px" }}>
              <div className="panel-header">
                <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                  <Check size={18} style={{ color: "var(--success-color)" }} />
                  <h2>AI Extracted Draft Results</h2>
                </div>
                <p>Review and modify fields extracted from your unstructured text before adding them as a draft row.</p>
              </div>
              
              <span 
                className="status-badge" 
                style={{ 
                  fontSize: "12px", 
                  padding: "4px 10px",
                  background: extractedData.confidence >= 0.7 ? "rgba(34, 197, 94, 0.15)" : "rgba(249, 115, 22, 0.15)",
                  color: extractedData.confidence >= 0.7 ? "#16a34a" : "#ea580c"
                }}
              >
                AI Confidence: {Math.round(extractedData.confidence * 100)}%
              </span>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "16px" }}>
              <div className="form-group">
                <label htmlFor="comp_name">Company Name</label>
                <input
                  id="comp_name"
                  type="text"
                  value={extractedData.name}
                  onChange={(e) => handleExtractedFieldChange("name", e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="addr">Address</label>
                <input
                  id="addr"
                  type="text"
                  value={extractedData.address}
                  onChange={(e) => handleExtractedFieldChange("address", e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="ph">Phone</label>
                <input
                  id="ph"
                  type="text"
                  value={extractedData.phone}
                  onChange={(e) => handleExtractedFieldChange("phone", e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="cap">Registered Capital</label>
                <input
                  id="cap"
                  type="text"
                  value={extractedData.capital}
                  onChange={(e) => handleExtractedFieldChange("capital", e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="biz_type">Business Type</label>
                <input
                  id="biz_type"
                  type="text"
                  value={extractedData.business_type}
                  onChange={(e) => handleExtractedFieldChange("business_type", e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="cont_name">Contact Name</label>
                <input
                  id="cont_name"
                  type="text"
                  value={extractedData.contact_name}
                  onChange={(e) => handleExtractedFieldChange("contact_name", e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="cont_email">Contact Email</label>
                <input
                  id="cont_email"
                  type="email"
                  value={extractedData.contact_email}
                  onChange={(e) => handleExtractedFieldChange("contact_email", e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="cont_tel">Contact Tel</label>
                <input
                  id="cont_tel"
                  type="text"
                  value={extractedData.contact_tel}
                  onChange={(e) => handleExtractedFieldChange("contact_tel", e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="cont_pos">Contact Position</label>
                <input
                  id="cont_pos"
                  type="text"
                  value={extractedData.contact_position}
                  onChange={(e) => handleExtractedFieldChange("contact_position", e.target.value)}
                />
              </div>
            </div>

            <div style={{ display: "flex", gap: "10px", marginTop: "20px", justifyContent: "flex-end" }}>
              <button className="ghost-button" onClick={() => setExtractedData(null)} type="button">
                Discard
              </button>
              <button 
                className="primary-button" 
                onClick={handleAddExtractedRow} 
                type="button"
                style={{ background: "var(--success-color)", border: "none" }}
              >
                Add to Draft Grid
              </button>
            </div>
          </div>
        )}

        {/* Step 2: Column Mapping UI */}
        {previewData && (
          <div className="panel panel-padded animate-fade-in" style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "12px" }}>
              <div className="panel-header">
                <h2>Map Columns to Target Schema</h2>
                <p>Align the headers from the uploaded file to customer registry parameters. Unmapped fields will be skipped.</p>
              </div>
              {mappingConfidence !== null && (
                <span className="status-badge info" style={{ fontSize: "12px", padding: "4px 10px" }}>
                  AI Suggestion Confidence: {Math.round(mappingConfidence * 100)}%
                </span>
              )}
            </div>

            {isMappingLoading ? (
              <div style={{ textAlign: "center", padding: "20px", color: "var(--text-muted)" }}>
                Suggesting column mappings...
              </div>
            ) : (
              <div className="table-wrap" style={{ maxHeight: "300px", overflowY: "auto", border: "1px solid var(--border-color)", borderRadius: "var(--border-radius)" }}>
                <table className="corporate-table" style={{ width: "100%", tableLayout: "fixed" }} aria-label="Columns mapping configuration table">
                  <thead>
                    <tr>
                      <th style={{ width: "35%" }}>Detected File Header</th>
                      <th style={{ width: "35%" }}>Sample Value (Row 1)</th>
                      <th style={{ width: "30%" }}>Map to Database Field</th>
                    </tr>
                  </thead>
                  <tbody>
                    {previewData.columns.map((col) => {
                      const sampleVal = previewData.sampleRows[0]?.[col] || "";
                      const currentField = mappings[col] || "";
                      const suggestedItem = suggestedMappings.find(m => m.column === col);
                      let mappingBadge = null;
                      let selectStyle: React.CSSProperties = {
                        width: "100%",
                        padding: "6px 10px",
                        borderRadius: "4px",
                        border: "1px solid var(--border-color)",
                        background: "var(--bg-active)",
                        fontSize: "13px"
                      };

                      if (suggestedItem) {
                        if (suggestedItem.status === "auto_accepted") {
                          mappingBadge = (
                            <span style={{ fontSize: "10px", color: "#16a34a", background: "rgba(34,197,94,0.1)", padding: "2px 6px", borderRadius: "10px", marginLeft: "8px", fontWeight: "600" }}>
                              ✓ Auto ({Math.round(suggestedItem.confidence * 100)}%)
                            </span>
                          );
                        } else if (suggestedItem.status === "review_required") {
                          mappingBadge = (
                            <span style={{ fontSize: "10px", color: "#ca8a04", background: "rgba(234,179,8,0.1)", padding: "2px 6px", borderRadius: "10px", marginLeft: "8px", fontWeight: "600" }}>
                              ⚠ Review ({Math.round(suggestedItem.confidence * 100)}%)
                            </span>
                          );
                          selectStyle.borderColor = "#ca8a04";
                          selectStyle.background = "rgba(234,179,8,0.02)";
                        }
                      }

                      return (
                        <tr key={col}>
                          <td style={{ fontWeight: "600" }}>
                            {col}
                            {mappingBadge}
                          </td>
                          <td style={{ color: "var(--text-muted)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                            {sampleVal}
                          </td>
                          <td>
                            <select
                              value={currentField}
                              onChange={(e) => handleMappingChange(col, e.target.value)}
                              style={selectStyle}
                              aria-label={`Map ${col} to target field`}
                            >
                              <option value="">-- Skip / Do Not Map --</option>
                              {TARGET_FIELDS.map((f) => (
                                <option key={f.value} value={f.value}>
                                  {f.label}
                                </option>
                              ))}
                            </select>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* Step 3: Validation Summary Dashboard */}
        {hasValidated && validationSummary && (
          <div className="panel panel-padded animate-fade-in" style={{ background: "var(--bg-active)", border: "1px solid var(--border-color)", display: "flex", flexDirection: "column", gap: "16px" }}>
            <div>
              <div className="panel-header" style={{ marginBottom: "12px" }}>
                <h2>Validation & Duplicate Summary</h2>
                <p>Health and similarity check results on your customer import draft list.</p>
              </div>

              {/* Data Format Validation Row */}
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: "16px" }}>
                <div style={{ padding: "12px", background: "var(--bg-card)", border: "1px solid var(--border-color)", borderRadius: "var(--border-radius)", textAlign: "center" }}>
                  <span style={{ display: "block", fontSize: "20px", fontWeight: "700", color: "var(--text-color)" }}>
                    {validationSummary.total}
                  </span>
                  <span style={{ fontSize: "11px", color: "var(--text-muted)", fontWeight: "500" }}>Total Draft Rows</span>
                </div>
                
                <div style={{ padding: "12px", background: "var(--bg-card)", border: "1px solid rgba(34, 197, 94, 0.4)", borderRadius: "var(--border-radius)", textAlign: "center" }}>
                  <span style={{ display: "block", fontSize: "20px", fontWeight: "700", color: "#16a34a" }}>
                    {validationSummary.valid}
                  </span>
                  <span style={{ fontSize: "11px", color: "#16a34a", fontWeight: "500" }}>Format Valid</span>
                </div>

                <div style={{ padding: "12px", background: "var(--bg-card)", border: "1px solid rgba(249, 115, 22, 0.4)", borderRadius: "var(--border-radius)", textAlign: "center" }}>
                  <span style={{ display: "block", fontSize: "20px", fontWeight: "700", color: "#ea580c" }}>
                    {validationSummary.warning}
                  </span>
                  <span style={{ fontSize: "11px", color: "#ea580c", fontWeight: "500" }}>Format Warnings</span>
                </div>

                <div style={{ padding: "12px", background: "var(--bg-card)", border: "1px solid rgba(239, 68, 68, 0.4)", borderRadius: "var(--border-radius)", textAlign: "center" }}>
                  <span style={{ display: "block", fontSize: "20px", fontWeight: "700", color: "#dc2626" }}>
                    {validationSummary.error}
                  </span>
                  <span style={{ fontSize: "11px", color: "#dc2626", fontWeight: "500" }}>Format Errors</span>
                </div>
              </div>
            </div>

            {/* Duplicate Detection Row */}
            <div>
              <div style={{ fontSize: "12px", fontWeight: "600", color: "var(--text-muted)", marginBottom: "8px", borderBottom: "1px solid var(--border-color)", paddingBottom: "4px" }}>
                DUPLICATE SCAN STATISTICS
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: "16px" }}>
                <div style={{ padding: "12px", background: "var(--bg-card)", border: "1px solid rgba(239, 68, 68, 0.4)", borderRadius: "var(--border-radius)", textAlign: "center" }}>
                  <span style={{ display: "block", fontSize: "20px", fontWeight: "700", color: "#dc2626" }}>
                    {validationSummary.duplicateCount}
                  </span>
                  <span style={{ fontSize: "11px", color: "#dc2626", fontWeight: "500" }}>Duplicates (95%+)</span>
                </div>

                <div style={{ padding: "12px", background: "var(--bg-card)", border: "1px solid rgba(249, 115, 22, 0.4)", borderRadius: "var(--border-radius)", textAlign: "center" }}>
                  <span style={{ display: "block", fontSize: "20px", fontWeight: "700", color: "#ea580c" }}>
                    {validationSummary.duplicateWarningCount}
                  </span>
                  <span style={{ fontSize: "11px", color: "#ea580c", fontWeight: "500" }}>Fuzzy Warnings (85-94%)</span>
                </div>

                <div style={{ padding: "12px", background: "var(--bg-card)", border: "1px solid rgba(34, 197, 94, 0.4)", borderRadius: "var(--border-radius)", textAlign: "center" }}>
                  <span style={{ display: "block", fontSize: "20px", fontWeight: "700", color: "#16a34a" }}>
                    {validationSummary.uniqueCount}
                  </span>
                  <span style={{ fontSize: "11px", color: "#16a34a", fontWeight: "500" }}>Unique Records (&lt;85%)</span>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Step 4: Consolidated Draft Preview Data Grid */}
        {rawDraftRows.length > 0 && (
          <div className="panel panel-padded animate-fade-in" style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "12px" }}>
              <div className="panel-header">
                <h2>Draft Customers Registry Workspace</h2>
                <p>
                  {hasValidated 
                    ? "Displaying normalized data from the validation engine. Check inline error/warning flags below." 
                    : "Final preview of target records mapped from source file and manually entered AI extractions."}
                </p>
              </div>
              <span className="status-badge new" style={{ fontSize: "13px", padding: "6px 12px" }}>
                Draft Row Count: {rawDraftRows.length}
              </span>
            </div>

            {/* Import Policy Settings */}
            <div style={{
              display: "flex",
              flexDirection: "column",
              gap: "8px",
              padding: "16px",
              background: "rgba(59, 130, 246, 0.02)",
              border: "1px solid var(--border-color)",
              borderRadius: "var(--border-radius)",
              marginTop: "8px",
              marginBottom: "8px"
            }}>
              <label style={{ fontSize: "13px", fontWeight: "700", color: "var(--text-color)", display: "flex", alignItems: "center", gap: "8px" }}>
                <ShieldAlert size={15} style={{ color: "var(--primary-color)" }} />
                Import Policy Mode
              </label>
              <p style={{ fontSize: "11px", color: "var(--text-muted)", margin: 0 }}>
                Choose the automation mode. Safest auto-imports unique rows only; Fast mode accepts all valid unique/exact duplicate rows.
              </p>
              <div style={{ display: "flex", gap: "20px", marginTop: "8px", flexWrap: "wrap" }}>
                <label style={{ display: "flex", alignItems: "center", gap: "6px", fontSize: "12px", cursor: "pointer", fontWeight: "600" }}>
                  <input
                    type="radio"
                    name="importPolicy"
                    value="Safe"
                    checked={policy === "Safe"}
                    onChange={(e) => setPolicy(e.target.value)}
                  />
                  Safe Mode
                </label>
                <label style={{ display: "flex", alignItems: "center", gap: "6px", fontSize: "12px", cursor: "pointer", fontWeight: "600" }}>
                  <input
                    type="radio"
                    name="importPolicy"
                    value="Fast"
                    checked={policy === "Fast"}
                    onChange={(e) => setPolicy(e.target.value)}
                  />
                  Fast Mode
                </label>
                <label style={{ display: "flex", alignItems: "center", gap: "6px", fontSize: "12px", cursor: "pointer", fontWeight: "600" }}>
                  <input
                    type="radio"
                    name="importPolicy"
                    value="Strict"
                    checked={policy === "Strict"}
                    onChange={(e) => setPolicy(e.target.value)}
                  />
                  Strict Mode
                </label>
              </div>
            </div>

            {/* Assignee selectors */}
            <div style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
              gap: "16px",
              padding: "16px",
              background: "rgba(var(--primary-color-rgb), 0.02)",
              border: "1px solid var(--border-color)",
              borderRadius: "var(--border-radius)",
              marginTop: "8px",
              marginBottom: "8px"
            }}>
              <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
                <label style={{ fontSize: "12px", fontWeight: "600", color: "var(--text-muted)" }}>
                  Assign to Sale (Optional)
                </label>
                <select
                  value={selectedSaleId || ""}
                  onChange={(e) => setSelectedSaleId(e.target.value ? Number(e.target.value) : null)}
                  style={{
                    padding: "8px 12px",
                    borderRadius: "var(--border-radius)",
                    border: "1px solid var(--border-color)",
                    background: "var(--bg-card)",
                    color: "var(--text-color)",
                    fontSize: "13px",
                    width: "100%"
                  }}
                  aria-label="Assign to Sale"
                >
                  <option value="">-- Unassigned / Keep Current --</option>
                  {saleUsers.map(u => (
                    <option key={u.id} value={u.id}>{u.name} ({u.username})</option>
                  ))}
                </select>
              </div>

              <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
                <label style={{ fontSize: "12px", fontWeight: "600", color: "var(--text-muted)" }}>
                  Assign to Tele Sale (Optional)
                </label>
                <select
                  value={selectedTelesaleId || ""}
                  onChange={(e) => setSelectedTelesaleId(e.target.value ? Number(e.target.value) : null)}
                  style={{
                    padding: "8px 12px",
                    borderRadius: "var(--border-radius)",
                    border: "1px solid var(--border-color)",
                    background: "var(--bg-card)",
                    color: "var(--text-color)",
                    fontSize: "13px",
                    width: "100%"
                  }}
                  aria-label="Assign to Tele Sale"
                >
                  <option value="">-- Unassigned / Keep Current --</option>
                  {telesaleUsers.map(u => (
                    <option key={u.id} value={u.id}>{u.name} ({u.username})</option>
                  ))}
                </select>
              </div>
            </div>

            {/* Tab Controls */}
            {hasValidated && (
              <div style={{ display: "flex", gap: "8px", borderBottom: "1px solid var(--border-color)", paddingBottom: "8px", flexWrap: "wrap", alignItems: "center" }}>
                <button
                  onClick={() => setActiveTab("autoReady")}
                  style={{
                    padding: "8px 16px",
                    borderRadius: "20px",
                    fontSize: "12px",
                    fontWeight: "700",
                    background: activeTab === "autoReady" ? "rgba(34, 197, 94, 0.1)" : "transparent",
                    color: activeTab === "autoReady" ? "#16a34a" : "var(--text-muted)",
                    border: "none",
                    cursor: "pointer"
                  }}
                  type="button"
                >
                  Ready to Import ({activeSummary.autoReadyCount})
                </button>
                <button
                  onClick={() => setActiveTab("needsReview")}
                  style={{
                    padding: "8px 16px",
                    borderRadius: "20px",
                    fontSize: "12px",
                    fontWeight: "700",
                    background: activeTab === "needsReview" ? "rgba(234, 179, 8, 0.1)" : "transparent",
                    color: activeTab === "needsReview" ? "#ca8a04" : "var(--text-muted)",
                    border: "none",
                    cursor: "pointer"
                  }}
                  type="button"
                >
                  Needs Review ({activeSummary.needsReviewCount})
                </button>
                <button
                  onClick={() => setActiveTab("duplicates")}
                  style={{
                    padding: "8px 16px",
                    borderRadius: "20px",
                    fontSize: "12px",
                    fontWeight: "700",
                    background: activeTab === "duplicates" ? "rgba(239, 68, 68, 0.1)" : "transparent",
                    color: activeTab === "duplicates" ? "#dc2626" : "var(--text-muted)",
                    border: "none",
                    cursor: "pointer"
                  }}
                  type="button"
                >
                  Duplicates ({activeSummary.duplicateCount})
                </button>
                <button
                  onClick={() => setActiveTab("errors")}
                  style={{
                    padding: "8px 16px",
                    borderRadius: "20px",
                    fontSize: "12px",
                    fontWeight: "700",
                    background: activeTab === "errors" ? "rgba(220, 38, 38, 0.1)" : "transparent",
                    color: activeTab === "errors" ? "#dc2626" : "var(--text-muted)",
                    border: "none",
                    cursor: "pointer"
                  }}
                  type="button"
                >
                  Errors ({activeSummary.errorCount})
                </button>

                {activeTab === "autoReady" && activeSummary.autoReadyCount > 0 && (
                  <button
                    onClick={handleBulkConfirmAutoReady}
                    className="secondary-button"
                    style={{
                      marginLeft: "auto",
                      background: "#16a34a",
                      color: "#ffffff",
                      border: "none",
                      padding: "6px 12px",
                      borderRadius: "var(--border-radius)",
                      fontSize: "12px",
                      fontWeight: "700",
                      cursor: "pointer",
                      display: "flex",
                      alignItems: "center",
                      gap: "6px"
                    }}
                    type="button"
                  >
                    <Check size={14} />
                    Confirm All Auto-Ready
                  </button>
                )}
              </div>
            )}

            <div className="table-wrap" style={{ maxHeight: "400px", overflowY: "auto", border: "1px solid var(--border-color)", borderRadius: "var(--border-radius)" }}>
              <table className="corporate-table" style={{ width: "100%", tableLayout: "auto" }} aria-label="Customer drafts combined table">
                <thead>
                  <tr>
                    <th style={{ width: "50px" }}>Status</th>
                    {hasValidated && <th style={{ minWidth: "180px" }}>Duplicate Match</th>}
                    {hasValidated && <th style={{ minWidth: "150px" }}>Resolution Action</th>}
                    <th>Source</th>
                    <th>Company Name</th>
                    <th>Address</th>
                    <th>Phone</th>
                    <th>Capital</th>
                    <th>Business Type</th>
                    <th>Contact Name</th>
                    <th>Contact Email</th>
                    <th>Contact Tel</th>
                    <th>Contact Position</th>
                  </tr>
                </thead>
                <tbody>
                  {rowsToRender.map((row: any, mapIdx) => {
                    const idx = row._originalIdx;
                    const rowStatus = row.status || "valid";
                    const isRowValid = rowStatus === "valid";
                    const isRowError = rowStatus === "error";
                    const isRowWarning = rowStatus === "warning";

                    // Determine border/shading style based on status
                    let rowBg = "transparent";
                    if (hasValidated) {
                      if (isRowError) rowBg = "rgba(239, 68, 68, 0.03)";
                      else if (isRowWarning) rowBg = "rgba(249, 115, 22, 0.02)";
                    }

                    // Render columns from either validated (normalized) or raw mapping values
                    const nameVal = row.name || (hasValidated ? "" : rawDraftRows[idx]?.["name"]);
                    const addrVal = row.address || (hasValidated ? "" : rawDraftRows[idx]?.["address"]);
                    const phoneVal = row.phone || (hasValidated ? "" : rawDraftRows[idx]?.["phone"]);
                    const capVal = row.capital !== undefined ? row.capital : (hasValidated ? "" : rawDraftRows[idx]?.["capital"]);
                    const btVal = row.businessType || row.business_type || (hasValidated ? "" : rawDraftRows[idx]?.["business_type"]);
                    const contactNameVal = row.contactName || row.contact_name || (hasValidated ? "" : rawDraftRows[idx]?.["contact_name"]);
                    const contactEmailVal = row.contactEmail || row.contact_email || (hasValidated ? "" : rawDraftRows[idx]?.["contact_email"]);
                    const contactTelVal = row.contactTel || row.contact_tel || (hasValidated ? "" : rawDraftRows[idx]?.["contact_tel"]);
                    const contactPosVal = row.contactPosition || row.contact_position || (hasValidated ? "" : rawDraftRows[idx]?.["contact_position"]);

                    const suggestedCallAngle = row.suggestedCallAngle || row.suggested_call_angle;
                    const suggestedPriority = row.suggestedPriority || row.suggested_priority;
                    const suggestedFollowUpDays = row.suggestedFollowUpDays !== undefined ? row.suggestedFollowUpDays : row.suggested_follow_up_days;

                    const renderCell = (fieldKey: string, val: any) => {
                      if (!hasValidated) {
                        return (val !== undefined && val !== null && val !== "") ? val : <span style={{ color: "var(--text-muted)", fontStyle: "italic" }}>-</span>;
                      }

                      const meta = row.extractionMetadata?.[fieldKey];
                      const displayVal = (val !== undefined && val !== null && val !== "") ? val : <span style={{ color: "var(--text-muted)", fontStyle: "italic" }}>-</span>;

                      if (meta && meta.source === "AiExtraction") {
                        return (
                          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "6px" }}>
                            <span>{displayVal}</span>
                            <span 
                              title={`AI Extracted (Confidence: ${Math.round(meta.confidence * 100)}%)`}
                              style={{ 
                                display: "inline-flex",
                                alignItems: "center", 
                                color: "#9333ea", 
                                background: "rgba(168, 85, 247, 0.1)", 
                                padding: "2px 4px", 
                                borderRadius: "4px", 
                                fontSize: "9px",
                                fontWeight: "700"
                              }}
                            >
                              <Sparkles size={8} style={{ marginRight: "2px" }} />
                              {Math.round(meta.confidence * 100)}%
                            </span>
                          </div>
                        );
                      }

                      return displayVal;
                    };

                    return (
                      <React.Fragment key={idx}>
                        <tr style={{ background: rowBg }}>
                          {/* Status Indicator */}
                          <td style={{ textAlign: "center" }}>
                            {!hasValidated ? (
                              <span style={{ color: "var(--text-muted)" }}>-</span>
                            ) : isRowError ? (
                              <span title="Contains errors"><AlertCircle size={16} style={{ color: "#dc2626" }} /></span>
                            ) : isRowWarning ? (
                              <span title="Contains warnings"><AlertTriangle size={16} style={{ color: "#ea580c" }} /></span>
                            ) : (
                              <span title="Valid row"><Check size={16} style={{ color: "#16a34a" }} /></span>
                            )}
                          </td>

                          {/* Duplicate Match Column */}
                          {hasValidated && (
                            <td>
                              {row.duplicate && row.duplicate.status !== "unique" ? (
                                <div style={{ fontSize: "12px", display: "flex", flexDirection: "column", gap: "2px" }}>
                                  <span style={{ 
                                    fontWeight: "600",
                                    color: row.duplicate.status === "duplicate" ? "#dc2626" : "#ea580c"
                                  }}>
                                    {row.duplicate.score}% - {row.duplicate.reason}
                                  </span>
                                  {row.duplicate.matchedCustomerName && (
                                    <span style={{ fontSize: "11px", color: "var(--text-muted)" }}>
                                      Match: {row.duplicate.matchedCustomerName} {row.duplicate.matchedCustomerCode ? `(${row.duplicate.matchedCustomerCode})` : ""}
                                    </span>
                                  )}
                                  <button
                                    onClick={() => handleAskAiExplanation("duplicate", "customer", nameVal || "", row.duplicate.reason, row.duplicate.matchedCustomerName)}
                                    className="ghost-button"
                                    style={{ fontSize: "11px", padding: "2px 6px", color: "var(--primary-color)", display: "flex", alignItems: "center", gap: "4px", width: "fit-content", marginTop: "4px" }}
                                    type="button"
                                  >
                                    <Sparkles size={11} />
                                    Ask AI why
                                  </button>
                                </div>
                              ) : (
                                <span style={{ fontSize: "12px", color: "#16a34a", fontWeight: "500" }}>No match (Unique)</span>
                              )}
                            </td>
                          )}

                          {/* Resolution Action Column */}
                          {hasValidated && (
                            <td>
                              <select
                                value={row.importAction || "new"}
                                onChange={(e) => handleActionChange(idx, e.target.value as any)}
                                style={{
                                  padding: "4px 8px",
                                  borderRadius: "4px",
                                  border: "1px solid var(--border-color)",
                                  background: "var(--bg-active)",
                                  fontSize: "12px",
                                  width: "100%"
                                }}
                              >
                                <option value="new">Import as new</option>
                                <option value="skip">Skip</option>
                                {row.duplicate?.matchedCustomerId && (
                                  <option value="update">Update existing</option>
                                )}
                              </select>
                            </td>
                          )}

                          {/* Source Label */}
                          <td style={{ fontSize: "11px", fontWeight: "600" }}>
                            <span 
                              style={{
                                padding: "3px 8px",
                                borderRadius: "12px",
                                background: row._source === "Uploaded File" ? "rgba(59, 130, 246, 0.1)" : "rgba(168, 85, 247, 0.1)",
                                color: row._source === "Uploaded File" ? "var(--primary-color)" : "#9333ea"
                              }}
                            >
                              {row._source}
                            </span>
                          </td>

                          {/* Fields */}
                          <td>{renderCell("name", nameVal)}</td>
                          <td>{renderCell("address", addrVal)}</td>
                          <td>{renderCell("phone", phoneVal)}</td>
                          <td>{renderCell("capital", capVal)}</td>
                          <td>{renderCell("business_type", btVal)}</td>
                          <td>{renderCell("contact_name", contactNameVal)}</td>
                          <td>{renderCell("contact_email", contactEmailVal)}</td>
                          <td>{renderCell("contact_tel", contactTelVal)}</td>
                          <td>{renderCell("contact_position", contactPosVal)}</td>
                        </tr>

                        {/* Inline issues details sub-row */}
                        {hasValidated && row.issues && row.issues.length > 0 && (
                          <tr style={{ background: rowBg }}>
                            <td colSpan={hasValidated ? 13 : 11} style={{ padding: "6px 16px 10px 16px", borderTop: "none" }}>
                              <div style={{ display: "flex", flexDirection: "column", gap: "6px", background: "rgba(0,0,0,0.015)", padding: "8px 12px", borderRadius: "4px" }}>
                                {row.issues.map((issue: any, issueIdx: number) => {
                                  const isError = issue.severity === "error";
                                  const getFieldValue = (f: string) => {
                                    switch (f.toLowerCase()) {
                                      case "name": return nameVal;
                                      case "address": return addrVal;
                                      case "phone": return phoneVal;
                                      case "capital": return capVal;
                                      case "business_type": return btVal;
                                      case "contact_name": return contactNameVal;
                                      case "contact_email": return contactEmailVal;
                                      case "contact_tel": return contactTelVal;
                                      case "contact_position": return contactPosVal;
                                      default: return "";
                                    }
                                  };
                                  return (
                                    <div 
                                      key={issueIdx} 
                                      style={{ 
                                        display: "flex", 
                                        alignItems: "center", 
                                        justifyContent: "space-between",
                                        gap: "8px", 
                                        fontSize: "12px",
                                        color: isError ? "#dc2626" : "#ea580c"
                                      }}
                                    >
                                      <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                                        {isError ? <AlertCircle size={13} /> : <AlertTriangle size={13} />}
                                        <span>
                                          <strong>[{issue.field.toUpperCase()}]:</strong> {issue.message}
                                        </span>
                                      </div>
                                      
                                      <button
                                        onClick={() => handleAskAiExplanation("validation", issue.field, String(getFieldValue(issue.field) || ""), issue.message)}
                                        className="ghost-button"
                                        style={{ fontSize: "11px", padding: "2px 6px", color: "var(--primary-color)", display: "flex", alignItems: "center", gap: "4px" }}
                                        type="button"
                                      >
                                        <Sparkles size={11} />
                                        Explain with AI
                                      </button>
                                    </div>
                                  );
                                })}
                              </div>
                            </td>
                          </tr>
                        )}

                        {/* Telesales call angle suggestion panel */}
                        {hasValidated && suggestedCallAngle && (
                          <tr style={{ background: rowBg }}>
                            <td colSpan={hasValidated ? 13 : 11} style={{ padding: "4px 16px 8px 16px", borderTop: "none" }}>
                              <div style={{ display: "flex", gap: "10px", background: "rgba(59, 130, 246, 0.05)", border: "1px solid rgba(0, 91, 187, 0.22)", padding: "8px 12px", borderRadius: "4px", fontSize: "12px" }}>
                                <span style={{ fontWeight: "700", color: "var(--primary-color)" }}>Telesales Tip:</span>
                                <span style={{ color: "var(--text-color)", flex: 1 }}>{suggestedCallAngle}</span>
                                <span style={{ fontSize: "11px", background: "var(--bg-active)", padding: "2px 6px", borderRadius: "10px", color: "var(--text-muted)" }}>Priority: <strong style={{ color: suggestedPriority === "High" ? "#dc2626" : suggestedPriority === "Low" ? "#16a34a" : "#ca8a04" }}>{suggestedPriority}</strong></span>
                                <span style={{ fontSize: "11px", background: "var(--bg-active)", padding: "2px 6px", borderRadius: "10px", color: "var(--text-muted)" }}>Follow-up: <strong>{suggestedFollowUpDays} days</strong></span>
                              </div>
                            </td>
                          </tr>
                        )}
                      </React.Fragment>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {/* Pagination Controls */}
            {fileId && previewData && (
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", borderTop: "1px solid var(--border-color)", paddingTop: "12px", marginTop: "8px" }}>
                <div style={{ fontSize: "13px", color: "var(--text-muted)" }}>
                  Showing Page <strong>{currentPage}</strong> of <strong>{Math.ceil(previewData.totalRows / pageSize)}</strong> ({previewData.totalRows} total rows)
                </div>
                <div style={{ display: "flex", gap: "8px" }}>
                  <button
                    className="secondary-button"
                    disabled={currentPage <= 1 || isValidating || isLoading}
                    onClick={() => handlePageChange(currentPage - 1)}
                    type="button"
                    style={{ fontSize: "12px", padding: "6px 12px" }}
                  >
                    Previous Page
                  </button>
                  <button
                    className="secondary-button"
                    disabled={currentPage >= Math.ceil(previewData.totalRows / pageSize) || isValidating || isLoading}
                    onClick={() => handlePageChange(currentPage + 1)}
                    type="button"
                    style={{ fontSize: "12px", padding: "6px 12px" }}
                  >
                    Next Page
                  </button>
                </div>
              </div>
            )}

            {/* Validation & Save actions */}
            <div style={{ display: "flex", gap: "12px", justifyContent: "flex-end", marginTop: "16px", alignItems: "center", flexWrap: "wrap" }}>
              {fileId && validationSummary && (validationSummary.error > 0 || validationSummary.warning > 0) && (
                <a
                  href={apiService.exportErrorsUrl(fileId, mappings)}
                  className="secondary-button"
                  style={{ display: "flex", alignItems: "center", gap: "8px", textDecoration: "none", color: "#dc2626", borderColor: "#dc2626", fontSize: "13px", padding: "8px 14px" }}
                  download="import_validation_errors.csv"
                >
                  <AlertCircle size={15} />
                  Download Error Report
                </a>
              )}

              <button
                className="ghost-button"
                onClick={handleValidate}
                disabled={rawDraftRows.length === 0 || isValidating}
                type="button"
                style={{ display: "flex", alignItems: "center", gap: "8px" }}
              >
                {isValidating ? (
                  <span className="spinner" style={{ width: "14px", height: "14px" }}></span>
                ) : (
                  <Sparkles size={16} />
                )}
                {isValidating ? "Validating..." : "Validate Drafts"}
              </button>

              <button 
                className="primary-button" 
                onClick={handleImportToDb}
                disabled={!hasValidated || (validationSummary?.error ?? 0) > 0}
                type="button"
                style={{ display: "flex", alignItems: "center", gap: "8px" }}
              >
                <Database size={16} />
                Save Drafts to Database
              </button>
            </div>
          </div>
        )}
      </main>

      {/* Streaming Commit Progress Overlay */}
      {isCommitingStream && commitProgress && (
        <div style={{
          position: "fixed",
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: "rgba(0, 0, 0, 0.5)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          zIndex: 1000,
          backdropFilter: "none"
        }}>
          <div className="panel panel-padded" style={{ width: "400px", background: "var(--bg-card)", borderRadius: "var(--border-radius)", display: "flex", flexDirection: "column", gap: "16px", textAlign: "center" }}>
            <h2>Importing Customers...</h2>
            <div style={{ fontSize: "14px", fontWeight: "600" }}>
              {commitProgress.currentProgress} / {commitProgress.totalRows} Rows Processed
            </div>
            
            {/* Progress Bar */}
            <div style={{ width: "100%", background: "var(--border-color)", height: "8px", borderRadius: "4px", overflow: "hidden" }}>
              <div style={{
                width: `${(commitProgress.currentProgress / (commitProgress.totalRows || 1)) * 100}%`,
                background: "var(--primary-color)",
                height: "100%",
                transition: "width 0.2s ease"
              }}></div>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "10px", fontSize: "12px" }}>
              <div style={{ color: "#16a34a" }}><strong>{commitProgress.imported}</strong> Imported</div>
              <div style={{ color: "var(--primary-color)" }}><strong>{commitProgress.updated}</strong> Updated</div>
              <div style={{ color: "var(--text-muted)" }}><strong>{commitProgress.skipped}</strong> Skipped</div>
            </div>

            <button
              onClick={handleCancelCommit}
              className="secondary-button"
              style={{ borderColor: "#dc2626", color: "#dc2626", marginTop: "10px" }}
              type="button"
            >
              Cancel Import
            </button>
          </div>
        </div>
      )}

      {/* AI Explanation Drawer Modal */}
      {(isExplaining || explanationText) && (
        <div style={{
          position: "fixed",
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: "rgba(0, 0, 0, 0.4)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          zIndex: 999,
          backdropFilter: "none"
        }}>
          <div className="panel panel-padded" style={{ width: "450px", background: "var(--bg-card)", borderRadius: "var(--border-radius)", display: "flex", flexDirection: "column", gap: "16px" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "8px", borderBottom: "1px solid var(--border-color)", paddingBottom: "10px" }}>
              <Sparkles size={20} style={{ color: "var(--primary-color)" }} />
              <h2 style={{ margin: 0 }}>AI Assistant Explanation</h2>
            </div>
            
            {isExplaining ? (
              <div style={{ display: "flex", flexDirection: "column", alignItems: "center", padding: "20px 0", gap: "10px" }}>
                <span className="spinner" style={{ width: "24px", height: "24px" }}></span>
                <span style={{ fontSize: "13px", color: "var(--text-muted)" }}>Consulting AI Assistant...</span>
              </div>
            ) : (
              <div style={{ fontSize: "14px", lineHeight: "1.5", color: "var(--text-color)" }}>
                {explanationText}
              </div>
            )}

            <div style={{ display: "flex", justifyContent: "flex-end", borderTop: "1px solid var(--border-color)", paddingTop: "10px" }}>
              <button 
                onClick={() => { setExplanationText(null); setIsExplaining(false); }}
                className="primary-button"
                type="button"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
