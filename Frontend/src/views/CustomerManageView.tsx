import React, { useState, useMemo, useEffect, useRef, useCallback } from "react";
import { 
  Users, UserCheck, ShieldCheck, FileText, Search, Plus, 
  FileDown, Pencil, Trash2, ArrowLeft, Laptop, Target, Check, AlertCircle, Info 
} from "lucide-react";
import { apiService } from "../domain/apiService";
import type { Customer, ContactDetail, DetailDevice, DetailProject, User, Category, Competitor } from "../domain/types";
import { Drawer } from "../components/Drawer";

import { ForbiddenView } from "./ForbiddenView";
import { Pagination } from "../components/Pagination";
import { isAdminRole, isAgentRole, isSupervisorRole, canDeleteCustomer } from "../domain/permissions";
import { 
  customerMatchesQuickFilter, 
  getCustomerMissingFields, 
  hasProductLicenseCompletenessData, 
  type CustomerQuickFilter 
} from "../domain/customerCompleteness";

interface CustomerManageViewProps {
  userRole: string;
  showToast: (msg: string, type: "success" | "error" | "info") => void;
}

type SubView = 
  | { type: "list" }
  | { type: "advance-data"; customer: Customer }
  | { type: "devices"; contact: ContactDetail; customer: Customer }
  | { type: "projects"; contact: ContactDetail; customer: Customer };

const SEARCH_DEBOUNCE_MS = 350;

