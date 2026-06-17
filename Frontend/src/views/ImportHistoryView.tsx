import React, { useState, useEffect, useCallback } from "react";
import { History, ChevronDown, ChevronUp, AlertCircle, Calendar, FileSpreadsheet, User, CheckCircle, Database } from "lucide-react";
import { apiService } from "../domain/apiService";
import { ForbiddenView } from "./ForbiddenView";
import { isAdminRole } from "../domain/permissions";
import { Pagination } from "../components/Pagination";

interface ImportHistoryViewProps {
  userRole: string;
  showToast: (msg: string, type: "success" | "error" | "info") => void;
}

export const ImportHistoryView: React.FC<ImportHistoryViewProps> = ({ userRole, showToast }) => {
  const [historyList, setHistoryList] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [expandedSessionId, setExpandedSessionId] = useState<number | null>(null);

  // Pagination State
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);

  const isAdmin = isAdminRole(userRole);

  const fetchHistory = useCallback(async (currentPage: number, currentPageSize: number) => {
    try {
      setIsLoading(true);
      const res = await apiService.getImportHistoryPaginated({ page: currentPage, pageSize: currentPageSize });
      setHistoryList(res.items);
      setTotalCount(res.totalCount);
      setTotalPages(res.totalPages);
    } catch (err: any) {
      showToast(err.message || "Failed to load import history.", "error");
    } finally {
      setIsLoading(false);
    }
  }, [showToast]);

  useEffect(() => {
    if (isAdmin) {
      void fetchHistory(page, pageSize);
    }
  }, [isAdmin, page, pageSize, fetchHistory]);

  if (!isAdmin) {
    return <ForbiddenView />;
  }

  const toggleExpand = (id: number) => {
    setExpandedSessionId(prev => (prev === id ? null : id));
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return "-";
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  return (
    <div className="workspace-view">
      <header className="topbar">
        <div>
          <p>Admin / Import History</p>
          <h1>Import History Logs</h1>
        </div>
      </header>

      <main className="content animate-fade-in">
        <div className="panel panel-padded" style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
          <div className="panel-header" style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <div>
              <h2>Audit Session Records</h2>
              <p>Logs of all customer imports, including row counts, status updates, assignees, and validation errors.</p>
            </div>
            <button 
              className="secondary-button" 
              onClick={() => fetchHistory(page, pageSize)} 
              disabled={isLoading}
              type="button"
            >
              Refresh Logs
            </button>
          </div>

          <div className="table-wrap">
            <table className="corporate-table" style={{ width: "100%" }} aria-label="Import history records table">
              <thead>
                <tr>
                  <th style={{ width: "6%" }}>No.</th>
                  <th style={{ width: "16%" }}>Timestamp</th>
                  <th style={{ width: "18%" }}>File Name</th>
                  <th style={{ width: "15%" }}>Imported By</th>
                  <th style={{ width: "10%" }}>Total Rows</th>
                  <th style={{ width: "10%" }}>Imported</th>
                  <th style={{ width: "10%" }}>Skipped</th>
                  <th style={{ width: "10%" }}>Errors</th>
                  <th style={{ width: "5%" }}>&nbsp;</th>
                </tr>
              </thead>
              <tbody>
                {isLoading ? (
                  <tr>
                    <td colSpan={9} style={{ textAlign: "center", padding: "32px 0" }}>
                      <span className="spinner" style={{ display: "inline-block", width: "24px", height: "24px", border: "3px solid var(--border-color)", borderTopColor: "var(--primary-color)", borderRadius: "50%", animation: "spin 1s linear infinite" }}></span>
                      <p style={{ marginTop: "8px", color: "var(--text-muted)", fontSize: "13px" }}>Loading history logs...</p>
                    </td>
                  </tr>
                ) : historyList.length > 0 ? (
                  historyList.map((session, index) => {
                    const isExpanded = expandedSessionId === session.id;
                    const hasErrors = session.errorRows > 0 || (session.errorsJson && session.errorsJson !== "[]");
                    
                    let parsedErrors: string[] = [];
                    if (session.errorsJson) {
                      try {
                        parsedErrors = JSON.parse(session.errorsJson);
                      } catch {
                        parsedErrors = [session.errorsJson];
                      }
                    }

                    return (
                      <React.Fragment key={session.id}>
                        <tr 
                          style={{ 
                            cursor: hasErrors ? "pointer" : "default",
                            background: hasErrors ? "rgba(239, 68, 68, 0.01)" : "transparent"
                          }}
                          onClick={() => hasErrors && toggleExpand(session.id)}
                        >
                          <td>{totalCount - ((page - 1) * pageSize + index)}</td>
                          <td>
                            <div style={{ display: "flex", alignItems: "center", gap: "6px", fontSize: "13px" }}>
                              <Calendar size={14} style={{ color: "var(--text-muted)" }} />
                              {formatDate(session.createdAt)}
                            </div>
                          </td>
                          <td>
                            <div style={{ display: "flex", alignItems: "center", gap: "8px", fontWeight: "600", fontSize: "13px" }}>
                              <FileSpreadsheet size={16} style={{ color: "var(--primary-color)" }} />
                              {session.fileName || "Unstructured Text Extract"}
                            </div>
                          </td>
                          <td>
                            <div style={{ display: "flex", alignItems: "center", gap: "6px", fontSize: "13px" }}>
                              <User size={14} style={{ color: "var(--text-muted)" }} />
                              {session.importedBy}
                            </div>
                          </td>
                          <td style={{ fontWeight: "600" }}>{session.totalRows}</td>
                          <td style={{ color: session.importedRows > 0 ? "#16a34a" : "inherit", fontWeight: session.importedRows > 0 ? "600" : "normal" }}>
                            {session.importedRows}
                          </td>
                          <td style={{ color: "var(--text-muted)" }}>{session.skippedRows}</td>
                          <td style={{ color: session.errorRows > 0 ? "#dc2626" : "inherit", fontWeight: session.errorRows > 0 ? "600" : "normal" }}>
                            {session.errorRows}
                          </td>
                          <td style={{ textAlign: "right" }}>
                            {hasErrors && (
                              <button 
                                className="ghost-button" 
                                style={{ padding: "4px" }}
                                onClick={(e) => {
                                  e.stopPropagation();
                                  toggleExpand(session.id);
                                }}
                                aria-label="Toggle error details"
                                type="button"
                              >
                                {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                              </button>
                            )}
                          </td>
                        </tr>

                        {isExpanded && hasErrors && (
                          <tr>
                            <td colSpan={9} style={{ padding: "12px 24px", background: "rgba(239, 68, 68, 0.02)", borderTop: "none" }}>
                              <div style={{ display: "flex", flexDirection: "column", gap: "8px", borderLeft: "3px solid #dc2626", paddingLeft: "16px" }}>
                                <div style={{ fontWeight: "600", color: "#dc2626", fontSize: "13px", display: "flex", alignItems: "center", gap: "6px" }}>
                                  <AlertCircle size={15} />
                                  Execution Errors & Rollback Context
                                </div>
                                <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
                                  {parsedErrors.length > 0 ? (
                                    parsedErrors.map((err, errIdx) => (
                                      <div key={errIdx} style={{ fontSize: "12px", color: "var(--text-color)", background: "var(--bg-card)", padding: "8px 12px", borderRadius: "4px", border: "1px solid var(--border-color)", fontFamily: "monospace" }}>
                                        {err}
                                      </div>
                                    ))
                                  ) : (
                                    <div style={{ fontSize: "12px", color: "var(--text-muted)" }}>
                                      No detailed error logs written.
                                    </div>
                                  )}
                                </div>
                              </div>
                            </td>
                          </tr>
                        )}
                      </React.Fragment>
                    );
                  })
                ) : (
                  <tr>
                    <td colSpan={9} style={{ textAlign: "center", padding: "32px 0", color: "var(--text-muted)" }}>
                      No import history found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={totalCount}
            totalPages={totalPages}
            onPageChange={setPage}
            onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
          />
        </div>
      </main>
    </div>
  );
};
