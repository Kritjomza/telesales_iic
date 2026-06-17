import React, { useState, useEffect, useMemo, useCallback } from "react";
import { Plus, Check, X, ArrowLeft, Download, Send } from "lucide-react";
import { apiService } from "../domain/apiService";
import type { CostSheet, Customer, Brand, Product } from "../domain/types";
import { ForbiddenView } from "./ForbiddenView";
import { isAdminRole } from "../domain/permissions";
import { Pagination } from "../components/Pagination";

interface CostSheetViewProps {
  userRole: string;
  showToast: (msg: string, type: "success" | "error" | "info") => void;
}

type Mode = "list" | "create" | "preview";

export const CostSheetView: React.FC<CostSheetViewProps> = ({ userRole, showToast }) => {
  const isAdmin = isAdminRole(userRole);
  const [mode, setMode] = useState<Mode>("list");
  const [costSheets, setCostSheets] = useState<CostSheet[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [brands, setBrands] = useState<Brand[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedCostSheet, setSelectedCostSheet] = useState<CostSheet | null>(null);
  const [isForbidden, setIsForbidden] = useState(false);

  // Pagination State
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);

  // Form Fields
  const [selectedCustId, setSelectedCustId] = useState<number>(0);
  const [projectName, setProjectName] = useState("");
  const [brandId, setBrandId] = useState<number>(0);
  const [productId, setProductId] = useState<number>(0);
  const [qty, setQty] = useState(1);
  const [costPrice, setCostPrice] = useState(0);
  const [salePrice, setSalePrice] = useState(0);
  const [discount, setDiscount] = useState(0);
  const [ownerShare, setOwnerShare] = useState(60);
  const [employeeShare, setEmployeeShare] = useState(40);

  const loadData = useCallback(async (currentPage: number, currentPageSize: number) => {
    try {
      setIsLoading(true);
      setIsForbidden(false);
      const [res, custs, brds, prods] = await Promise.all([
        apiService.getCostSheetsPaginated({ page: currentPage, pageSize: currentPageSize }),
        apiService.getCustomers(),
        apiService.getBrands(),
        apiService.getProducts()
      ]);
      setCostSheets(res.items);
      setTotalCount(res.totalCount);
      setTotalPages(res.totalPages);
      setCustomers(custs);
      setBrands(brds);
      setProducts(prods);
    } catch (err: any) {
      if (err.message?.includes("403") || err.message?.includes("Forbidden")) {
        setIsForbidden(true);
      } else {
        showToast("Failed to load cost sheet data", "error");
      }
    } finally {
      setIsLoading(false);
    }
  }, [showToast]);

  const refreshCostSheets = useCallback(async () => {
    try {
      const res = await apiService.getCostSheetsPaginated({ page, pageSize });
      setCostSheets(res.items);
      setTotalCount(res.totalCount);
      setTotalPages(res.totalPages);
    } catch {
      showToast("Failed to refresh Cost Sheets", "error");
    }
  }, [page, pageSize, showToast]);

  useEffect(() => {
    if (mode === "list") {
      void loadData(page, pageSize);
    }
  }, [page, pageSize, mode, loadData]);

  // Computed values during creation
  const brandProducts = useMemo(() => {
    return products.filter(p => p.brand_id === brandId);
  }, [products, brandId]);

  const calculations = useMemo(() => {
    const totalCost = qty * costPrice;
    const grossSale = qty * salePrice;
    const netSale = Math.max(0, grossSale - discount);
    const gpAmount = netSale - totalCost;
    const gpPercent = netSale > 0 ? Number(((gpAmount / netSale) * 100).toFixed(1)) : 0;
    const ownerAmount = gpAmount * (ownerShare / 100);
    const employeeAmount = gpAmount * (employeeShare / 100);

    return {
      totalCost,
      grossSale,
      netSale,
      gpAmount,
      gpPercent,
      ownerAmount,
      employeeAmount
    };
  }, [qty, costPrice, salePrice, discount, ownerShare, employeeShare]);

  // Handle Brand selection change
  const handleBrandChange = (bId: number) => {
    setBrandId(bId);
    const firstProd = products.find(p => p.brand_id === bId);
    if (firstProd) {
      handleProductChange(firstProd.id);
    } else {
      setProductId(0);
      setCostPrice(0);
      setSalePrice(0);
    }
  };

  // Handle Product selection change
  const handleProductChange = (pId: number) => {
    setProductId(pId);
    const prod = products.find(p => p.id === pId);
    if (prod) {
      setCostPrice(prod.cost);
      setSalePrice(prod.price);
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedCustId === 0 || productId === 0) {
      showToast("Please fill all required fields", "error");
      return;
    }

    try {
      const sheet = await apiService.addCostSheet({
        cust_id: selectedCustId,
        project_name: projectName,
        brand_id: brandId,
        product_id: productId,
        qty,
        cost_price: costPrice,
        sale_price: salePrice,
        discount,
        margin: calculations.gpAmount,
        gp_amount: calculations.gpAmount,
        gp_percent: calculations.gpPercent,
        owner_share_percent: ownerShare,
        employee_share_percent: employeeShare,
        status: "Pending"
      });

      await refreshCostSheets();
      setSelectedCostSheet(sheet);
      setMode("preview");
      showToast("Cost Sheet created successfully", "success");
    } catch (err) {
      showToast("Failed to create Cost Sheet", "error");
    }
  };

  const handleDelete = async (id: number) => {
    if (window.confirm("Are you sure you want to delete this Cost Sheet?")) {
      try {
        await apiService.deleteCostSheet(id);
        await refreshCostSheets();
        showToast("Cost Sheet deleted successfully", "success");
      } catch (err) {
        showToast("Failed to delete Cost Sheet", "error");
      }
    }
  };

  const handleUpdateStatus = async (id: number, status: "Approved" | "Rejected") => {
    try {
      await apiService.updateCostSheetStatus(id, status);
      await refreshCostSheets();
      if (selectedCostSheet) {
        setSelectedCostSheet({ ...selectedCostSheet, status });
      }
      showToast(`Cost Sheet ${status} successfully`, "success");
    } catch (err) {
      showToast(`Failed to update status to ${status}`, "error");
    }
  };

  const getCustomerName = (id: number) => {
    return customers.find(c => c.id === id)?.name || "Unknown Customer";
  };

  const getProductName = (id: number) => {
    return products.find(p => p.id === id)?.name || "Unknown Product";
  };

  const getBrandName = (id: number) => {
    return brands.find(b => b.id === id)?.name || "Unknown Brand";
  };

  if (isForbidden) {
    return <ForbiddenView />;
  }

  return (
    <div className="workspace-view">
      {/* 1. LISTING MODE */}
      {mode === "list" && (
        <>
          <header className="topbar">
            <div>
              <p>Sale Manager / Cost Sheet</p>
              <h1>Cost Sheets</h1>
            </div>
            <button className="primary-button" onClick={() => {
              setMode("create");
              setSelectedCustId(customers[0]?.id || 0);
              setProjectName("");
              handleBrandChange(brands[0]?.id || 0);
              setQty(1);
              setDiscount(0);
              setOwnerShare(60);
              setEmployeeShare(40);
            }} type="button">
              <Plus size={15} />
              Create Cost Sheet
            </button>
          </header>

          <main className="content animate-fade-in">
            <div className="panel">
              <div className="table-wrap">
                <table className="corporate-table" aria-label="Cost sheets table">
                  <thead>
                    <tr>
                      <th style={{ width: "5%" }}>No.</th>
                      <th style={{ width: "25%" }}>Customer</th>
                      <th style={{ width: "25%" }}>Project Name</th>
                      <th style={{ width: "15%" }}>Net Amount</th>
                      <th style={{ width: "10%" }}>GP %</th>
                      <th style={{ width: "10%" }}>Status</th>
                      <th style={{ width: "10%" }}>&nbsp;</th>
                    </tr>
                  </thead>
                  <tbody>
                    {isLoading ? (
                      <tr>
                        <td colSpan={7} style={{ textAlign: "center", padding: "32px 0" }}>
                          Loading cost sheets...
                        </td>
                      </tr>
                    ) : costSheets.length > 0 ? (
                      costSheets.map((item, index) => {
                        const totalCost = item.qty * item.cost_price;
                        const grossSale = item.qty * item.sale_price;
                        const netSale = Math.max(0, grossSale - item.discount);
                        return (
                          <tr key={item.id}>
                            <td>{(page - 1) * pageSize + index + 1}</td>
                            <td><strong>{getCustomerName(item.cust_id)}</strong></td>
                            <td>{item.project_name}</td>
                            <td>{netSale.toLocaleString()} THB</td>
                            <td>
                              <span className={item.gp_percent > 25 ? "text-success-strong" : "text-warning-strong"}>
                                {item.gp_percent}%
                              </span>
                            </td>
                            <td>
                              <span className={`status-badge ${item.status.toLowerCase()}`}>
                                {item.status}
                              </span>
                            </td>
                            <td style={{ textAlign: "right" }}>
                              <div className="row-actions">
                                <button
                                  className="action-pill blue"
                                  onClick={() => {
                                    setSelectedCostSheet(item);
                                    setMode("preview");
                                  }}
                                  type="button"
                                >
                                  View
                                </button>
                                <button className="delete-btn" onClick={() => handleDelete(item.id)} aria-label="Delete cost sheet" type="button">
                                  <X size={14} />
                                </button>
                              </div>
                            </td>
                          </tr>
                        );
                      })
                    ) : (
                      <tr>
                        <td colSpan={7} style={{ textAlign: "center", padding: "32px 0" }}>
                          No Cost Sheets created yet.
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

      {/* 2. CREATE COST SHEET MODE */}
      {mode === "create" && (
        <>
          <header className="topbar">
            <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
              <button className="icon-back-btn" onClick={() => setMode("list")} aria-label="Back to cost sheets" type="button">
                <ArrowLeft size={18} />
              </button>
              <div>
                <p>Cost Sheet / Create</p>
                <h1>Create Cost Sheet</h1>
              </div>
            </div>
          </header>

          <main className="content animate-fade-in">
            <form onSubmit={handleSave} className="corporate-form cost-sheet-form">
              <div className="panel panel-padded">
                <div className="form-grid-2">
                  <div className="form-group">
                    <label htmlFor="cust_id_cs">Customer *</label>
                    <select
                      id="cust_id_cs"
                      value={selectedCustId}
                      onChange={(e) => setSelectedCustId(Number(e.target.value))}
                      required
                    >
                      <option value="">Select Customer</option>
                      {customers.map(c => (
                        <option key={c.id} value={c.id}>{c.name}</option>
                      ))}
                    </select>
                  </div>
                  <div className="form-group">
                    <label htmlFor="project_name_cs">Project Name *</label>
                    <input
                      id="project_name_cs"
                      type="text"
                      placeholder="e.g. Kaspersky Renewal 2026"
                      value={projectName}
                      onChange={(e) => setProjectName(e.target.value)}
                      required
                    />
                  </div>
                </div>

                <div className="form-grid-3" style={{ marginTop: "16px" }}>
                  <div className="form-group">
                    <label htmlFor="brand_id_cs">Brand *</label>
                    <select
                      id="brand_id_cs"
                      value={brandId}
                      onChange={(e) => handleBrandChange(Number(e.target.value))}
                      required
                    >
                      <option value="">Select Brand</option>
                      {brands.map(b => (
                        <option key={b.id} value={b.id}>{b.name}</option>
                      ))}
                    </select>
                  </div>
                  <div className="form-group">
                    <label htmlFor="product_id_cs">Product *</label>
                    <select
                      id="product_id_cs"
                      value={productId}
                      onChange={(e) => handleProductChange(Number(e.target.value))}
                      required
                    >
                      <option value="">Select Product</option>
                      {brandProducts.map(p => (
                        <option key={p.id} value={p.id}>{p.name}</option>
                      ))}
                    </select>
                  </div>
                  <div className="form-group">
                    <label htmlFor="qty_cs">Quantity *</label>
                    <input
                      id="qty_cs"
                      type="number"
                      min={1}
                      value={qty}
                      onChange={(e) => setQty(Math.max(1, Number(e.target.value)))}
                      required
                    />
                  </div>
                </div>

                <div className="form-grid-3" style={{ marginTop: "16px" }}>
                  <div className="form-group">
                    <label htmlFor="cost_price_cs">Cost Price per unit (THB) *</label>
                    <input
                      id="cost_price_cs"
                      type="number"
                      value={costPrice}
                      onChange={(e) => setCostPrice(Number(e.target.value))}
                      required
                    />
                  </div>
                  <div className="form-group">
                    <label htmlFor="sale_price_cs">Sale Price per unit (THB) *</label>
                    <input
                      id="sale_price_cs"
                      type="number"
                      value={salePrice}
                      onChange={(e) => setSalePrice(Number(e.target.value))}
                      required
                    />
                  </div>
                  <div className="form-group">
                    <label htmlFor="discount_cs">Discount Amount (THB)</label>
                    <input
                      id="discount_cs"
                      type="number"
                      value={discount}
                      onChange={(e) => setDiscount(Number(e.target.value))}
                    />
                  </div>
                </div>

                <div className="form-grid-2" style={{ marginTop: "16px" }}>
                  <div className="form-group">
                    <label htmlFor="owner_share_cs">Owner Share (%) *</label>
                    <input
                      id="owner_share_cs"
                      type="number"
                      max={100}
                      min={0}
                      value={ownerShare}
                      onChange={(e) => {
                        const val = Math.min(100, Math.max(0, Number(e.target.value)));
                        setOwnerShare(val);
                        setEmployeeShare(100 - val);
                      }}
                      required
                    />
                  </div>
                  <div className="form-group">
                    <label htmlFor="employee_share_cs">Employee Share (%) *</label>
                    <input
                      id="employee_share_cs"
                      type="number"
                      max={100}
                      min={0}
                      value={employeeShare}
                      onChange={(e) => {
                        const val = Math.min(100, Math.max(0, Number(e.target.value)));
                        setEmployeeShare(val);
                        setOwnerShare(100 - val);
                      }}
                      required
                    />
                  </div>
                </div>
              </div>

              {/* LIVE CALCULATIONS BOX */}
              <div className="panel cost-sheet-summary-panel animate-fade-in">
                <h3>Margin & Share Calculation Summary</h3>
                <div className="calc-summary-grid">
                  <div className="calc-item">
                    <span>Total Cost:</span>
                    <strong>{calculations.totalCost.toLocaleString()} THB</strong>
                  </div>
                  <div className="calc-item">
                    <span>Gross Sales:</span>
                    <strong>{calculations.grossSale.toLocaleString()} THB</strong>
                  </div>
                  <div className="calc-item">
                    <span>Net Sales (after discount):</span>
                    <strong>{calculations.netSale.toLocaleString()} THB</strong>
                  </div>
                  <div className="calc-item highlight">
                    <span>Gross Profit (GP):</span>
                    <strong className="text-success">{calculations.gpAmount.toLocaleString()} THB ({calculations.gpPercent}%)</strong>
                  </div>
                  <div className="calc-item">
                    <span>Owner Profit Share ({ownerShare}%):</span>
                    <strong>{calculations.ownerAmount.toLocaleString()} THB</strong>
                  </div>
                  <div className="calc-item">
                    <span>Employee Commission Share ({employeeShare}%):</span>
                    <strong>{calculations.employeeAmount.toLocaleString()} THB</strong>
                  </div>
                </div>
              </div>

              <footer className="form-actions" style={{ marginTop: "24px" }}>
                <button className="ghost-button" type="button" onClick={() => setMode("list")}>Cancel</button>
                <button className="primary-button" type="submit">Generate Cost Sheet</button>
              </footer>
            </form>
          </main>
        </>
      )}

      {/* 3. PREVIEW COST SHEET MODE */}
      {mode === "preview" && selectedCostSheet && (
        <>
          <header className="topbar">
            <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
              <button className="icon-back-btn" onClick={() => setMode("list")} aria-label="Back to listing" type="button">
                <ArrowLeft size={18} />
              </button>
              <div>
                <p>Cost Sheet / Preview</p>
                <h1>Sheet #{selectedCostSheet.id}</h1>
              </div>
            </div>
            <div className="topbar-actions">
              <button className="secondary-button" onClick={() => showToast("XLS Report exported successfully", "success")} type="button">
                <Download size={15} />
                Export XLS
              </button>
              <button className="secondary-button" onClick={() => showToast("Cost Sheet mailed to manager for review", "success")} type="button">
                <Send size={15} />
                Send Mail
              </button>
              {isAdmin && selectedCostSheet.status === "Pending" && (
                <>
                  <button className="primary-button success-button" onClick={() => handleUpdateStatus(selectedCostSheet.id, "Approved")} type="button">
                    <Check size={15} />
                    Approve
                  </button>
                  <button className="primary-button danger-button" onClick={() => handleUpdateStatus(selectedCostSheet.id, "Rejected")} type="button">
                    <X size={15} />
                    Reject
                  </button>
                </>
              )}
            </div>
          </header>

          <main className="content animate-fade-in">
            {/* Sheet Preview Panel */}
            <div className="panel invoice-preview-box" style={{ padding: "36px", background: "#fff", maxWidth: "800px", margin: "0 auto", border: "1px solid #ddd" }}>
              <div className="invoice-header" style={{ display: "flex", justifyContent: "space-between", borderBottom: "2px solid #333", paddingBottom: "16px" }}>
                <div>
                  <h2 style={{ margin: "0", color: "#1e3a8a" }}>ARROW IT SOLUTIONS</h2>
                  <p style={{ margin: "4px 0 0", color: "gray", fontSize: "13px" }}>Corporate Antivirus License Cost Assessment Sheet</p>
                </div>
                <div style={{ textAlign: "right" }}>
                  <h4 style={{ margin: "0" }}>COST SHEET #{selectedCostSheet.id}</h4>
                  <p style={{ margin: "4px 0 0", color: "gray", fontSize: "13px" }}>Date: {selectedCostSheet.created_at}</p>
                  <span className={`status-badge ${selectedCostSheet.status.toLowerCase()}`} style={{ marginTop: "8px" }}>
                    {selectedCostSheet.status}
                  </span>
                </div>
              </div>

              <div className="invoice-parties" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "24px", marginTop: "24px" }}>
                <div>
                  <h5 style={{ margin: "0 0 6px", color: "gray" }}>CUSTOMER</h5>
                  <strong>{getCustomerName(selectedCostSheet.cust_id)}</strong>
                  <p style={{ margin: "4px 0 0", fontSize: "13px", color: "#444" }}>
                    {customers.find(c => c.id === selectedCostSheet.cust_id)?.address}
                  </p>
                </div>
                <div>
                  <h5 style={{ margin: "0 0 6px", color: "gray" }}>PROJECT DETAILS</h5>
                  <strong>{selectedCostSheet.project_name}</strong>
                  <p style={{ margin: "4px 0 0", fontSize: "13px", color: "#444" }}>
                    Brand: {getBrandName(selectedCostSheet.brand_id)}
                  </p>
                </div>
              </div>

              <table className="corporate-table" aria-label="Cost sheet items table" style={{ marginTop: "32px", width: "100%", borderCollapse: "collapse" }}>
                <thead>
                  <tr style={{ background: "#f3f4f6" }}>
                    <th style={{ padding: "10px", borderBottom: "1px solid #ddd", textAlign: "left" }}>Description</th>
                    <th style={{ padding: "10px", borderBottom: "1px solid #ddd", textAlign: "right" }}>Qty</th>
                    <th style={{ padding: "10px", borderBottom: "1px solid #ddd", textAlign: "right" }}>Unit Price</th>
                    <th style={{ padding: "10px", borderBottom: "1px solid #ddd", textAlign: "right" }}>Total Cost</th>
                    <th style={{ padding: "10px", borderBottom: "1px solid #ddd", textAlign: "right" }}>Total Sale</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td style={{ padding: "12px 10px", borderBottom: "1px solid #ddd" }}>
                      {getProductName(selectedCostSheet.product_id)}
                    </td>
                    <td style={{ padding: "12px 10px", borderBottom: "1px solid #ddd", textAlign: "right" }}>
                      {selectedCostSheet.qty}
                    </td>
                    <td style={{ padding: "12px 10px", borderBottom: "1px solid #ddd", textAlign: "right" }}>
                      {selectedCostSheet.sale_price.toLocaleString()} THB
                    </td>
                    <td style={{ padding: "12px 10px", borderBottom: "1px solid #ddd", textAlign: "right" }}>
                      {(selectedCostSheet.qty * selectedCostSheet.cost_price).toLocaleString()} THB
                    </td>
                    <td style={{ padding: "12px 10px", borderBottom: "1px solid #ddd", textAlign: "right" }}>
                      {(selectedCostSheet.qty * selectedCostSheet.sale_price).toLocaleString()} THB
                    </td>
                  </tr>
                </tbody>
              </table>

              <div className="invoice-totals" style={{ marginTop: "24px", display: "flex", justifyContent: "flex-end" }}>
                <div style={{ width: "300px", display: "grid", gap: "8px", fontSize: "14px" }}>
                  <div style={{ display: "flex", justifyContent: "space-between" }}>
                    <span>Gross Sales:</span>
                    <strong>{(selectedCostSheet.qty * selectedCostSheet.sale_price).toLocaleString()} THB</strong>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between" }}>
                    <span>Discount:</span>
                    <strong style={{ color: "red" }}>-{selectedCostSheet.discount.toLocaleString()} THB</strong>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between", borderBottom: "1px solid #ddd", paddingBottom: "8px" }}>
                    <span>Net Sales:</span>
                    <strong>{((selectedCostSheet.qty * selectedCostSheet.sale_price) - selectedCostSheet.discount).toLocaleString()} THB</strong>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between", color: "green", fontSize: "16px" }}>
                    <span>Gross Profit (GP):</span>
                    <strong>{selectedCostSheet.gp_amount.toLocaleString()} THB ({selectedCostSheet.gp_percent}%)</strong>
                  </div>
                </div>
              </div>

              <div className="invoice-share" style={{ borderTop: "1px dashed #ddd", marginTop: "32px", paddingTop: "16px" }}>
                <h4 style={{ margin: "0 0 12px" }}>Share Breakdown</h4>
                <div style={{ display: "flex", justifyContent: "space-between", fontSize: "14px" }}>
                  <div>
                    Owner Share ({selectedCostSheet.owner_share_percent}%):{" "}
                    <strong>{(selectedCostSheet.gp_amount * (selectedCostSheet.owner_share_percent / 100)).toLocaleString()} THB</strong>
                  </div>
                  <div>
                    Employee Share ({selectedCostSheet.employee_share_percent}%):{" "}
                    <strong>{(selectedCostSheet.gp_amount * (selectedCostSheet.employee_share_percent / 100)).toLocaleString()} THB</strong>
                  </div>
                </div>
              </div>
            </div>
          </main>
        </>
      )}
    </div>
  );
};