export const CustomerManageView: React.FC<CustomerManageViewProps> = ({ userRole, showToast }) => {
  const isAdmin = isAdminRole(userRole);
  const isSupervisor = isSupervisorRole(userRole);
  const isAgent = isAgentRole(userRole);

  // Navigation inside Customer Workspace
  const [subView, setSubView] = useState<SubView>({ type: "list" });

  // Live database states
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [businessTypes, setBusinessTypes] = useState<Category[]>([]);
  const [competitors, setCompetitors] = useState<Competitor[]>([]);

  const [contacts, setContacts] = useState<ContactDetail[]>([]);
  const [devices, setDevices] = useState<DetailDevice[]>([]);
  const [projects, setProjects] = useState<DetailProject[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Search/Filters (List View)
  const [draftQuery, setDraftQuery] = useState("");
  const [draftBusinessType, setDraftBusinessType] = useState("");
  const [draftSaleFilter, setDraftSaleFilter] = useState("all");
  const [draftTelesaleFilter, setDraftTelesaleFilter] = useState("all");

  const [appliedQuery, setAppliedQuery] = useState("");
  const [appliedBusinessType, setAppliedBusinessType] = useState("");
  const [appliedSaleFilter, setAppliedSaleFilter] = useState("all");
  const [appliedTelesaleFilter, setAppliedTelesaleFilter] = useState("all");

  // States for completeness and missing-field filtering
  const [completenessFilter, setCompletenessFilter] = useState<"all" | "incomplete" | "complete">("all");
  const [missingFieldFilter, setMissingFieldFilter] = useState<CustomerQuickFilter | "all">("all");
  const [openPopoverCustomerId, setOpenPopoverCustomerId] = useState<number | null>(null);

  // Drawer / Modal triggers
  const [isCustomerDrawerOpen, setIsCustomerDrawerOpen] = useState(false);
  const [activeCustomer, setActiveCustomer] = useState<Customer | null>(null); // null means "Add New"


  // Import flow states
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [importPreviewRows, setImportPreviewRows] = useState<any[]>([]);

  // Contact Drawer triggers
  const [isContactDrawerOpen, setIsContactDrawerOpen] = useState(false);
  const [activeContact, setActiveContact] = useState<ContactDetail | null>(null);

  // Device/Project Drawer triggers
  const [isDeviceDrawerOpen, setIsDeviceDrawerOpen] = useState(false);
  const [activeDevice, setActiveDevice] = useState<DetailDevice | null>(null);
  const [isProjectDrawerOpen, setIsProjectDrawerOpen] = useState(false);
  const [activeProject, setActiveProject] = useState<DetailProject | null>(null);

  const [isForbidden, setIsForbidden] = useState(false);
  const hasLoadedCustomersRef = useRef(false);
  const customerRequestSeq = useRef(0);
  const lastLoadedQueryRef = useRef("");

  // Pagination State
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [backendMetrics, setBackendMetrics] = useState({
    total: 0,
    nearRenewal: 0,
    pendingCostSheets: 0,
    complete: 0,
    incomplete: 0
  });

  const fetchCustomers = useCallback(async (
    currentPage: number,
    currentPageSize: number,
    searchVal: string,
    btFilter: string,
    saleFilter: string,
    teleFilter: string
  ) => {
    const requestId = customerRequestSeq.current + 1;
    customerRequestSeq.current = requestId;

    try {
      setIsLoading(true);
      const res = await apiService.getCustomersPaginated({
        page: currentPage,
        pageSize: currentPageSize,
        search: searchVal,
        businessType: btFilter,
        saleId: saleFilter,
        telesaleId: teleFilter
      });

      if (requestId === customerRequestSeq.current) {
        setCustomers(res.items);
        setTotalCount(res.totalCount);
        setTotalPages(res.totalPages);
        if (res.metrics) {
          setBackendMetrics({
            total: res.metrics.total,

            nearRenewal: res.metrics.nearRenewal,
            pendingCostSheets: res.metrics.pendingCostSheets ?? 0,
            complete: res.metrics.complete ?? 0,
            incomplete: res.metrics.incomplete ?? 0
          });
        }
      }
    } catch (err: any) {
      if (requestId !== customerRequestSeq.current) return;

      if (err.status === 403 || err.message?.includes("403") || err.message?.includes("Forbidden")) {
        setIsForbidden(true);
      } else {
        showToast("Failed to load customer data", "error");
      }
    } finally {
      if (requestId === customerRequestSeq.current) {
        setIsLoading(false);
      }
    }
  }, [showToast]);

  const refreshCustomers = useCallback(() => {
    void fetchCustomers(
      page,
      pageSize,
      appliedQuery,
      appliedBusinessType,
      appliedSaleFilter,
      appliedTelesaleFilter
    );
  }, [fetchCustomers, page, pageSize, appliedQuery, appliedBusinessType, appliedSaleFilter, appliedTelesaleFilter]);

  const loadInitialData = async () => {
    let shouldLoadSupportingData = false;

    try {
      setIsLoading(true);
      setIsForbidden(false);
      const [res, uList] = await Promise.all([
        apiService.getCustomersPaginated({ page: 1, pageSize }),
        Promise.resolve([])
      ]);
      setCustomers(res.items);
      setTotalCount(res.totalCount);
      setTotalPages(res.totalPages);
      if (res.metrics) {
        setBackendMetrics({
          total: res.metrics.total,

          nearRenewal: res.metrics.nearRenewal,
          pendingCostSheets: res.metrics.pendingCostSheets ?? 0,
          complete: res.metrics.complete ?? 0,
          incomplete: res.metrics.incomplete ?? 0
        });
      }
      setUsers(uList);
      hasLoadedCustomersRef.current = true;
      setPage(1);
      lastLoadedQueryRef.current = "";
      shouldLoadSupportingData = true;
    } catch (err: any) {
      if (err.status === 403 || err.message?.includes("403") || err.message?.includes("Forbidden")) {
        setIsForbidden(true);
      } else {
        showToast("Failed to load initial customer data", "error");
      }
    } finally {
      setIsLoading(false);
    }

    if (!shouldLoadSupportingData) return;

    try {
      const [btList, compList] = await Promise.all([
        apiService.getBusinessTypes(),
        apiService.getCompetitors()
      ]);
      setBusinessTypes(btList);
      setCompetitors(compList);
    } catch {
      showToast("Failed to load supporting master data", "info");
    }
  };

  // Close missing fields popover when clicking anywhere else
  useEffect(() => {
    const handleDocumentClick = () => {
      setOpenPopoverCustomerId(null);
    };
    document.addEventListener("click", handleDocumentClick);
    return () => {
      document.removeEventListener("click", handleDocumentClick);
    };
  }, []);

  useEffect(() => {
    if (subView.type === "list") {
      void loadInitialData();
    }
  }, [subView.type]);

  useEffect(() => {
    if (subView.type === "list" && hasLoadedCustomersRef.current) {
      fetchCustomers(
        page,
        pageSize,
        appliedQuery,
        appliedBusinessType,
        appliedSaleFilter,
        appliedTelesaleFilter
      );
    }
  }, [
    page,
    pageSize,
    appliedQuery,
    appliedBusinessType,
    appliedSaleFilter,
    appliedTelesaleFilter,
    subView.type,
    fetchCustomers
  ]);

  // Debounce search in CustomerWorkspace list view
  useEffect(() => {
    if (subView.type !== "list" || !hasLoadedCustomersRef.current) return;

    const timeoutId = window.setTimeout(() => {
      setPage(1);
      setAppliedQuery(draftQuery);
    }, SEARCH_DEBOUNCE_MS);

    return () => window.clearTimeout(timeoutId);
  }, [draftQuery, subView.type]);

  // Metrics
  const metrics = backendMetrics;

  // Filtered customers (with client-side fallback for test/mock environments returning raw arrays)
  const filteredCustomers = useMemo(() => {
    let list = customers;

    // Apply backend-filtering client-side logic only if not already filtered by backend
    const isBackendFiltered = backendMetrics.total > 0 && backendMetrics.total !== customers.length;
    if (!isBackendFiltered) {
      list = list.filter(c => {
        const matchQuery = !appliedQuery || `${c.name} ${c.address}`.toLowerCase().includes(appliedQuery.toLowerCase());
        const matchBus = appliedBusinessType ? c.bt_type === appliedBusinessType : true;
        
        return matchQuery && matchBus;
      });
    }

    // Apply completeness filtering client-side
    if (completenessFilter !== "all") {
      list = list.filter(c => customerMatchesQuickFilter(c, completenessFilter));
    }
    if (missingFieldFilter !== "all") {
      list = list.filter(c => customerMatchesQuickFilter(c, missingFieldFilter));
    }

    return list;
  }, [
    customers, 
    appliedQuery, 
    appliedBusinessType, 
    appliedSaleFilter, 
    appliedTelesaleFilter, 
    backendMetrics, 
    completenessFilter, 
    missingFieldFilter
  ]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    setAppliedQuery(draftQuery);
    setAppliedBusinessType(draftBusinessType);
    setAppliedSaleFilter(draftSaleFilter);
    setAppliedTelesaleFilter(draftTelesaleFilter);
  };

  const handleClearSearch = () => {
    setPage(1);
    setDraftQuery("");
    setDraftBusinessType("");
    setDraftSaleFilter("all");
    setDraftTelesaleFilter("all");
    setAppliedQuery("");
    setAppliedBusinessType("");
    setAppliedSaleFilter("all");
    setAppliedTelesaleFilter("all");
    setCompletenessFilter("all");
    setMissingFieldFilter("all");
  };

  // Helper: Get Username by ID


  // Actions: Customer CRUD
  const handleSaveCustomer = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    const customerData = {
      name: formData.get("name") as string,
      address: formData.get("address") as string,
      capital: formData.get("capital") as string,
      bt_type: formData.get("bt_type") as string,
      start_dt: (formData.get("start_dt") as string) || null,
      status: (formData.get("status") as string) || "New",
      is_active: formData.get("is_active") === "true",
    };

    try {
      if (activeCustomer) {
        // Edit
        await apiService.updateCustomer(activeCustomer.id, customerData);
        showToast(`Updated customer: ${customerData.name}`, "success");
      } else {
        // Add
        await apiService.addCustomer({
          ...customerData,
          telesale_id: null,
          sale_id: null,
          start_dt: customerData.start_dt || new Date().toISOString().split("T")[0]
        });
        showToast(`Added customer: ${customerData.name}`, "success");
      }
      refreshCustomers();
      setIsCustomerDrawerOpen(false);
    } catch (err) {
      showToast("Failed to save customer", "error");
    }
  };

  const handleDeleteCustomer = async (id: number, name: string) => {
    if (window.confirm(`Are you sure you want to delete customer "${name}"?`)) {
      try {
        await apiService.deleteCustomer(id);
        refreshCustomers();
        showToast("Customer deleted successfully", "success");
      } catch (err) {
        showToast("Failed to delete customer", "error");
      }
    }
  };

  const handleToggleCustomerActive = async (c: Customer) => {
    const nextState = !c.is_active;
    try {
      await apiService.updateCustomer(c.id, { is_active: nextState });
      refreshCustomers();
      showToast(`${c.name} ${nextState ? "Enabled" : "Disabled"} successfully`, "success");
    } catch (err) {
      showToast("Operation failed", "error");
    }
  };



  // Actions: Contact CRUD
  const openContactDrawer = (contact: ContactDetail | null) => {
    setActiveContact(contact);
    setIsContactDrawerOpen(true);
  };

  const handleSaveContact = async (e: React.FormEvent<HTMLFormElement>, custId: number) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    const contactData = {
      contact_name: formData.get("contact_name") as string,
      contact_email: formData.get("contact_email") as string,
      contact_tel: formData.get("contact_tel") as string,
      contact_tel_office: formData.get("contact_tel_office") as string,
    };

    try {
      if (activeContact) {
        await apiService.updateContactDetail(activeContact.id, contactData);
        showToast(`Updated contact: ${contactData.contact_name}`, "success");
      } else {
        await apiService.addContactDetail(custId, contactData);
        showToast(`Added contact: ${contactData.contact_name}`, "success");
      }
      const refreshedContacts = await apiService.getContactDetails(custId);
      setContacts(refreshedContacts);
      setIsContactDrawerOpen(false);
    } catch (err) {
      showToast("Failed to save contact", "error");
    }
  };

  const handleDeleteContact = async (contactId: number, custId: number) => {
    if (window.confirm("Are you sure you want to delete this contact?")) {
      try {
        await apiService.deleteContactDetail(contactId);
        const refreshedContacts = await apiService.getContactDetails(custId);
        setContacts(refreshedContacts);
        showToast("Contact deleted successfully", "success");
      } catch (err) {
        showToast("Failed to delete contact", "error");
      }
    }
  };

  // Actions: Devices CRUD
  const openDeviceDrawer = (device: DetailDevice | null) => {
    setActiveDevice(device);
    setIsDeviceDrawerOpen(true);
  };

  const handleSaveDevice = async (e: React.FormEvent<HTMLFormElement>, contactId: number) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    const deviceData = {
      full_name: formData.get("full_name") as string,
      full_name2: formData.get("full_name2") as string,
      dtl: formData.get("dtl") as string,
      desktop_qty: Number(formData.get("desktop_qty")),
      server_qty: Number(formData.get("server_qty")),
      equipment_expire: (formData.get("equipment_expire") as string) || null,
      point: Number(formData.get("point")),
      progress_status: (formData.get("progress_status") as string) || "New",
      competitor_name: formData.get("competitor_name") as string,
    };

    try {
      if (activeDevice) {
        await apiService.updateDevice(activeDevice.id, deviceData);
        showToast("Updated device record", "success");
      } else {
        await apiService.addDevice(contactId, {
          ...deviceData,
          equipment_expire: deviceData.equipment_expire || new Date().toISOString().split("T")[0]
        });
        showToast("Added device record", "success");
      }
      const refreshedDevices = await apiService.getDevices(contactId);
      setDevices(refreshedDevices);
      setIsDeviceDrawerOpen(false);

      // Refresh parent contact points
      if (subView.type === "devices") {
        const refreshedContacts = await apiService.getContactDetails(subView.customer.id);
        setContacts(refreshedContacts);
      }
    } catch (err) {
      showToast("Failed to save device", "error");
    }
  };

  const handleDeleteDevice = async (deviceId: number, contactId: number) => {
    if (window.confirm("Are you sure you want to delete this device record?")) {
      try {
        await apiService.deleteDevice(deviceId);
        const refreshedDevices = await apiService.getDevices(contactId);
        setDevices(refreshedDevices);
        showToast("Device record deleted successfully", "success");
        
        // Refresh parent contact points
        if (subView.type === "devices") {
          const refreshedContacts = await apiService.getContactDetails(subView.customer.id);
          setContacts(refreshedContacts);
        }
      } catch (err) {
        showToast("Failed to delete device record", "error");
      }
    }
  };

  // Actions: Projects CRUD
  const openProjectDrawer = (project: DetailProject | null) => {
    setActiveProject(project);
    setIsProjectDrawerOpen(true);
  };

  const handleSaveProject = async (e: React.FormEvent<HTMLFormElement>, contactId: number) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    const projectData = {
      dtl: formData.get("dtl") as string,
      close_date: (formData.get("close_date") as string) || null,
      point: Number(formData.get("point")),
      progress_status: (formData.get("progress_status") as string) || "New",
    };

    try {
      if (activeProject) {
        await apiService.updateProject(activeProject.id, projectData);
        showToast("Updated project record", "success");
      } else {
        await apiService.addProject(contactId, {
          ...projectData,
          close_date: projectData.close_date || new Date().toISOString().split("T")[0]
        });
        showToast("Added project record", "success");
      }
      const refreshedProjects = await apiService.getProjects(contactId);
      setProjects(refreshedProjects);
      setIsProjectDrawerOpen(false);

      // Refresh parent contact points
      if (subView.type === "projects") {
        const refreshedContacts = await apiService.getContactDetails(subView.customer.id);
        setContacts(refreshedContacts);
      }
    } catch (err) {
      showToast("Failed to save project", "error");
    }
  };

  const handleDeleteProject = async (projectId: number, contactId: number) => {
    if (window.confirm("Are you sure you want to delete this project record?")) {
      try {
        await apiService.deleteProject(projectId);
        const refreshedProjects = await apiService.getProjects(contactId);
        setProjects(refreshedProjects);
        showToast("Project record deleted successfully", "success");
        
        // Refresh parent contact points
        if (subView.type === "projects") {
          const refreshedContacts = await apiService.getContactDetails(subView.customer.id);
          setContacts(refreshedContacts);
        }
      } catch (err) {
        showToast("Failed to delete project record", "error");
      }
    }
  };

  // Import Flow: Mock Excel File Reading
  const handleImportFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      const mockedRows = [
        { name: "บจก. เทคโนโลยี แอดวานซ์", address: "ลาดพร้าว กรุงเทพฯ", capital: "10,000,000", bt_type: "Telco", contact_name: "คุณ สมภพ", contact_email: "somphop@techadv.co.th" },
        { name: "บจก. กรุงเทพ โลจิสติกส์", address: "บางนา กรุงเทพฯ", capital: "5,000,000", bt_type: "Transport Logistics", contact_name: "คุณ พรรณนา", contact_email: "pannana@bkklog.com" },
        { name: "บจก. อาหารดีเจริญ", address: "เมือง นนทบุรี", capital: "2,000,000", bt_type: "Drink", contact_name: "คุณ ชูเกียรติ", contact_email: "chukiart@goodfood.com" }
      ];
      setImportPreviewRows(mockedRows);
    }
  };

  const handleCommitImport = async () => {
    setIsLoading(true);
    try {
      for (const row of importPreviewRows) {
        await apiService.addCustomer({
          name: row.name,
          address: row.address,
          capital: row.capital,
          bt_type: row.bt_type,
          status: "New",
          is_active: true,
          telesale_id: null,
          sale_id: null,
          start_dt: new Date().toISOString().split("T")[0]
        });
      }
      refreshCustomers();
      setImportPreviewRows([]);
      setIsImportModalOpen(false);
      showToast(`Successfully imported ${importPreviewRows.length} customers!`, "success");
    } catch (err) {
      showToast("Import failed", "error");
    } finally {
      setIsLoading(false);
    }
  };

  if (isForbidden) {
    return <ForbiddenView />;
  }

  return (
    <div className="workspace-view">
      {/* SECTION 1: CUSTOMER LISTING VIEW */}
      {subView.type === "list" && (
        <>
          <header className="topbar">
            <div>
              <p>Customer / Manage</p>
              <h1>Customer Workspace</h1>
            </div>
            <div className="topbar-actions">
              {(isAdmin || isSupervisor) && (
                <button className="secondary-button" onClick={() => setIsImportModalOpen(true)} type="button">
                  <FileDown size={15} />
                  Import
                </button>
              )}
              <button className="primary-button" onClick={() => { setActiveCustomer(null); setIsCustomerDrawerOpen(true); }} type="button">
                <Plus size={15} />
                Add Customer
              </button>
            </div>
          </header>

          <main className="content animate-fade-in">
            {/* Metrics */}
            <section className="metrics-grid" aria-label="Customer summary metrics">
              <div className="metric-card blue">
                <div className="metric-icon"><Users size={18} /></div>
                <div>
                  <span>Total Customers</span>
                  <strong>{metrics.total}</strong>
                </div>
              </div>
              <div className="metric-card teal">
                <div className="metric-icon"><Check size={18} /></div>
                <div>
                  <span>Complete Data</span>
                  <strong>{metrics.complete}</strong>
                </div>
              </div>
              <div className="metric-card amber">
                <div className="metric-icon"><AlertCircle size={18} /></div>
                <div>
                  <span>Incomplete Data</span>
                  <strong>{metrics.incomplete}</strong>
                </div>
              </div>
              <div className="metric-card red">
                <div className="metric-icon"><ShieldCheck size={18} /></div>
                <div>
                  <span>Near Renewal (30d)</span>
                  <strong>{metrics.nearRenewal}</strong>
                </div>
              </div>
            </section>

            {/* Filter Panel */}
            <div className="panel filter-panel">
              <form className="filter-band" onSubmit={handleSearchSubmit}>
                <label>
                  <span>Customer name or address</span>
                  <input 
                    type="text" 
                    placeholder="Name, Address" 
                    value={draftQuery}
                    onChange={(e) => setDraftQuery(e.target.value)}
                    aria-label="Customer name or address"
                  />
                </label>
                <label>
                  <span>Business type</span>
                  <select 
                    value={draftBusinessType} 
                    onChange={(e) => setDraftBusinessType(e.target.value)}
                    aria-label="Business type"
                  >
                    <option value="">All business types</option>
                    {businessTypes.map(t => (
                      <option key={t.id} value={t.name}>{t.name}</option>
                    ))}
                  </select>
                </label>

                <div className="filter-actions">
                  <button className="primary-button" type="submit">
                    <Search size={16} />
                    Search
                  </button>
                  <button className="ghost-button" type="button" onClick={handleClearSearch}>
                    Clear
                  </button>
                </div>
              </form>

              <div 
                className="completeness-filter-row" 
                style={{ 
                  display: "flex", 
                  justifyContent: "space-between", 
                  alignItems: "center", 
                  marginTop: "12px", 
                  paddingTop: "12px", 
                  borderTop: "1px solid var(--border)",
                  gap: "16px",
                  flexWrap: "wrap"
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                  <span style={{ fontSize: "11px", fontWeight: "600", textTransform: "uppercase", letterSpacing: "0.5px", color: "var(--text-muted)" }}>
                    Completeness:
                  </span>
                  <div style={{ display: "flex", gap: "6px" }}>
                    {[
                      { id: "all", label: "All" },
                      { id: "incomplete", label: "Incomplete" },
                      { id: "complete", label: "Complete" }
                    ].map(opt => (
                      <button
                        key={opt.id}
                        type="button"
                        className={`filter-pill ${completenessFilter === opt.id ? "active" : ""}`}
                        onClick={() => {
                          setCompletenessFilter(opt.id as any);
                          setPage(1);
                        }}
                        style={{ height: "26px", padding: "0 10px", borderRadius: "13px", fontSize: "11px" }}
                      >
                        {opt.label}
                      </button>
                    ))}
                  </div>
                </div>

                <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                  <span style={{ fontSize: "11px", fontWeight: "600", textTransform: "uppercase", letterSpacing: "0.5px", color: "var(--text-muted)" }}>
                    Missing Field Filter:
                  </span>
                  <select
                    value={missingFieldFilter}
                    onChange={(e) => {
                      setMissingFieldFilter(e.target.value as any);
                      setPage(1);
                    }}
                    aria-label="Filter by missing field"
                    style={{ 
                      height: "30px", 
                      padding: "4px 8px", 
                      borderRadius: "6px", 
                      border: "1px solid var(--border)", 
                      fontSize: "12px", 
                      background: "#ffffff", 
                      color: "var(--primary)",
                      minWidth: "160px"
                    }}
                  >
                    <option value="all">All Fields</option>
                    <option value="noPhone">No Phone</option>
                    <option value="noContact">No Contact</option>
                    <option value="noBusinessType">No Business Type</option>
                    <option value="noAddress">No Address</option>
                    <option value="noEmail">No Email</option>
                    {hasProductLicenseCompletenessData(customers) && (
                      <option value="noProductLicense">No Product / License Info</option>
                    )}
                  </select>
                </div>
              </div>

              {/* Customer Table */}
              <div className="table-wrap">
                <table className="corporate-table" aria-label="Customer records">
                  <thead>
                    <tr>
                      <th style={{ width: "5%" }}>No.</th>
                      <th style={{ width: "32%" }}>Name / Address</th>
                      <th style={{ width: "12%" }}>Start Date</th>
                      <th style={{ width: "10%" }}>Status</th>
                      <th style={{ width: "15%" }}>Business</th>

                      <th style={{ width: "14%" }}>&nbsp;</th>
                    </tr>
                  </thead>
                  <tbody>
                    {isLoading ? (
                      <tr>
                        <td colSpan={6} style={{ textAlign: "center", padding: "32px 0" }}>
                          Loading database customers...
                        </td>
                      </tr>
                    ) : filteredCustomers.length > 0 ? (
                      filteredCustomers.map(c => (
                        <tr key={c.id} className={!c.is_active ? "row-disabled" : ""}>
                          <td>{c.id}</td>
                          <td>
                            <strong>{c.name}</strong>
                            <span className="subtext">{c.address}</span>
                            {c.matchedField && (
                              <span className="subtext">Matched: {c.matchedField}</span>
                            )}
                          </td>
                          <td>{c.start_dt || "-"}</td>
                          <td>
                            <span className={`status-badge ${c.is_active ? c.status.toLowerCase() : "neutral"}`}>
                              {c.is_active ? c.status : "Inactive"}
                            </span>
                          </td>
                          <td>
                            {(!c.is_active && isAdmin) ? (
                              <button 
                                className="danger-action-btn" 
                                onClick={() => handleToggleCustomerActive(c)}
                                type="button"
                              >
                                Enable
                              </button>
                            ) : (
                              c.bt_type
                            )}
                          </td>

                          <td style={{ textAlign: "right" }}>
                            <div className="row-actions">
                              {(() => {
                                const missingFields = getCustomerMissingFields(c);
                                const isComplete = missingFields.length === 0;
                                return isComplete ? (
                                  <span className="status-badge approved" title="ข้อมูลครบถ้วน" style={{ height: "20px", display: "inline-flex", alignItems: "center", gap: "4px", fontSize: "10px", padding: "0 6px" }}>
                                    <Check size={12} style={{ color: "#15803d" }} /> Complete
                                  </span>
                                ) : (
                                  <span className="status-badge wait" title="ข้อมูลไม่ครบถ้วน" style={{ height: "20px", display: "inline-flex", alignItems: "center", gap: "4px", fontSize: "10px", padding: "0 6px" }}>
                                    <AlertCircle size={12} style={{ color: "#b45309" }} /> Incomplete
                                  </span>
                                );
                              })()}

                              <div style={{ position: "relative", display: "inline-flex" }}>
                                <button
                                  type="button"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setOpenPopoverCustomerId(prev => prev === c.id ? null : c.id);
                                  }}
                                  aria-label={`Show missing fields for ${c.name}`}
                                >
                                  <Info size={14} />
                                </button>
                                
                                {openPopoverCustomerId === c.id && (
                                  <div 
                                    className="missing-fields-popover"
                                    onClick={(e) => e.stopPropagation()}
                                    style={{
                                      position: "absolute",
                                      right: 0,
                                      top: "100%",
                                      marginTop: "6px",
                                      zIndex: 100,
                                      width: "200px",
                                      backgroundColor: "#ffffff",
                                      border: "1px solid var(--border)",
                                      borderRadius: "6px",
                                      boxShadow: "0 10px 15px -3px rgba(0,0,0,0.1), 0 4px 6px -2px rgba(0,0,0,0.05)",
                                      padding: "12px",
                                      textAlign: "left",
                                      display: "flex",
                                      flexDirection: "column",
                                      gap: "8px"
                                    }}
                                  >
                                    <h4 style={{ fontSize: "11px", fontWeight: "600", textTransform: "uppercase", letterSpacing: "0.5px", color: "var(--text-muted)", margin: 0 }}>
                                      Missing Fields
                                    </h4>
                                    {(() => {
                                      const missingFields = getCustomerMissingFields(c);
                                      const grouped = [];
                                      if (missingFields.includes("phone")) grouped.push("Phone");
                                      if (missingFields.includes("contact")) grouped.push("Contact");
                                      if (missingFields.includes("businessType")) grouped.push("Business Type");
                                      if (
                                        missingFields.includes("address") ||
                                        missingFields.includes("subdistrict") ||
                                        missingFields.includes("district") ||
                                        missingFields.includes("province") ||
                                        missingFields.includes("postalCode")
                                      ) {
                                        grouped.push("Address");
                                      }
                                      if (missingFields.includes("email")) grouped.push("Email");
                                      if (missingFields.includes("productLicense")) grouped.push("Product / License");

                                      if (grouped.length > 0) {
                                        return (
                                          <div style={{ display: "flex", flexWrap: "wrap", gap: "6px" }}>
                                            {grouped.map((field, idx) => (
                                              <span 
                                                key={idx} 
                                                className="status-badge wait" 
                                                style={{ 
                                                  fontSize: "10px", 
                                                  height: "18px", 
                                                  lineHeight: "16px",
                                                  backgroundColor: "#fffbeb",
                                                  borderColor: "#fcd34d",
                                                  color: "#b45309",
                                                  padding: "0 6px"
                                                }}
                                              >
                                                {field}
                                              </span>
                                            ))}
                                          </div>
                                        );
                                      } else {
                                        return (
                                          <span style={{ fontSize: "11px", color: "#15803d", fontWeight: "600", display: "flex", alignItems: "center", gap: "4px" }}>
                                            <Check size={12} /> ข้อมูลครบถ้วน
                                          </span>
                                        );
                                      }
                                    })()}
                                  </div>
                                )}
                              </div>

                              {(c.status === "Assigned" || c.status === "REPLACE" || c.status === "Booking" || c.status === "Win" || c.status === "Sent" || c.status === "Wait") && (
                                <button
                                  className="action-pill blue"
                                  onClick={async () => {
                                    setIsLoading(true);
                                    try {
                                      const fetchedContacts = await apiService.getContactDetails(c.id);
                                      setContacts(fetchedContacts);
                                      setSubView({ type: "advance-data", customer: c });
                                    } catch (err) {
                                      showToast("Failed to fetch contact details", "error");
                                    } finally {
                                      setIsLoading(false);
                                    }
                                  }}
                                  aria-label={`Open advance data for ${c.name}`}
                                  type="button"
                                >
                                  Advance
                                </button>
                              )}
                              <button 
                                onClick={() => { setActiveCustomer(c); setIsCustomerDrawerOpen(true); }}
                                aria-label={`Edit ${c.name}`}
                                type="button"
                              >
                                <Pencil size={14} />
                              </button>
                              {canDeleteCustomer(userRole) && (
                                <button 
                                  className="delete-btn"
                                  onClick={() => handleDeleteCustomer(c.id, c.name)}
                                  aria-label={`Delete ${c.name}`}
                                  type="button"
                                >
                                  <Trash2 size={14} />
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td colSpan={6} style={{ textAlign: "center", padding: "32px 0" }}>
                          No customers found.
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
        </>
      )}

      {/* SECTION 2: CUSTOMER ADVANCE DATA (CONTACT PERSONS) VIEW */}
      {subView.type === "advance-data" && (
        <>
          <header className="topbar">
            <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
              <button className="icon-back-btn" onClick={() => setSubView({ type: "list" })} aria-label="Back to customers" type="button">
                <ArrowLeft size={18} />
              </button>
              <div>
                <p>Customer / Manage / Advance Data</p>
                <h1>{subView.customer.name}</h1>
              </div>
            </div>
            <button className="primary-button" onClick={() => openContactDrawer(null)} type="button">
              <Plus size={15} />
              Add Contact Person
            </button>
          </header>

          <main className="content animate-fade-in">
            {/* Customer mini card */}
            <div className="panel customer-detail-card">
              <h3>Customer Information</h3>
              <div className="detail-grid">
                <div><span>Address:</span> {subView.customer.address}</div>
                <div><span>Business Type:</span> {subView.customer.bt_type}</div>
                <div><span>Capital:</span> {subView.customer.capital || "-"} THB</div>
                <div><span>Status:</span> <span className={`status-badge ${subView.customer.status.toLowerCase()}`}>{subView.customer.status}</span></div>
              </div>
            </div>

            <div className="panel">
              <div className="panel-header">
                <h2>Contact Persons List</h2>
              </div>
              <div className="table-wrap">
                <table className="corporate-table" aria-label="Contacts list table">
                  <thead>
                    <tr>
                      <th style={{ width: "5%" }}>No.</th>
                      <th style={{ width: "25%" }}>Name</th>
                      <th style={{ width: "25%" }}>Email</th>
                      <th style={{ width: "20%" }}>Tel / Tel Office</th>

                      <th style={{ width: "15%" }}>&nbsp;</th>
                    </tr>
                  </thead>
                  <tbody>
                    {contacts.length > 0 ? (
                      contacts.map((item, index) => (
                        <tr key={item.id}>
                          <td>{index + 1}</td>
                          <td><strong>{item.contact_name}</strong></td>
                          <td>{item.contact_email}</td>
                          <td>{item.contact_tel} {item.contact_tel_office ? ` / ${item.contact_tel_office}` : ""}</td>

                          <td style={{ textAlign: "right" }}>
                            <div className="row-actions">
                              <button
                                className="action-pill blue"
                                onClick={async () => {
                                  setIsLoading(true);
                                  try {
                                    const devList = await apiService.getDevices(item.id);
                                    setDevices(devList);
                                    setSubView({ type: "devices", contact: item, customer: subView.customer });
                                  } catch (err) {
                                    showToast("Failed to fetch devices", "error");
                                  } finally {
                                    setIsLoading(false);
                                  }
                                }}
                                type="button"
                              >
                                <Laptop size={12} aria-hidden="true" />
                                Device
                              </button>
                              <button
                                className="action-pill green"
                                onClick={async () => {
                                  setIsLoading(true);
                                  try {
                                    const projList = await apiService.getProjects(item.id);
                                    setProjects(projList);
                                    setSubView({ type: "projects", contact: item, customer: subView.customer });
                                  } catch (err) {
                                    showToast("Failed to fetch projects", "error");
                                  } finally {
                                    setIsLoading(false);
                                  }
                                }}
                                type="button"
                              >
                                <Target size={12} aria-hidden="true" />
                                Project
                              </button>
                              <button onClick={() => openContactDrawer(item)} aria-label="Edit contact" type="button">
                                <Pencil size={13} />
                              </button>
                              {userRole !== "Viewer" && (
                                <button className="delete-btn" onClick={() => handleDeleteContact(item.id, subView.customer.id)} aria-label="Delete contact" type="button">
                                  <Trash2 size={13} />
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td colSpan={5} style={{ textAlign: "center", padding: "24px 0" }}>
                          No contact persons added yet.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </main>
        </>
      )}

      {/* SECTION 3: DEVICES WORKSPACE */}
      {subView.type === "devices" && (
        <>
          <header className="topbar">
            <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
              <button 
                className="icon-back-btn" 
                onClick={async () => {
                  setIsLoading(true);
                  try {
                    const refreshedContacts = await apiService.getContactDetails(subView.customer.id);
                    setContacts(refreshedContacts);
                    setSubView({ type: "advance-data", customer: subView.customer });
                  } catch (err) {
                    showToast("Failed to refresh contacts list", "error");
                  } finally {
                    setIsLoading(false);
                  }
                }} 
                aria-label="Back to contact persons"
                type="button"
              >
                <ArrowLeft size={18} />
              </button>
              <div>
                <p>Customer / Manage / Advance / Devices</p>
                <h1>Devices of {subView.contact.contact_name}</h1>
              </div>
            </div>
            <button className="primary-button" onClick={() => openDeviceDrawer(null)} type="button">
              <Plus size={15} />
              Add Device
            </button>
          </header>

          <main className="content animate-fade-in">
            <div className="panel">
              <div className="table-wrap">
                <table className="corporate-table" aria-label="Device statistics table">
                  <thead>
                    <tr>
                      <th style={{ width: "5%" }}>No.</th>
                      <th style={{ width: "25%" }}>Device / Software</th>
                      <th style={{ width: "12%" }}>Desktop Qty</th>
                      <th style={{ width: "12%" }}>Server Qty</th>
                      <th style={{ width: "15%" }}>Expire Date</th>

                      <th style={{ width: "10%" }}>Status</th>
                      <th style={{ width: "13%" }}>Competitor</th>
                      <th style={{ width: "10%" }}>&nbsp;</th>
                    </tr>
                  </thead>
                  <tbody>
                    {devices.length > 0 ? (
                      devices.map(item => (
                        <tr key={item.id}>
                          <td>{item.id}</td>
                          <td>
                            <strong>{item.full_name} {item.full_name2 || ""}</strong>
                            <span className="subtext">{item.dtl || "-"}</span>
                          </td>
                          <td>{item.desktop_qty}</td>
                          <td>{item.server_qty}</td>
                          <td>{item.equipment_expire || "-"}</td>

                          <td>
                            <span className={`status-badge ${item.progress_status.toLowerCase()}`}>
                              {item.progress_status}
                            </span>
                          </td>
                          <td>{item.competitor_name || "-"}</td>
                          <td style={{ textAlign: "right" }}>
                            {item.progress_status !== "Win" && (
                              <div className="row-actions">
                                <button onClick={() => openDeviceDrawer(item)} aria-label="Edit device" type="button">
                                  <Pencil size={13} />
                                </button>
                                {userRole !== "Viewer" && (
                                  <button className="delete-btn" onClick={() => handleDeleteDevice(item.id, subView.contact.id)} aria-label="Delete device" type="button">
                                    <Trash2 size={13} />
                                  </button>
                                )}
                              </div>
                            )}
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td colSpan={8} style={{ textAlign: "center", padding: "24px 0" }}>
                          No device records found.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </main>
        </>
      )}

      {/* SECTION 4: PROJECTS WORKSPACE */}
      {subView.type === "projects" && (
        <>
          <header className="topbar">
            <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
              <button 
                className="icon-back-btn" 
                onClick={async () => {
                  setIsLoading(true);
                  try {
                    const refreshedContacts = await apiService.getContactDetails(subView.customer.id);
                    setContacts(refreshedContacts);
                    setSubView({ type: "advance-data", customer: subView.customer });
                  } catch (err) {
                    showToast("Failed to refresh contacts list", "error");
                  } finally {
                    setIsLoading(false);
                  }
                }} 
                aria-label="Back to contact persons"
                type="button"
              >
                <ArrowLeft size={18} />
              </button>
              <div>
                <p>Customer / Manage / Advance / Projects</p>
                <h1>Projects of {subView.contact.contact_name}</h1>
              </div>
            </div>
            <button className="primary-button" onClick={() => openProjectDrawer(null)} type="button">
              <Plus size={15} />
              Add Project
            </button>
          </header>

          <main className="content animate-fade-in">
            <div className="panel">
              <div className="table-wrap">
                <table className="corporate-table" aria-label="Project details table">
                  <thead>
                    <tr>
                      <th style={{ width: "5%" }}>No.</th>
                      <th style={{ width: "45%" }}>Project Detail</th>
                      <th style={{ width: "15%" }}>Close Date</th>

                      <th style={{ width: "15%" }}>Status</th>
                      <th style={{ width: "10%" }}>&nbsp;</th>
                    </tr>
                  </thead>
                  <tbody>
                    {projects.length > 0 ? (
                      projects.map(item => (
                        <tr key={item.id}>
                          <td>{item.id}</td>
                          <td><strong>{item.dtl}</strong></td>
                          <td>{item.close_date || "-"}</td>

                          <td>
                            <span className={`status-badge ${item.progress_status.toLowerCase()}`}>
                              {item.progress_status}
                            </span>
                          </td>
                          <td style={{ textAlign: "right" }}>
                            <div className="row-actions">
                              <button onClick={() => openProjectDrawer(item)} aria-label="Edit project" type="button">
                                <Pencil size={13} />
                              </button>
                               {userRole !== "Viewer" && (
                                <button className="delete-btn" onClick={() => handleDeleteProject(item.id, subView.contact.id)} aria-label="Delete project" type="button">
                                  <Trash2 size={13} />
                                </button>
                               )}
                            </div>
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td colSpan={5} style={{ textAlign: "center", padding: "24px 0" }}>
                          No project records found.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </main>
        </>
      )}

      {/* DRAWERS & MODALS */}

      {/* 1. Customer Add/Edit Drawer */}
      <Drawer
        isOpen={isCustomerDrawerOpen}
        title={activeCustomer ? `Edit Customer: ${activeCustomer.name}` : "Add New Customer"}
        onClose={() => setIsCustomerDrawerOpen(false)}
      >
        <form onSubmit={handleSaveCustomer} className="corporate-form">
          <div className="form-group">
            <label htmlFor="name">Customer Name *</label>
            <input 
              id="name"
              name="name" 
              type="text" 
              defaultValue={activeCustomer?.name || ""} 
              required 
            />
          </div>
          <div className="form-group">
            <label htmlFor="address">Address *</label>
            <textarea 
              id="address"
              name="address" 
              rows={3} 
              defaultValue={activeCustomer?.address || ""} 
              required 
            />
          </div>
          <div className="form-group">
            <label htmlFor="capital">Capital (THB)</label>
            <input 
              id="capital"
              name="capital" 
              type="text" 
              defaultValue={activeCustomer?.capital || ""} 
              placeholder="e.g. 5,000,000" 
            />
          </div>

          <fieldset className="form-section">
            <legend>Metadata & Status</legend>
            <div className="form-group">
              <label htmlFor="bt_type">Business Type *</label>
              <select 
                id="bt_type"
                name="bt_type" 
                defaultValue={activeCustomer?.bt_type || (businessTypes[0]?.name || "")} 
                required
              >
                {businessTypes.map(t => (
                  <option key={t.id} value={t.name}>{t.name}</option>
                ))}
              </select>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label htmlFor="start_dt">Start Date</label>
                <input 
                  id="start_dt"
                  name="start_dt" 
                  type="date" 
                  defaultValue={activeCustomer?.start_dt || ""} 
                />
              </div>
              <div className="form-group">
                <label htmlFor="status">Status</label>
                <select 
                  id="status"
                  name="status" 
                  defaultValue={activeCustomer?.status || "New"}
                >
                  <option value="New">New</option>
                  <option value="Assigned">Assigned</option>
                  <option value="Booking">Booking</option>
                  <option value="Wait">Wait</option>
                  <option value="Sent">Sent</option>
                  <option value="Win">Win</option>
                  <option value="Lost">Lost</option>
                  <option value="REPLACE">REPLACE</option>
                </select>
              </div>
            </div>
            <div className="form-group checkbox-group">
              <label className="checkbox-label" htmlFor="is_active">
                <input 
                  id="is_active"
                  name="is_active" 
                  type="checkbox" 
                  value="true" 
                  defaultChecked={activeCustomer ? activeCustomer.is_active : true} 
                />
                <span>Active Customer (Enable)</span>
              </label>
            </div>
          </fieldset>

          <footer className="form-actions">
            <button className="ghost-button" type="button" onClick={() => setIsCustomerDrawerOpen(false)}>Cancel</button>
            <button className="primary-button" type="submit">Save Customer</button>
          </footer>
        </form>
      </Drawer>



      {/* 3. Excel Import Mock Modal */}
      {isImportModalOpen && (
        <Drawer
          isOpen={isImportModalOpen}
          title="Import Customers from Excel"
          onClose={() => setIsImportModalOpen(false)}
        >
          <div className="import-modal-container">
            <div className="file-drop-area">
              <AlertCircle size={24} className="text-info" />
              <p>Select customer datasheet file (.xls, .xlsx) to simulate parsing.</p>
              <input
                type="file"
                accept=".xls,.xlsx"
                onChange={handleImportFileChange}
                aria-label="Upload excel file"
              />
            </div>

            {importPreviewRows.length > 0 && (
              <div className="import-preview-box">
                <h4>Preview parsed data ({importPreviewRows.length} rows)</h4>
                <div className="table-wrap table-scroll-sm">
                  <table className="corporate-table compact" aria-label="Excel parsed row preview table">
                    <thead>
                      <tr>
                        <th>Company Name</th>
                        <th>Address</th>
                        <th>Business Type</th>
                      </tr>
                    </thead>
                    <tbody>
                      {importPreviewRows.map((r, i) => (
                        <tr key={i}>
                          <td>{r.name}</td>
                          <td>{r.address}</td>
                          <td>{r.bt_type}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div className="action-row" style={{ marginTop: "16px" }}>
                  <button className="ghost-button" onClick={() => setImportPreviewRows([])} type="button">Reset</button>
                  <button className="primary-button" onClick={handleCommitImport} type="button">
                    <Check size={14} aria-hidden="true" />
                    Commit Import
                  </button>
                </div>
              </div>
            )}
          </div>
        </Drawer>
      )}

      {/* 4. Contact Person Add/Edit Drawer */}
      {subView.type === "advance-data" && (
        <Drawer
          isOpen={isContactDrawerOpen}
          title={activeContact ? `Edit Contact: ${activeContact.contact_name}` : "Add Contact Person"}
          onClose={() => setIsContactDrawerOpen(false)}
        >
          <form onSubmit={(e) => handleSaveContact(e, subView.customer.id)} className="corporate-form">
            <div className="form-group">
              <label htmlFor="contact_name_person">Name *</label>
              <input 
                id="contact_name_person"
                name="contact_name" 
                type="text" 
                defaultValue={activeContact?.contact_name || ""} 
                required 
              />
            </div>
            <div className="form-group">
              <label htmlFor="contact_email_person">Email *</label>
              <input 
                id="contact_email_person"
                name="contact_email" 
                type="email" 
                defaultValue={activeContact?.contact_email || ""} 
                required 
              />
            </div>
            <div className="form-row">
              <div className="form-group">
                <label htmlFor="contact_tel_person">Mobile Tel *</label>
                <input 
                  id="contact_tel_person"
                  name="contact_tel" 
                  type="text" 
                  defaultValue={activeContact?.contact_tel || ""} 
                  required 
                />
              </div>
              <div className="form-group">
                <label htmlFor="contact_tel_office_person">Office Tel</label>
                <input 
                  id="contact_tel_office_person"
                  name="contact_tel_office" 
                  type="text" 
                  defaultValue={activeContact?.contact_tel_office || ""} 
                />
              </div>
            </div>
            <footer className="form-actions">
              <button className="ghost-button" type="button" onClick={() => setIsContactDrawerOpen(false)}>Cancel</button>
              <button className="primary-button" type="submit">Save Contact</button>
            </footer>
          </form>
        </Drawer>
      )}

      {/* 5. Device Add/Edit Drawer */}
      {subView.type === "devices" && (
        <Drawer
          isOpen={isDeviceDrawerOpen}
          title={activeDevice ? "Edit Device Record" : "Add Device Record"}
          onClose={() => setIsDeviceDrawerOpen(false)}
        >
          <form onSubmit={(e) => handleSaveDevice(e, subView.contact.id)} className="corporate-form">
            <div className="form-group">
              <label htmlFor="full_name_dev">Software Name *</label>
              <input 
                id="full_name_dev"
                name="full_name" 
                type="text" 
                defaultValue={activeDevice?.full_name || ""} 
                placeholder="e.g. Kaspersky Security" 
                required 
              />
            </div>
            <div className="form-group">
              <label htmlFor="dtl_dev">Description</label>
              <textarea 
                id="dtl_dev"
                name="dtl" 
                rows={2} 
                defaultValue={activeDevice?.dtl || ""} 
              />
            </div>
            <div className="form-row">
              <div className="form-group">
                <label htmlFor="desktop_qty">Desktop Qty *</label>
                <input 
                  id="desktop_qty"
                  name="desktop_qty" 
                  type="number" 
                  defaultValue={activeDevice ? activeDevice.desktop_qty : 0} 
                  required 
                />
              </div>
              <div className="form-group">
                <label htmlFor="server_qty">Server Qty *</label>
                <input 
                  id="server_qty"
                  name="server_qty" 
                  type="number" 
                  defaultValue={activeDevice ? activeDevice.server_qty : 0} 
                  required 
                />
              </div>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label htmlFor="equipment_expire">Expire Date</label>
                <input 
                  id="equipment_expire"
                  name="equipment_expire" 
                  type="date" 
                  defaultValue={activeDevice?.equipment_expire || ""} 
                />
              </div>
              <input type="hidden" name="point" value="0" />
            </div>
            <div className="form-row">
              <div className="form-group">
                <label htmlFor="progress_status_dev">Status</label>
                <select 
                  id="progress_status_dev"
                  name="progress_status" 
                  defaultValue={activeDevice?.progress_status || "New"}
                >
                  <option value="New">New</option>
                  <option value="Booking">Booking</option>
                  <option value="Win">Win</option>
                  <option value="Lost">Lost</option>
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="competitor_name">Competitor</label>
                <select 
                  id="competitor_name"
                  name="competitor_name" 
                  defaultValue={activeDevice?.competitor_name || ""}
                >
                  <option value="">None</option>
                  {competitors.map(c => (
                    <option key={c.id} value={c.name}>{c.name}</option>
                  ))}
                </select>
              </div>
            </div>
            <footer className="form-actions">
              <button className="ghost-button" type="button" onClick={() => setIsDeviceDrawerOpen(false)}>Cancel</button>
              <button className="primary-button" type="submit">Save Device</button>
            </footer>
          </form>
        </Drawer>
      )}

      {/* 6. Project Add/Edit Drawer */}
      {subView.type === "projects" && (
        <Drawer
          isOpen={isProjectDrawerOpen}
          title={activeProject ? "Edit Project Record" : "Add Project Record"}
          onClose={() => setIsProjectDrawerOpen(false)}
        >
          <form onSubmit={(e) => handleSaveProject(e, subView.contact.id)} className="corporate-form">
            <div className="form-group">
              <label htmlFor="dtl_proj">Project Detail / Name *</label>
              <textarea 
                id="dtl_proj"
                name="dtl" 
                rows={3} 
                defaultValue={activeProject?.dtl || ""} 
                placeholder="e.g. Bitdefender Cloud Antivirus implementation" 
                required 
              />
            </div>
            <div className="form-row">
              <div className="form-group">
                <label htmlFor="close_date">Expected Close Date</label>
                <input 
                  id="close_date"
                  name="close_date" 
                  type="date" 
                  defaultValue={activeProject?.close_date || ""} 
                />
              </div>
              <input type="hidden" name="point" value="0" />
            </div>
            <div className="form-group">
              <label htmlFor="progress_status_proj">Status</label>
              <select 
                id="progress_status_proj"
                name="progress_status" 
                defaultValue={activeProject?.progress_status || "New"}
              >
                <option value="New">New</option>
                <option value="Booking">Booking</option>
                <option value="Win">Win</option>
                <option value="Lost">Lost</option>
              </select>
            </div>
            <footer className="form-actions">
              <button className="ghost-button" type="button" onClick={() => setIsProjectDrawerOpen(false)}>Cancel</button>
              <button className="primary-button" type="submit">Save Project</button>
            </footer>
          </form>
        </Drawer>
      )}
    </div>
  );
};
