import React, { useState, useMemo, useEffect } from "react";
import { Plus, Pencil, Trash2, Search } from "lucide-react";
import { apiService } from "../domain/apiService";
import type { Brand, Product, AntivirusPrice, Category, Competitor, Profile, User } from "../domain/types";
import { Drawer } from "../components/Drawer";
import { ForbiddenView } from "./ForbiddenView";
import { canWriteMasterData, canDeleteMasterData } from "../domain/permissions";

export type MasterTableType = 
  | "profiles" 
  | "antiviruspricelist" 
  | "products" 
  | "brands" 
  | "businesstypes" 
  | "categories" 
  | "users" 
  | "competitors";

interface MasterDataViewProps {
  tableType: MasterTableType;
  userRole: string;
  showToast: (msg: string, type: "success" | "error" | "info") => void;
}

export const MasterDataView: React.FC<MasterDataViewProps> = ({ tableType, userRole, showToast }) => {
  // Local states for tables
  const [data, setData] = useState<any[]>([]);
  const [brands, setBrands] = useState<Brand[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [query, setQuery] = useState("");
  const [isOpen, setIsOpen] = useState(false);
  const [activeItem, setActiveItem] = useState<any | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isForbidden, setIsForbidden] = useState(false);

  // Sync data when table type changes
  useEffect(() => {
    loadTableData();
    setQuery("");
  }, [tableType]);

  const loadTableData = async () => {
    try {
      setIsLoading(true);
      setIsForbidden(false);
      // First load lookup tables if they are empty
      const [brdList, prodList, catList] = await Promise.all([
        apiService.getBrands(),
        apiService.getProducts(),
        apiService.getCategories()
      ]);
      setBrands(brdList);
      setProducts(prodList);
      setCategories(catList);

      let fetchedData: any[] = [];
      switch (tableType) {
        case "profiles":
          fetchedData = await apiService.getProfiles();
          break;
        case "antiviruspricelist":
          fetchedData = await apiService.getAntivirusPrices();
          break;
        case "products":
          fetchedData = prodList; // reuse loaded products
          break;
        case "brands":
          fetchedData = brdList; // reuse loaded brands
          break;
        case "businesstypes":
          fetchedData = await apiService.getBusinessTypes();
          break;
        case "categories":
          fetchedData = catList; // reuse loaded categories
          break;
        case "users":
          fetchedData = await apiService.getUsers();
          break;
        case "competitors":
          fetchedData = await apiService.getCompetitors();
          break;
      }
      setData(fetchedData);
    } catch (err: any) {
      if (err.message?.includes("403") || err.message?.includes("Forbidden")) {
        setIsForbidden(true);
      } else {
        showToast(`Failed to load ${getTableTitle().toLowerCase()} data`, "error");
      }
    } finally {
      setIsLoading(false);
    }
  };

  const getTableTitle = () => {
    switch (tableType) {
      case "profiles": return "Profiles";
      case "antiviruspricelist": return "Antivirus Price List";
      case "products": return "Products";
      case "brands": return "Brands";
      case "businesstypes": return "Business Types";
      case "categories": return "Categories";
      case "users": return "Users";
      case "competitors": return "Competitors";
    }
  };

  const getBrandName = (id: number) => {
    return brands.find(b => b.id === id)?.name || "Unknown Brand";
  };

  const getProductName = (id: number) => {
    return products.find(p => p.id === id)?.name || "Unknown Product";
  };

  const getCategoryName = (id: number) => {
    return categories.find(c => c.id === id)?.name || "Unknown Category";
  };

  // Filtered rows
  const filteredData = useMemo(() => {
    return data.filter(item => {
      const searchStr = Object.values(item).join(" ").toLowerCase();
      return searchStr.includes(query.toLowerCase());
    });
  }, [data, query]);

  // Handle Save
  const handleSave = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    const formObj: any = {};
    formData.forEach((value, key) => {
      formObj[key] = value;
    });

    // Parse numeric values
    if (formObj.brand_id) formObj.brand_id = Number(formObj.brand_id);
    if (formObj.category_id) formObj.category_id = Number(formObj.category_id);
    if (formObj.product_id) formObj.product_id = Number(formObj.product_id);
    if (formObj.price) formObj.price = Number(formObj.price);
    if (formObj.cost) formObj.cost = Number(formObj.cost);
    if (formObj.amt) formObj.amt = Number(formObj.amt);
    if (formObj.year) formObj.year = Number(formObj.year);
    if (formObj.start) formObj.start = Number(formObj.start);
    if (formObj.end) formObj.end = Number(formObj.end);

    try {
      if (activeItem) {
        // Edit mode
        switch (tableType) {
          case "profiles":
            await apiService.updateProfile(activeItem.id, formObj);
            break;
          case "antiviruspricelist":
            await apiService.updateAntivirusPrice(activeItem.id, formObj);
            break;
          case "products":
            await apiService.updateProduct(activeItem.id, formObj);
            break;
          case "brands":
            await apiService.updateBrand(activeItem.id, formObj.name);
            break;
          case "businesstypes":
            await apiService.updateBusinessType(activeItem.id, formObj.type);
            break;
          case "categories":
            await apiService.updateCategory(activeItem.id, formObj.name);
            break;
          case "users":
            showToast("User updates must be performed in backend", "info");
            break;
          case "competitors":
            await apiService.updateCompetitor(activeItem.id, formObj);
            break;
        }
        showToast("Updated item details successfully", "success");
      } else {
        // Create mode
        switch (tableType) {
          case "profiles":
            await apiService.addProfile(formObj);
            break;
          case "antiviruspricelist":
            await apiService.addAntivirusPrice(formObj);
            break;
          case "products":
            await apiService.addProduct(formObj);
            break;
          case "brands":
            await apiService.addBrand(formObj.name);
            break;
          case "businesstypes":
            await apiService.addBusinessType(formObj.type);
            break;
          case "categories":
            await apiService.addCategory(formObj.name);
            break;
          case "users":
            showToast("User creation must be performed in backend", "info");
            break;
          case "competitors":
            await apiService.addCompetitor(formObj);
            break;
        }
        showToast("Added new record successfully", "success");
      }
      loadTableData();
      setIsOpen(false);
    } catch (err) {
      showToast("Operation failed", "error");
    }
  };

  // Handle Delete
  const handleDelete = async (id: number) => {
    if (window.confirm("Are you sure you want to delete this record?")) {
      try {
        switch (tableType) {
          case "profiles": await apiService.deleteProfile(id); break;
          case "antiviruspricelist": await apiService.deleteAntivirusPrice(id); break;
          case "products": await apiService.deleteProduct(id); break;
          case "brands": await apiService.deleteBrand(id); break;
          case "businesstypes": await apiService.deleteBusinessType(id); break;
          case "categories": await apiService.deleteCategory(id); break;
          case "users": showToast("User deletion must be performed in backend", "info"); return;
          case "competitors": await apiService.deleteCompetitor(id); break;
        }
        loadTableData();
        showToast("Record deleted successfully", "success");
      } catch (err) {
        showToast("Failed to delete record", "error");
      }
    }
  };

  if (isForbidden) {
    return <ForbiddenView />;
  }

  return (
    <div className="workspace-view">
      <header className="topbar">
        <div>
          <p>Master Data / {getTableTitle()}</p>
          <h1>Manage {getTableTitle()}</h1>
        </div>
        {tableType !== "users" && canWriteMasterData(userRole, tableType) && (
          <button className="primary-button" onClick={() => { setActiveItem(null); setIsOpen(true); }} type="button">
            <Plus size={15} />
            Add Record
          </button>
        )}
      </header>

      <main className="content animate-fade-in">
        <div className="panel">
          <div className="filter-band">
            <label className="filter-search">
              <span>Search inside {getTableTitle().toLowerCase()}</span>
              <div className="search-field">
                <Search size={16} aria-hidden="true" />
                <input
                  type="text"
                  placeholder="Search..."
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  aria-label="Filter database"
                />
              </div>
            </label>
          </div>

          <div className="table-wrap">
            <table className="corporate-table" aria-label={`${getTableTitle()} database table`}>
              <thead>
                {tableType === "profiles" && (
                  <tr>
                    <th style={{ width: "5%" }}>ID</th>
                    <th style={{ width: "30%" }}>Name</th>
                    <th style={{ width: "20%" }}>Type</th>
                    <th style={{ width: "25%" }}>Item</th>
                    <th style={{ width: "20%" }}>Edition</th>
                    <th style={{ width: "10%" }}>&nbsp;</th>
                  </tr>
                )}
                {tableType === "antiviruspricelist" && (
                  <tr>
                    <th style={{ width: "5%" }}>ID</th>
                    <th style={{ width: "15%" }}>Brand</th>
                    <th style={{ width: "10%" }}>Code</th>
                    <th style={{ width: "25%" }}>Edition</th>
                    <th style={{ width: "10%" }}>Start</th>
                    <th style={{ width: "10%" }}>End</th>
                    <th style={{ width: "15%" }}>Cost</th>
                    <th style={{ width: "10%" }}>Type</th>
                    <th style={{ width: "10%" }}>&nbsp;</th>
                  </tr>
                )}
                {tableType === "products" && (
                  <tr>
                    <th style={{ width: "5%" }}>ID</th>
                    <th style={{ width: "20%" }}>Brand</th>
                    <th style={{ width: "20%" }}>Category</th>
                    <th style={{ width: "30%" }}>Name</th>
                    <th style={{ width: "10%" }}>Cost</th>
                    <th style={{ width: "10%" }}>Price</th>
                    <th style={{ width: "10%" }}>&nbsp;</th>
                  </tr>
                )}
                {tableType === "brands" && (
                  <tr>
                    <th style={{ width: "10%" }}>ID</th>
                    <th style={{ width: "50%" }}>Brand Name</th>
                    <th style={{ width: "30%" }}>Code</th>
                    <th style={{ width: "10%" }}>&nbsp;</th>
                  </tr>
                )}
                {tableType === "businesstypes" && (
                  <tr>
                    <th style={{ width: "10%" }}>ID</th>
                    <th style={{ width: "80%" }}>Business Type Detail</th>
                    <th style={{ width: "10%" }}>&nbsp;</th>
                  </tr>
                )}
                {tableType === "categories" && (
                  <tr>
                    <th style={{ width: "10%" }}>ID</th>
                    <th style={{ width: "80%" }}>Category Name</th>
                    <th style={{ width: "10%" }}>&nbsp;</th>
                  </tr>
                )}
                {tableType === "users" && (
                  <tr>
                    <th style={{ width: "5%" }}>No.</th>
                    <th style={{ width: "15%" }}>User</th>
                    <th style={{ width: "15%" }}>Role</th>
                    <th style={{ width: "20%" }}>Email</th>
                    <th style={{ width: "15%" }}>Tel</th>
                    <th style={{ width: "15%" }}>Position</th>
                    <th style={{ width: "15%" }}>Line Token</th>
                  </tr>
                )}
                {tableType === "competitors" && (
                  <tr>
                    <th style={{ width: "10%" }}>ID</th>
                    <th style={{ width: "30%" }}>Name</th>
                    <th style={{ width: "15%" }}>Year</th>
                    <th style={{ width: "20%" }}>Amount</th>
                    <th style={{ width: "15%" }}>Compare</th>
                    <th style={{ width: "10%" }}>&nbsp;</th>
                  </tr>
                )}
              </thead>
              <tbody>
                {isLoading ? (
                  <tr>
                    <td colSpan={10} style={{ textAlign: "center", padding: "32px 0" }}>
                      Loading data from database...
                    </td>
                  </tr>
                ) : filteredData.length > 0 ? (
                  filteredData.map(item => (
                    <tr key={item.id}>
                      {tableType === "profiles" && (
                        <>
                          <td>{item.id}</td>
                          <td><strong>{item.name}</strong></td>
                          <td>{item.type}</td>
                          <td>{item.item}</td>
                          <td>{item.edition}</td>
                        </>
                      )}
                      {tableType === "antiviruspricelist" && (
                        <>
                          <td>{item.id}</td>
                          <td>{item.brand}</td>
                          <td><code>{item.code}</code></td>
                          <td><strong>{item.edition}</strong></td>
                          <td>{item.start}</td>
                          <td>{item.end}</td>
                          <td>{item.cost?.toLocaleString()} THB</td>
                          <td>{item.types}</td>
                        </>
                      )}
                      {tableType === "products" && (
                        <>
                          <td>{item.id}</td>
                          <td>{getBrandName(item.brand_id)}</td>
                          <td>{getCategoryName(item.category_id)}</td>
                          <td><strong>{item.name}</strong></td>
                          <td>{item.cost.toLocaleString()} THB</td>
                          <td>{item.price.toLocaleString()} THB</td>
                        </>
                      )}
                      {tableType === "brands" && (
                        <>
                          <td>{item.id}</td>
                          <td><strong>{item.name}</strong></td>
                          <td><code>{item.code}</code></td>
                        </>
                      )}
                      {tableType === "businesstypes" && (
                        <>
                          <td>{item.id}</td>
                          <td><strong>{item.name}</strong></td>
                        </>
                      )}
                      {tableType === "categories" && (
                        <>
                          <td>{item.id}</td>
                          <td><strong>{item.name}</strong></td>
                        </>
                      )}
                      {tableType === "users" && (
                        <>
                          <td>{item.id}</td>
                          <td>
                            <strong>{item.name}</strong>
                            <span className="subtext">({item.username})</span>
                          </td>
                          <td>
                            <span className={`status-badge info`}>{item.roles}</span>
                          </td>
                          <td>{item.email}</td>
                          <td>{item.tel || "-"}</td>
                          <td>{item.position || "-"}</td>
                          <td><code>{item.linetoken || "-"}</code></td>
                        </>
                      )}
                      {tableType === "competitors" && (
                        <>
                          <td>{item.id}</td>
                          <td><strong>{item.name}</strong></td>
                          <td>{item.year}</td>
                          <td>{item.amt.toLocaleString()}</td>
                          <td>{item.compare}</td>
                        </>
                      )}

                      {tableType !== "users" && (
                        <td style={{ textAlign: "right" }}>
                          <div className="row-actions">
                            {canWriteMasterData(userRole, tableType) && (
                              <button onClick={() => { setActiveItem(item); setIsOpen(true); }} aria-label="Edit record" type="button">
                                <Pencil size={13} />
                              </button>
                            )}
                            {canDeleteMasterData(userRole, tableType) && (
                              <button className="delete-btn" onClick={() => handleDelete(item.id)} aria-label="Delete record" type="button">
                                <Trash2 size={13} />
                              </button>
                            )}
                          </div>
                        </td>
                      )}
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={10} style={{ textAlign: "center", padding: "24px 0" }}>
                      No records found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </main>

      <Drawer
        isOpen={isOpen}
        title={activeItem ? `Edit ${getTableTitle().slice(0, -1)}` : `Add ${getTableTitle().slice(0, -1)}`}
        onClose={() => setIsOpen(false)}
      >
        <form onSubmit={handleSave} className="corporate-form">
          {tableType === "profiles" && (
            <>
              <div className="form-group">
                <label htmlFor="name_prof">Name *</label>
                <input id="name_prof" name="name" type="text" defaultValue={activeItem?.name || ""} required />
              </div>
              <div className="form-group">
                <label htmlFor="type_prof">Type *</label>
                <input id="type_prof" name="type" type="text" defaultValue={activeItem?.type || "ANTIVIRUS"} required />
              </div>
              <div className="form-group">
                <label htmlFor="item_prof">Item</label>
                <input id="item_prof" name="item" type="text" defaultValue={activeItem?.item || ""} />
              </div>
              <div className="form-group">
                <label htmlFor="edition_prof">Edition</label>
                <input id="edition_prof" name="edition" type="text" defaultValue={activeItem?.edition || ""} />
              </div>
            </>
          )}

          {tableType === "antiviruspricelist" && (
            <>
              <div className="form-group">
                <label htmlFor="brand_id_ap">Brand *</label>
                <select id="brand_id_ap" name="brand_id" defaultValue={activeItem?.brand_id || brands[0]?.id}>
                  {brands.map(b => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="edition">Edition *</label>
                <input id="edition" name="edition" type="text" defaultValue={activeItem?.edition || ""} required />
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="start_ap">Start *</label>
                  <input id="start_ap" name="start" type="number" defaultValue={activeItem?.start ?? 1} required />
                </div>
                <div className="form-group">
                  <label htmlFor="end_ap">End *</label>
                  <input id="end_ap" name="end" type="number" defaultValue={activeItem?.end ?? 100} required />
                </div>
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="price_ap">Cost (Price) *</label>
                  <input id="price_ap" name="price" type="number" defaultValue={activeItem?.cost ?? activeItem?.price ?? 0} required />
                </div>
                <div className="form-group">
                  <label htmlFor="types_ap">Type *</label>
                  <select id="types_ap" name="types" defaultValue={activeItem?.types || "Client"}>
                    <option value="Client">Client</option>
                    <option value="Server">Server</option>
                  </select>
                </div>
              </div>
            </>
          )}

          {tableType === "products" && (
            <>
              <div className="form-group">
                <label htmlFor="brand_id_p">Brand *</label>
                <select id="brand_id_p" name="brand_id" defaultValue={activeItem?.brand_id || brands[0]?.id}>
                  {brands.map(b => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="category_id_p">Category *</label>
                <select id="category_id_p" name="category_id" defaultValue={activeItem?.category_id || categories[0]?.id}>
                  {categories.map(c => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="name_p">Product Name *</label>
                <input id="name_p" name="name" type="text" defaultValue={activeItem?.name || ""} required />
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="cost_p">Cost *</label>
                  <input id="cost_p" name="cost" type="number" defaultValue={activeItem?.cost || 0} required />
                </div>
              </div>
            </>
          )}

          {tableType === "brands" && (
            <>
              <div className="form-group">
                <label htmlFor="name_b">Brand Name *</label>
                <input id="name_b" name="name" type="text" defaultValue={activeItem?.name || ""} required />
              </div>
            </>
          )}

          {tableType === "businesstypes" && (
            <>
              <div className="form-group">
                <label htmlFor="type_bt">Business Type Name *</label>
                <input id="type_bt" name="type" type="text" defaultValue={activeItem?.name || ""} required />
              </div>
            </>
          )}

          {tableType === "categories" && (
            <>
              <div className="form-group">
                <label htmlFor="name_cat">Category Name *</label>
                <input id="name_cat" name="name" type="text" defaultValue={activeItem?.name || ""} required />
              </div>
            </>
          )}

          {tableType === "competitors" && (
            <>
              <div className="form-group">
                <label htmlFor="name_comp">Competitor Name *</label>
                <input id="name_comp" name="name" type="text" defaultValue={activeItem?.name || ""} required />
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="year">Year *</label>
                  <input id="year" name="year" type="number" defaultValue={activeItem?.year || new Date().getFullYear()} required />
                </div>
                <div className="form-group">
                  <label htmlFor="amt">Amount *</label>
                  <input id="amt" name="amt" type="number" defaultValue={activeItem?.amt || 0} required />
                </div>
              </div>
              <div className="form-group">
                <label htmlFor="compare">Compare *</label>
                <select id="compare" name="compare" defaultValue={activeItem?.compare || "Smaller"}>
                  <option value="Smaller">Smaller</option>
                  <option value="Equal">Equal</option>
                  <option value="Larger">Larger</option>
                </select>
              </div>
            </>
          )}

          <footer className="form-actions">
            <button className="ghost-button" type="button" onClick={() => setIsOpen(false)}>Cancel</button>
            <button className="primary-button" type="submit">Save Record</button>
          </footer>
        </form>
      </Drawer>
    </div>
  );
};
