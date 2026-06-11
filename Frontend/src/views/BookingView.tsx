import React, { useState, useEffect, useMemo } from "react";
import { Search, Bookmark, Pencil } from "lucide-react";
import { apiService } from "../domain/apiService";
import type { Customer, User, Category } from "../domain/types";
import { Drawer } from "../components/Drawer";

interface BookingViewProps {
  userRole: string;
  showToast: (msg: string, type: "success" | "error" | "info") => void;
}

export const BookingView: React.FC<BookingViewProps> = ({ userRole, showToast }) => {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [businessTypes, setBusinessTypes] = useState<Category[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Search/Filters
  const [query, setQuery] = useState("");
  const [businessType, setBusinessType] = useState("");
  const [saleFilter, setSaleFilter] = useState("all");
  const [telesaleFilter, setTelesaleFilter] = useState("all");

  // Drawer for edit customer inside Booking
  const [isCustomerDrawerOpen, setIsCustomerDrawerOpen] = useState(false);
  const [activeCustomer, setActiveCustomer] = useState<Customer | null>(null);

  const loadData = async () => {
    try {
      setIsLoading(true);
      const [custs, uList, btList] = await Promise.all([
        apiService.getCustomers(),
        apiService.getUsers(),
        apiService.getBusinessTypes()
      ]);
      setCustomers(custs);
      setUsers(uList);
      setBusinessTypes(btList);
    } catch (err) {
      showToast("Failed to load booking data", "error");
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  // Filtered customers list
  const filteredCustomers = useMemo(() => {
    return customers.filter(c => {
      const matchQuery = `${c.name} ${c.address}`.toLowerCase().includes(query.toLowerCase());
      const matchBus = businessType ? c.bt_type === businessType : true;

      let matchSale = true;
      if (saleFilter === "assigned") matchSale = c.sale_id !== null;
      else if (saleFilter === "unassigned") matchSale = c.sale_id === null;

      let matchTele = true;
      if (telesaleFilter === "assigned") matchTele = c.telesale_id !== null;
      else if (telesaleFilter === "unassigned") matchTele = c.telesale_id === null;

      return matchQuery && matchBus && matchSale && matchTele;
    });
  }, [customers, query, businessType, saleFilter, telesaleFilter]);

  const getUserName = (id: number | null) => {
    if (!id) return "Not assigned";
    return users.find(u => u.id === id)?.name || "Unknown";
  };

  const handleBook = async (c: Customer) => {
    if (window.confirm(`Are you sure you would like to book customer "${c.name}"?`)) {
      try {
        await apiService.bookCustomer(c.id);
        const refreshed = await apiService.getCustomers();
        setCustomers(refreshed);
        showToast(`Successfully booked customer: ${c.name}`, "success");
      } catch (err) {
        showToast("Booking failed", "error");
      }
    }
  };

  const handleSaveCustomer = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!activeCustomer) return;

    const formData = new FormData(e.currentTarget);
    const customerData = {
      name: formData.get("name") as string,
      address: formData.get("address") as string,
      capital: formData.get("capital") as string,
      bt_type: formData.get("bt_type") as string,
      status: formData.get("status") as string,
    };

    try {
      await apiService.updateCustomer(activeCustomer.id, customerData);
      const refreshed = await apiService.getCustomers();
      setCustomers(refreshed);
      setIsCustomerDrawerOpen(false);
      showToast(`Updated customer: ${customerData.name}`, "success");
    } catch (err) {
      showToast("Update failed", "error");
    }
  };

  // Helper to determine if User can Book this customer
  const canBook = (c: Customer) => {
    if (!c.is_active) return false;
    
    if (userRole === "Tele sale") {
      if (c.status === "Booking" && c.telesale_id !== null) return false;
    } else if (userRole === "Sale") {
      if (c.status === "Booking" && c.sale_id !== null) return false;
    }

    if (c.status === "Assigned" || c.status === "REPLACE") return false;

    return true;
  };

  return (
    <div className="workspace-view">
      <header className="topbar">
        <div>
          <p>Customer / Booking</p>
          <h1>Booking Workspace</h1>
        </div>
      </header>

      <main className="content animate-fade-in">
        {/* Filters */}
        <div className="panel filter-panel">
          <div className="filter-band">
            <label>
              <span>Customer name, address</span>
              <input 
                type="text" 
                placeholder="Search Name or Address..." 
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                aria-label="Search customer"
              />
            </label>
            <label>
              <span>Business Type</span>
              <select 
                value={businessType} 
                onChange={(e) => setBusinessType(e.target.value)}
                aria-label="Select business type"
              >
                <option value="">All business types</option>
                {businessTypes.map(t => (
                  <option key={t.id} value={t.name}>{t.name}</option>
                ))}
              </select>
            </label>
            <label>
              <span>Sale Assign</span>
              <select 
                value={saleFilter} 
                onChange={(e) => setSaleFilter(e.target.value)}
                aria-label="Filter by sale assignment"
              >
                <option value="all">All</option>
                <option value="assigned">Assigned</option>
                <option value="unassigned">Not assigned</option>
              </select>
            </label>
            <label>
              <span>Telesale Assign</span>
              <select 
                value={telesaleFilter} 
                onChange={(e) => setTelesaleFilter(e.target.value)}
                aria-label="Filter by telesale assignment"
              >
                <option value="all">All</option>
                <option value="assigned">Assigned</option>
                <option value="unassigned">Not assigned</option>
              </select>
            </label>
          </div>

          {/* Booking Table */}
          <div className="table-wrap">
            <table className="corporate-table" aria-label="Booking customer table">
              <thead>
                <tr>
                  <th style={{ width: "5%" }}>No.</th>
                  <th style={{ width: "35%" }}>Name / Address</th>
                  <th style={{ width: "15%" }}>Business Type</th>
                  <th style={{ width: "12%" }}>Sale</th>
                  <th style={{ width: "12%" }}>Tele sale</th>
                  <th style={{ width: "10%" }}>Status</th>
                  <th style={{ width: "11%" }}>&nbsp;</th>
                </tr>
              </thead>
              <tbody>
                {isLoading ? (
                  <tr>
                    <td colSpan={7} style={{ textAlign: "center", padding: "32px 0" }}>
                      Loading booking workspace...
                    </td>
                  </tr>
                ) : filteredCustomers.length > 0 ? (
                  filteredCustomers.map(c => (
                    <tr key={c.id} className={!c.is_active ? "row-disabled" : ""}>
                      <td>{c.id}</td>
                      <td>
                        <strong>{c.name}</strong>
                        <span className="subtext">{c.address}</span>
                      </td>
                      <td>{c.bt_type}</td>
                      <td>{getUserName(c.sale_id)}</td>
                      <td>{getUserName(c.telesale_id)}</td>
                      <td>
                        <span className={`status-badge ${c.is_active ? c.status.toLowerCase() : "neutral"}`}>
                          {c.is_active ? c.status : "Inactive"}
                        </span>
                      </td>
                      <td style={{ textAlign: "right" }}>
                        <div className="row-actions" style={{ justifyContent: "flex-end" }}>
                          {canBook(c) && (
                            <button
                              className="action-pill blue"
                              onClick={() => handleBook(c)}
                              type="button"
                              style={{ display: "inline-flex", gap: "4px" }}
                            >
                              <Bookmark size={12} />
                              Book
                            </button>
                          )}
                          {c.is_active && (
                            <button
                              onClick={() => { setActiveCustomer(c); setIsCustomerDrawerOpen(true); }}
                              aria-label={`Edit details of ${c.name}`}
                              type="button"
                            >
                              <Pencil size={13} />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={7} style={{ textAlign: "center", padding: "24px 0" }}>
                      No customers available for booking.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </main>

      {/* Edit Customer Drawer */}
      <Drawer
        isOpen={isCustomerDrawerOpen}
        title={activeCustomer ? `Edit Customer Details: ${activeCustomer.name}` : ""}
        onClose={() => setIsCustomerDrawerOpen(false)}
      >
        {activeCustomer && (
          <form onSubmit={handleSaveCustomer} className="corporate-form">
            <div className="form-group">
              <label htmlFor="name_book">Customer Name *</label>
              <input 
                id="name_book"
                name="name" 
                type="text" 
                defaultValue={activeCustomer.name} 
                required 
              />
            </div>
            <div className="form-group">
              <label htmlFor="address_book">Address *</label>
              <textarea 
                id="address_book"
                name="address" 
                rows={3} 
                defaultValue={activeCustomer.address} 
                required 
              />
            </div>
            <div className="form-group">
              <label htmlFor="capital_book">Capital (THB)</label>
              <input 
                id="capital_book"
                name="capital" 
                type="text" 
                defaultValue={activeCustomer.capital || ""} 
              />
            </div>

            <fieldset className="form-section">
              <legend>Status</legend>
              <div className="form-group">
                <label htmlFor="bt_type_book">Business Type *</label>
                <select 
                  id="bt_type_book"
                  name="bt_type" 
                  defaultValue={activeCustomer.bt_type} 
                  required
                >
                  {businessTypes.map(t => (
                    <option key={t.id} value={t.name}>{t.name}</option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="status_book">Status</label>
                <select 
                  id="status_book"
                  name="status" 
                  defaultValue={activeCustomer.status}
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
            </fieldset>

            <footer className="form-actions">
              <button className="ghost-button" type="button" onClick={() => setIsCustomerDrawerOpen(false)}>Cancel</button>
              <button className="primary-button" type="submit">Save Changes</button>
            </footer>
          </form>
        )}
      </Drawer>
    </div>
  );
};
