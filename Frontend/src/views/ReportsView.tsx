import React, { useState, useEffect, useMemo } from "react";
import { apiService } from "../domain/apiService";
import type { Customer, User } from "../domain/types";
import { FileText, Calendar, TrendingUp, Key, ClipboardList } from "lucide-react";
import { ForbiddenView } from "./ForbiddenView";

type ReportTab = "operation" | "assign" | "performance" | "renewal" | "project-detail";

export const ReportsView: React.FC = () => {
  const [activeTab, setActiveTab] = useState<ReportTab>("operation");
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [projectDetailSummary, setProjectDetailSummary] = useState<any[]>([]);
  const [agentPerformance, setAgentPerformance] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isForbidden, setIsForbidden] = useState(false);

  // Renewal filter
  const [renewalFilterDays, setRenewalFilterDays] = useState(30);

  const loadData = async () => {
    try {
      setIsLoading(true);
      setIsForbidden(false);
      const [custs, uList, reportData] = await Promise.all([
        apiService.getCustomers(),
        apiService.getUsers(),
        apiService.getReports()
      ]);
      setCustomers(custs);
      setUsers(uList);
      setProjectDetailSummary(reportData.projectLedger);
      setAgentPerformance(reportData.agentPerformance);
    } catch (err: any) {
      if (err.message?.includes("403") || err.message?.includes("Forbidden")) {
        setIsForbidden(true);
      } else {
        console.error("Failed to load reports data", err);
      }
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  // 1. Operation report data
  const operationReport = useMemo(() => {
    return customers.map(c => ({
      id: c.id,
      name: c.name,
      address: c.address,
      status: c.status,
      business: c.bt_type,
      updatedAt: c.updatedAt,
      is_active: c.is_active
    }));
  }, [customers]);

  // 2. Assign History data
  const assignHistory = useMemo(() => {
    return customers.filter(c => c.sale_id || c.telesale_id).map(c => {
      const sale = users.find(u => u.id === c.sale_id)?.name || "-";
      const tele = users.find(u => u.id === c.telesale_id)?.name || "-";
      return {
        id: c.id,
        name: c.name,
        sale,
        tele,
        assignedAt: c.updatedAt,
        status: c.status
      };
    });
  }, [customers, users]);

  // 4. Renewal summary list
  const renewalSummary = useMemo(() => {
    return customers
      .filter(c => c.is_active && c.renewalDays <= renewalFilterDays)
      .map(c => ({
        id: c.id,
        name: c.name,
        business: c.bt_type,
        renewalDays: c.renewalDays,
        expireDate: new Date(Date.now() + c.renewalDays * 24 * 60 * 60 * 1000).toISOString().split("T")[0],
        status: c.status
      }))
      .sort((a, b) => a.renewalDays - b.renewalDays);
  }, [customers, renewalFilterDays]);

  if (isForbidden) {
    return <ForbiddenView />;
  }

  return (
    <div className="workspace-view">
      <header className="topbar">
        <div>
          <p>Report / List</p>
          <h1>System Reports</h1>
        </div>
      </header>

      {/* Sub-tab navigation */}
      <div className="reports-tab-nav">
        <button 
          className={`report-tab-btn ${activeTab === "operation" ? "active" : ""}`}
          onClick={() => setActiveTab("operation")}
          type="button"
        >
          <ClipboardList size={15} />
          Operation Report
        </button>
        <button 
          className={`report-tab-btn ${activeTab === "assign" ? "active" : ""}`}
          onClick={() => setActiveTab("assign")}
          type="button"
        >
          <Key size={15} />
          Assign History
        </button>
        <button 
          className={`report-tab-btn ${activeTab === "performance" ? "active" : ""}`}
          onClick={() => setActiveTab("performance")}
          type="button"
        >
          <TrendingUp size={15} />
          Performance
        </button>
        <button 
          className={`report-tab-btn ${activeTab === "renewal" ? "active" : ""}`}
          onClick={() => setActiveTab("renewal")}
          type="button"
        >
          <Calendar size={15} />
          Summary Renewal
        </button>
        <button 
          className={`report-tab-btn ${activeTab === "project-detail" ? "active" : ""}`}
          onClick={() => setActiveTab("project-detail")}
          type="button"
        >
          <FileText size={15} />
          Project Details
        </button>
      </div>

      <main className="content animate-fade-in">
        {isLoading ? (
          <div className="panel" style={{ padding: "32px", textAlign: "center" }}>
            Loading report data...
          </div>
        ) : (
          <>
            {/* REPORT 1: OPERATION REPORT */}
            {activeTab === "operation" && (
              <div className="panel animate-fade-in">
                <div className="panel-header">
                  <h2>Operations Log</h2>
                  <p>Overall state and metadata history of all business customer entities.</p>
                </div>
                <div className="table-wrap">
                  <table className="corporate-table" aria-label="Customer operations log table">
                    <thead>
                      <tr>
                        <th style={{ width: "5%" }}>No.</th>
                        <th style={{ width: "35%" }}>Customer</th>
                        <th style={{ width: "20%" }}>Business</th>
                        <th style={{ width: "15%" }}>Status</th>
                        <th style={{ width: "15%" }}>Last Update</th>
                        <th style={{ width: "10%" }}>Active</th>
                      </tr>
                    </thead>
                    <tbody>
                      {operationReport.map((item, index) => (
                        <tr key={item.id}>
                          <td>{index + 1}</td>
                          <td><strong>{item.name}</strong><br /><span className="subtext">{item.address}</span></td>
                          <td>{item.business}</td>
                          <td>
                            <span className={`status-badge ${item.status.toLowerCase()}`}>{item.status}</span>
                          </td>
                          <td>{item.updatedAt}</td>
                          <td>{item.is_active ? "Yes" : "No"}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* REPORT 2: ASSIGN HISTORY */}
            {activeTab === "assign" && (
              <div className="panel animate-fade-in">
                <div className="panel-header">
                  <h2>Customer Assignment History</h2>
                  <p>Historical audit log of client accounts assigned to Sales managers and Telesales teams.</p>
                </div>
                <div className="table-wrap">
                  <table className="corporate-table" aria-label="Customer assignment history table">
                    <thead>
                      <tr>
                        <th style={{ width: "5%" }}>No.</th>
                        <th style={{ width: "30%" }}>Customer</th>
                        <th style={{ width: "20%" }}>Assigned Sale</th>
                        <th style={{ width: "20%" }}>Assigned Telesale</th>
                        <th style={{ width: "15%" }}>Assignment Date</th>
                        <th style={{ width: "10%" }}>Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {assignHistory.length > 0 ? (
                        assignHistory.map((item, index) => (
                          <tr key={item.id}>
                            <td>{index + 1}</td>
                            <td><strong>{item.name}</strong></td>
                            <td>{item.sale}</td>
                            <td>{item.tele}</td>
                            <td>{item.assignedAt}</td>
                            <td><span className={`status-badge ${item.status.toLowerCase()}`}>{item.status}</span></td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={6} style={{ textAlign: "center", padding: "24px 0" }}>
                            No assignments recorded yet.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* REPORT 3: PERFORMANCE REPORT */}
            {activeTab === "performance" && (
              <div className="panel animate-fade-in">
                <div className="panel-header">
                  <h2>Agent Sales Performance leaderboard</h2>
                  <p>Live rankings of Sales and Telesales representatives sorted by their accumulated client points.</p>
                </div>
                <div className="table-wrap">
                  <table className="corporate-table" aria-label="Sales performance table">
                    <thead>
                      <tr>
                        <th style={{ width: "5%" }}>Rank</th>
                        <th style={{ width: "25%" }}>Agent Name</th>
                        <th style={{ width: "15%" }}>Role</th>
                        <th style={{ width: "12%" }}>Bookings</th>
                        <th style={{ width: "12%" }}>Win Status</th>
                        <th style={{ width: "16%" }}>Points Total</th>
                        <th style={{ width: "15%" }}>Target Progress (150 pts)</th>
                      </tr>
                    </thead>
                    <tbody>
                      {agentPerformance.map((item, index) => (
                        <tr key={item.id}>
                          <td>{index + 1}</td>
                          <td><strong>{item.name}</strong></td>
                          <td>
                            <span className={`status-badge info`}>{item.role}</span>
                          </td>
                          <td>{item.bookings}</td>
                          <td>{item.wins} wins</td>
                          <td><strong>{item.points} points</strong></td>
                          <td>
                            <div className="perf-bar-container">
                              <div 
                                className={`perf-bar ${item.progress >= 100 ? "high" : item.progress > 50 ? "mid" : "low"}`}
                                style={{ 
                                  width: `${item.progress}%`
                                }} 
                              />
                            </div>
                            <span className="subtext">{item.progress}% completed</span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* REPORT 4: SUMMARY RENEWAL */}
            {activeTab === "renewal" && (
              <div className="panel animate-fade-in">
                <div className="panel-header split">
                  <div>
                    <h2>Antivirus Licensing Renewal Monitor</h2>
                    <p>List of active client nodes whose licenses require immediate extension.</p>
                  </div>
                  <div className="filter-pill-container">
                    {[30, 60, 90].map(days => (
                      <button 
                        key={days} 
                        className={`filter-pill ${renewalFilterDays === days ? "active" : ""}`}
                        onClick={() => setRenewalFilterDays(days)}
                        type="button"
                      >
                        {days} Days
                      </button>
                    ))}
                  </div>
                </div>
                <div className="table-wrap">
                  <table className="corporate-table" aria-label="Licensing renewal monitor table">
                    <thead>
                      <tr>
                        <th style={{ width: "5%" }}>No.</th>
                        <th style={{ width: "35%" }}>Customer</th>
                        <th style={{ width: "20%" }}>Business Type</th>
                        <th style={{ width: "15%" }}>Days Remaining</th>
                        <th style={{ width: "15%" }}>Estimated Expire</th>
                        <th style={{ width: "10%" }}>Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {renewalSummary.length > 0 ? (
                        renewalSummary.map((item, index) => (
                          <tr key={item.id} className={item.renewalDays <= 15 ? "urgent-row" : ""}>
                            <td>{index + 1}</td>
                            <td><strong>{item.name}</strong></td>
                            <td>{item.business}</td>
                            <td>
                              <strong className={item.renewalDays <= 15 ? "text-danger-strong" : "text-warning-strong"}>
                                {item.renewalDays} days
                              </strong>
                            </td>
                            <td>{item.expireDate}</td>
                            <td><span className={`status-badge ${item.status.toLowerCase()}`}>{item.status}</span></td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={6} style={{ textAlign: "center", padding: "24px 0" }}>
                            No clients expiring within {renewalFilterDays} days.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* REPORT 5: SUMMARY PROJECT DETAIL */}
            {activeTab === "project-detail" && (
              <div className="panel animate-fade-in">
                <div className="panel-header">
                  <h2>Project Implementation & License Ledger</h2>
                  <p>Unified catalog tracking all products, licenses, and renewal projects currently in progress.</p>
                </div>
                <div className="table-wrap">
                  <table className="corporate-table" aria-label="Implementation ledger table">
                    <thead>
                      <tr>
                        <th style={{ width: "5%" }}>No.</th>
                        <th style={{ width: "25%" }}>Customer Name</th>
                        <th style={{ width: "15%" }}>Contact Person</th>
                        <th style={{ width: "30%" }}>Project / License Description</th>
                        <th style={{ width: "10%" }}>Exp Date</th>
                        <th style={{ width: "10%" }}>Points</th>
                        <th style={{ width: "5%" }}>Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {projectDetailSummary.length > 0 ? (
                        projectDetailSummary.map((item, index) => (
                          <tr key={index}>
                            <td>{index + 1}</td>
                            <td><strong>{item.customerName}</strong></td>
                            <td>{item.contactName}</td>
                            <td>
                              <span className={`record-type-badge ${item.type === "Project" ? "project" : "license"}`}>
                                {item.type}
                              </span>
                              <strong style={{ display: "block" }}>{item.details}</strong>
                            </td>
                            <td>{item.closeDate}</td>
                            <td><span className="point-badge">{item.points} pts</span></td>
                            <td><span className={`status-badge ${item.status.toLowerCase()}`}>{item.status}</span></td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={7} style={{ textAlign: "center", padding: "24px 0" }}>
                            No project detail ledger items found.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </>
        )}
      </main>
    </div>
  );
};
