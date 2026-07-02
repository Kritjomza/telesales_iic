import React, { useState, useEffect, useMemo } from "react";
import { apiService } from "../domain/apiService";
import type { Customer, User } from "../domain/types";
import {
  AlertTriangle,
  BarChart3,
  Calendar,
  ClipboardList,
  FileText,
  RefreshCw,
  ShieldCheck
} from "lucide-react";
import { ForbiddenView } from "./ForbiddenView";

export type ReportTab = "operation" | "renewal" | "project-detail";

type ReportsViewProps = {
  activeTab?: ReportTab;
  onTabChange?: (tab: ReportTab) => void;
};

type DistributionItem = {
  label: string;
  value: number;
  tone?: "blue" | "green" | "amber" | "red" | "slate";
};

const statusClassName = (status?: string) => (status || "unknown").toLowerCase().replace(/\s+/g, "-");

const formatCount = (value: number) => new Intl.NumberFormat("en-US").format(value);

const ReportDistribution: React.FC<{ title: string; items: DistributionItem[]; emptyText: string }> = ({
  title,
  items,
  emptyText
}) => {
  const maxValue = Math.max(...items.map(item => item.value), 0);

  return (
    <section className="report-chart-panel" aria-label={title}>
      <div className="report-section-heading">
        <h3>{title}</h3>
        <p>Distribution based on currently loaded report records.</p>
      </div>
      {items.length > 0 ? (
        <div className="report-bars">
          {items.map(item => (
            <div className="report-bar-row" key={item.label}>
              <div className="report-bar-label">
                <span>{item.label}</span>
                <strong>{formatCount(item.value)}</strong>
              </div>
              <div className="report-bar-track" aria-hidden="true">
                <span
                  className={`report-bar-fill ${item.tone || "slate"}`}
                  style={{ width: `${maxValue ? Math.max((item.value / maxValue) * 100, 3) : 0}%` }}
                />
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="report-empty-state compact">
          <BarChart3 size={18} />
          <strong>No chart data</strong>
          <span>{emptyText}</span>
        </div>
      )}
    </section>
  );
};

export const ReportsView: React.FC<ReportsViewProps> = ({ activeTab: controlledActiveTab, onTabChange }) => {
  const [internalActiveTab, setInternalActiveTab] = useState<ReportTab>("operation");
  const activeTab = controlledActiveTab ?? internalActiveTab;
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [projectDetailSummary, setProjectDetailSummary] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isForbidden, setIsForbidden] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Renewal filter
  const [renewalFilterDays, setRenewalFilterDays] = useState(30);

  const setActiveReportTab = (tab: ReportTab) => {
    if (controlledActiveTab === undefined) {
      setInternalActiveTab(tab);
    }
    onTabChange?.(tab);
  };

  const loadData = async () => {
    try {
      setIsLoading(true);
      setIsForbidden(false);
      setLoadError(null);
      const [custs, uList, reportData] = await Promise.all([
        apiService.getCustomers(),
        apiService.getUsers(),
        apiService.getReports()
      ]);
      setCustomers(custs);
      setUsers(uList);
      setProjectDetailSummary(reportData.projectLedger || []);
    } catch (err: any) {
      if (err.message?.includes("403") || err.message?.includes("Forbidden")) {
        setIsForbidden(true);
      } else {
        console.error("Failed to load reports data", err);
        setLoadError("Report data could not be loaded. Please retry or contact an administrator.");
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

  const reportSummary = useMemo(() => {
    const activeCustomers = customers.filter(customer => customer.is_active).length;
    const renewalRisk = customers.filter(customer => customer.is_active && customer.renewalDays <= 30).length;
    const statusCounts = operationReport.reduce<Record<string, number>>((acc, item) => {
      acc[item.status] = (acc[item.status] || 0) + 1;
      return acc;
    }, {});

    return {
      activeCustomers,
      inactiveCustomers: customers.length - activeCustomers,
      renewalRisk,
      statusCounts
    };
  }, [customers, operationReport]);

  const statusDistribution = useMemo<DistributionItem[]>(() => {
    return Object.entries(reportSummary.statusCounts)
      .map(([label, value]) => ({
        label,
        value,
        tone:
          label.toLowerCase() === "win" || label.toLowerCase() === "sent"
            ? "green" as const
            : label.toLowerCase() === "lost"
              ? "red" as const
              : label.toLowerCase() === "wait"
                ? "amber" as const
                : "blue" as const
      }))
      .sort((a, b) => b.value - a.value);
  }, [reportSummary.statusCounts]);

  const renewalDistribution = useMemo<DistributionItem[]>(() => {
    const buckets = [
      { label: "0-15 days", value: renewalSummary.filter(item => item.renewalDays <= 15).length, tone: "red" as const },
      {
        label: "16-30 days",
        value: renewalSummary.filter(item => item.renewalDays > 15 && item.renewalDays <= 30).length,
        tone: "amber" as const
      },
      {
        label: "31-60 days",
        value: renewalSummary.filter(item => item.renewalDays > 30 && item.renewalDays <= 60).length,
        tone: "blue" as const
      },
      {
        label: "61-90 days",
        value: renewalSummary.filter(item => item.renewalDays > 60 && item.renewalDays <= 90).length,
        tone: "slate" as const
      }
    ];

    return buckets.filter(bucket => bucket.value > 0);
  }, [renewalSummary]);

  if (isForbidden) {
    return <ForbiddenView />;
  }

  return (
    <div className="workspace-view reports-workspace">
      <header className="topbar reports-topbar">
        <div>
          <p>Report / Intelligence</p>
          <h1>System Reports</h1>
          <span>Operational visibility across customers, renewals, and project ledger data.</span>
        </div>
      </header>

      <main className="content reports-content animate-fade-in">
        <div className="reports-tab-nav" role="tablist" aria-label="Report sections">
          <button
            className={`report-tab-btn ${activeTab === "operation" ? "active" : ""}`}
            onClick={() => setActiveReportTab("operation")}
            type="button"
            role="tab"
            aria-selected={activeTab === "operation"}
          >
            <ClipboardList size={15} />
            Operation
          </button>
          <button
            className={`report-tab-btn ${activeTab === "renewal" ? "active" : ""}`}
            onClick={() => setActiveReportTab("renewal")}
            type="button"
            role="tab"
            aria-selected={activeTab === "renewal"}
          >
            <Calendar size={15} />
            Renewal
          </button>
          <button
            className={`report-tab-btn ${activeTab === "project-detail" ? "active" : ""}`}
            onClick={() => setActiveReportTab("project-detail")}
            type="button"
            role="tab"
            aria-selected={activeTab === "project-detail"}
          >
            <FileText size={15} />
            Project Details
          </button>
        </div>

        {isLoading ? (
          <section className="report-loading-panel" aria-live="polite">
            <div className="report-loading-copy">
              <BarChart3 size={20} />
              <div>
                <strong>Loading report data</strong>
                <span>Preparing customer, renewal, and ledger records.</span>
              </div>
            </div>
            <div className="table-skeleton-list" aria-hidden="true">
              {[0, 1, 2, 3].map(row => (
                <div className="table-skeleton-row" key={row}>
                  <span className="skeleton" />
                  <span className="skeleton" />
                  <span className="skeleton" />
                  <span className="skeleton" />
                </div>
              ))}
            </div>
          </section>
        ) : loadError ? (
          <section className="report-empty-state report-error-state" role="alert">
            <AlertTriangle size={22} />
            <strong>Unable to load reports</strong>
            <span>{loadError}</span>
            <button className="secondary-button" onClick={loadData} type="button">
              <RefreshCw size={14} />
              Retry
            </button>
          </section>
        ) : (
          <>
            {activeTab === "operation" && (
              <section className="report-panel animate-fade-in" role="tabpanel">
                <div className="report-panel-header">
                  <div>
                    <h2>Operations Report</h2>
                    <p>Customer state, business type, active flag, and latest metadata update.</p>
                  </div>
                  <span className="report-count-pill">{formatCount(operationReport.length)} records</span>
                </div>
                <div className="report-analytics-grid">
                  <ReportDistribution
                    title="Status Distribution"
                    items={statusDistribution}
                    emptyText="No customer statuses are available for the operation report."
                  />
                  <section className="report-insight-panel" aria-label="Operation summary">
                    <div className="report-section-heading">
                      <h3>Operational Health</h3>
                      <p>Current active and inactive customer coverage.</p>
                    </div>
                    <div className="report-health-grid">
                      <div>
                        <span>Active</span>
                        <strong>{formatCount(reportSummary.activeCustomers)}</strong>
                      </div>
                      <div>
                        <span>Inactive</span>
                        <strong>{formatCount(reportSummary.inactiveCustomers)}</strong>
                      </div>
                    </div>
                  </section>
                </div>
                <div className="table-wrap report-table-wrap">
                  <table className="corporate-table report-table" aria-label="Customer operations log table">
                    <thead>
                      <tr>
                        <th style={{ width: "64px" }}>No.</th>
                        <th style={{ width: "34%" }}>Customer</th>
                        <th style={{ width: "20%" }}>Business</th>
                        <th style={{ width: "14%" }}>Status</th>
                        <th style={{ width: "16%" }}>Last Update</th>
                        <th style={{ width: "92px" }}>Active</th>
                      </tr>
                    </thead>
                    <tbody>
                      {operationReport.length > 0 ? (
                        operationReport.map((item, index) => (
                          <tr key={item.id}>
                            <td className="numeric-cell">{index + 1}</td>
                            <td>
                              <strong>{item.name}</strong>
                              <span className="subtext">{item.address}</span>
                            </td>
                            <td>{item.business}</td>
                            <td>
                              <span className={`status-badge ${statusClassName(item.status)}`}>{item.status}</span>
                            </td>
                            <td className="date-cell">{item.updatedAt}</td>
                            <td>
                              <span className={`report-state-pill ${item.is_active ? "active" : "inactive"}`}>
                                {item.is_active ? "Yes" : "No"}
                              </span>
                            </td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={6}>
                            <div className="report-empty-state compact">
                              <ClipboardList size={18} />
                              <strong>No operation records</strong>
                              <span>No customers were returned for this report.</span>
                            </div>
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </section>
            )}

            {activeTab === "renewal" && (
              <section className="report-panel animate-fade-in" role="tabpanel">
                <div className="report-panel-header">
                  <div>
                    <h2>Renewal Report</h2>
                    <p>Active customer licenses that require near-term follow-up.</p>
                  </div>
                  <div className="report-filter-toolbar" aria-label="Renewal range filter">
                    <span>Range</span>
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
                </div>
                <div className="report-analytics-grid">
                  <ReportDistribution
                    title="Renewal Risk Windows"
                    items={renewalDistribution}
                    emptyText={`No active renewals are inside the selected ${renewalFilterDays}-day window.`}
                  />
                  <section className="report-insight-panel" aria-label="Renewal summary">
                    <div className="report-section-heading">
                      <h3>Priority Queue</h3>
                      <p>Closest active renewal inside the selected range.</p>
                    </div>
                    {renewalSummary[0] ? (
                      <div className="report-priority-customer">
                        <ShieldCheck size={18} />
                        <div>
                          <strong>{renewalSummary[0].name}</strong>
                          <span>{renewalSummary[0].renewalDays} days remaining</span>
                        </div>
                      </div>
                    ) : (
                      <div className="report-empty-state compact">
                        <Calendar size={18} />
                        <strong>No priority renewal</strong>
                        <span>No active customers expire inside this window.</span>
                      </div>
                    )}
                  </section>
                </div>
                <div className="table-wrap report-table-wrap">
                  <table className="corporate-table report-table" aria-label="Licensing renewal monitor table">
                    <thead>
                      <tr>
                        <th style={{ width: "64px" }}>No.</th>
                        <th style={{ width: "35%" }}>Customer</th>
                        <th style={{ width: "20%" }}>Business Type</th>
                        <th className="numeric-cell" style={{ width: "15%" }}>Days Remaining</th>
                        <th style={{ width: "15%" }}>Estimated Expire</th>
                        <th style={{ width: "10%" }}>Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {renewalSummary.length > 0 ? (
                        renewalSummary.map((item, index) => (
                          <tr key={item.id} className={item.renewalDays <= 15 ? "urgent-row" : ""}>
                            <td className="numeric-cell">{index + 1}</td>
                            <td><strong>{item.name}</strong></td>
                            <td>{item.business}</td>
                            <td className="numeric-cell">
                              <strong className={item.renewalDays <= 15 ? "text-danger-strong" : "text-warning-strong"}>
                                {item.renewalDays} days
                              </strong>
                            </td>
                            <td className="date-cell">{item.expireDate}</td>
                            <td><span className={`status-badge ${statusClassName(item.status)}`}>{item.status}</span></td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={6}>
                            <div className="report-empty-state compact">
                              <Calendar size={18} />
                              <strong>No renewals in range</strong>
                              <span>No clients expire within {renewalFilterDays} days.</span>
                            </div>
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </section>
            )}

            {activeTab === "project-detail" && (
              <section className="report-panel animate-fade-in" role="tabpanel">
                <div className="report-panel-header">
                  <div>
                    <h2>Project Detail Report</h2>
                    <p>Project implementation and license ledger records currently in progress.</p>
                  </div>
                  <span className="report-count-pill">{formatCount(projectDetailSummary.length)} records</span>
                </div>
                <div className="table-wrap report-table-wrap">
                  <table className="corporate-table report-table" aria-label="Implementation ledger table">
                    <thead>
                      <tr>
                        <th style={{ width: "64px" }}>No.</th>
                        <th style={{ width: "25%" }}>Customer Name</th>
                        <th style={{ width: "15%" }}>Contact Person</th>
                        <th style={{ width: "35%" }}>Project / License Description</th>
                        <th style={{ width: "15%" }}>Exp Date</th>
                        <th style={{ width: "10%" }}>Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {projectDetailSummary.length > 0 ? (
                        projectDetailSummary.map((item, index) => (
                          <tr key={index}>
                            <td className="numeric-cell">{index + 1}</td>
                            <td><strong>{item.customerName}</strong></td>
                            <td>{item.contactName}</td>
                            <td>
                              <span className={`record-type-badge ${item.type === "Project" ? "project" : "license"}`}>
                                {item.type}
                              </span>
                              <strong className="report-description-cell">{item.details}</strong>
                            </td>
                            <td className="date-cell">{item.closeDate}</td>
                            <td><span className={`status-badge ${statusClassName(item.status)}`}>{item.status}</span></td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={6}>
                            <div className="report-empty-state compact">
                              <FileText size={18} />
                              <strong>No ledger records</strong>
                              <span>No project detail ledger items found.</span>
                            </div>
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </section>
            )}
          </>
        )}
      </main>
    </div>
  );
};
