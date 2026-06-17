import type {
  User,
  Customer,
  ContactDetail,
  DetailDevice,
  DetailProject,
  Brand,
  Product,
  AntivirusPrice,
  Category,
  Competitor,
  Profile,
  CostSheet,
  PaginatedResponse
} from "./types";

const API_BASE = import.meta.env.VITE_API_BASE_URL || "/api";

let onUnauthorizedCallback: (() => void) | null = null;
let onForbiddenCallback: (() => void) | null = null;

export const setUnauthorizedCallback = (callback: () => void) => {
  onUnauthorizedCallback = callback;
};

export const setForbiddenCallback = (callback: () => void) => {
  onForbiddenCallback = callback;
};

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const url = `${API_BASE}${path}`;
  const response = await fetch(url, {
    ...options,
    credentials: "same-origin", // Ensure same-origin cookies are sent for proxying
    headers: {
      "Content-Type": "application/json",
      ...(options?.headers || {})
    }
  });

  if (!response.ok) {
    let errorMessage = `API error ${response.status}`;
    try {
      const errorText = await response.text();
      try {
        const errorJson = JSON.parse(errorText);
        errorMessage = errorJson.message || errorJson.error || errorMessage;
      } catch {
        if (errorText && errorText.length < 200 && !errorText.includes("<!DOCTYPE") && !errorText.includes("<html>")) {
          errorMessage = errorText;
        } else {
          if (response.status === 401) {
            errorMessage = "Session expired. Please log in again.";
          } else if (response.status === 403) {
            errorMessage = "Access denied. You do not have permission to perform this action.";
          } else {
            errorMessage = "An internal server error occurred. Please contact support.";
          }
        }
      }
    } catch {
      // Ignored
    }

    if (response.status === 401) {
      if (onUnauthorizedCallback) {
        onUnauthorizedCallback();
      }
    } else if (response.status === 403) {
      if (onForbiddenCallback) {
        onForbiddenCallback();
      }
    }

    throw new ApiError(response.status, errorMessage);
  }

  const text = await response.text();
  return text ? JSON.parse(text) : ({} as T);
}

export const apiService = {
  // Auth
  async login(username: string, password: string): Promise<any> {
    return request<any>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password })
    });
  },

  async logout(): Promise<void> {
    await request<void>("/auth/logout", {
      method: "POST"
    });
  },

  async getMe(): Promise<any> {
    return request<any>("/auth/me");
  },

  // Users
  async getUsers(): Promise<User[]> {
    return request<User[]>("/users");
  },

  // Customers
  async getCustomers(params?: { search?: string; page?: number; pageSize?: number }): Promise<Customer[]> {
    if (params) {
      const searchParams = new URLSearchParams();
      if (params.page !== undefined) searchParams.append("page", params.page.toString());
      if (params.pageSize !== undefined) searchParams.append("pageSize", params.pageSize.toString());
      if (params.search !== undefined) searchParams.append("search", params.search);
      const res = await request<any>(`/customers?${searchParams.toString()}`);
      if (res && Array.isArray(res)) return res;
      if (res && res.items && Array.isArray(res.items)) return res.items;
      return [];
    }
    const res = await request<any>("/customers");
    if (res && Array.isArray(res)) return res;
    if (res && res.items && Array.isArray(res.items)) return res.items;
    return [];
  },

  async getCustomersPaginated(params: {
    page: number;
    pageSize: number;
    search?: string;
    businessType?: string;
    saleId?: string;
    telesaleId?: string;
  }): Promise<PaginatedResponse<Customer>> {
    const searchParams = new URLSearchParams();
    searchParams.append("page", params.page.toString());
    searchParams.append("pageSize", params.pageSize.toString());
    if (params.search) {
      const clean = params.search.trim().replace(/\s+/g, "+");
      if (clean) {
        searchParams.append("search", `+${clean}+`);
      }
    }
    if (params.businessType) searchParams.append("businessType", params.businessType);
    if (params.saleId) searchParams.append("saleId", params.saleId);
    if (params.telesaleId) searchParams.append("telesaleId", params.telesaleId);

    const res = await request<any>(`/customers?${searchParams.toString()}`);
    if (res && Array.isArray(res)) {
      return {
        items: res,
        totalCount: res.length,
        page: params.page,
        pageSize: params.pageSize,
        totalPages: Math.ceil(res.length / params.pageSize)
      };
    }
    return res;
  },

  async addCustomer(
    customer: Omit<
      Customer,
      | "id"
      | "updatedAt"
      | "renewalDays"
      | "hasCostSheet"
      | "telesale_id"
      | "sale_id"
      | "status"
      | "is_active"
      | "start_dt"
    > &
      Partial<Pick<Customer, "telesale_id" | "sale_id" | "status" | "is_active" | "start_dt">>
  ): Promise<Customer> {
    return request<Customer>("/customers", {
      method: "POST",
      body: JSON.stringify(customer)
    });
  },

  async updateCustomer(id: number, customer: Partial<Customer>): Promise<Customer> {
    return request<Customer>(`/customers/${id}`, {
      method: "PUT",
      body: JSON.stringify(customer)
    });
  },

  async deleteCustomer(id: number): Promise<boolean> {
    return request<boolean>(`/customers/${id}`, {
      method: "DELETE"
    });
  },

  async assignCustomer(id: number, userId: number, role: "Sale" | "Tele sale"): Promise<Customer> {
    return request<Customer>(`/customers/${id}/assign`, {
      method: "PUT",
      body: JSON.stringify({ userId, role })
    });
  },

  async bookCustomer(id: number): Promise<Customer> {
    return request<Customer>(`/customers/${id}/book`, {
      method: "PUT"
    });
  },

  // Contact Details
  async getContactDetails(custId: number): Promise<ContactDetail[]> {
    return request<ContactDetail[]>(`/customers/${custId}/contacts`);
  },

  async addContactDetail(
    custId: number,
    contact: Omit<ContactDetail, "id" | "cust_id" | "point" | "total_point">
  ): Promise<ContactDetail> {
    return request<ContactDetail>(`/customers/${custId}/contacts`, {
      method: "POST",
      body: JSON.stringify(contact)
    });
  },

  async updateContactDetail(id: number, contact: Partial<ContactDetail>): Promise<ContactDetail> {
    return request<ContactDetail>(`/customers/contacts/${id}`, {
      method: "PUT",
      body: JSON.stringify(contact)
    });
  },

  async deleteContactDetail(id: number): Promise<boolean> {
    return request<boolean>(`/customers/contacts/${id}`, {
      method: "DELETE"
    });
  },

  // Devices
  async getDevices(contactId: number): Promise<DetailDevice[]> {
    return request<DetailDevice[]>(`/customers/contacts/${contactId}/devices`);
  },

  async addDevice(contactId: number, device: Omit<DetailDevice, "id" | "dtl_id">): Promise<DetailDevice> {
    return request<DetailDevice>(`/customers/contacts/${contactId}/devices`, {
      method: "POST",
      body: JSON.stringify(device)
    });
  },

  async updateDevice(id: number, device: Partial<DetailDevice>): Promise<DetailDevice> {
    return request<DetailDevice>(`/customers/devices/${id}`, {
      method: "PUT",
      body: JSON.stringify(device)
    });
  },

  async deleteDevice(id: number): Promise<boolean> {
    return request<boolean>(`/customers/devices/${id}`, {
      method: "DELETE"
    });
  },

  // Projects
  async getProjects(contactId: number): Promise<DetailProject[]> {
    return request<DetailProject[]>(`/customers/contacts/${contactId}/projects`);
  },

  async addProject(contactId: number, project: Omit<DetailProject, "id" | "dtl_id">): Promise<DetailProject> {
    return request<DetailProject>(`/customers/contacts/${contactId}/projects`, {
      method: "POST",
      body: JSON.stringify(project)
    });
  },

  async updateProject(id: number, project: Partial<DetailProject>): Promise<DetailProject> {
    return request<DetailProject>(`/customers/projects/${id}`, {
      method: "PUT",
      body: JSON.stringify(project)
    });
  },

  async deleteProject(id: number): Promise<boolean> {
    return request<boolean>(`/customers/projects/${id}`, {
      method: "DELETE"
    });
  },

  // Cost Sheets
  async getCostSheets(): Promise<CostSheet[]> {
    return request<CostSheet[]>("/costsheets");
  },

  async getCostSheetsPaginated(params: { page: number; pageSize: number }): Promise<PaginatedResponse<CostSheet>> {
    const res = await request<any>(`/costsheets?page=${params.page}&pageSize=${params.pageSize}`);
    if (res && Array.isArray(res)) {
      return {
        items: res,
        totalCount: res.length,
        page: params.page,
        pageSize: params.pageSize,
        totalPages: Math.ceil(res.length / params.pageSize)
      };
    }
    return res;
  },

  async addCostSheet(costSheet: Omit<CostSheet, "id" | "created_at">): Promise<CostSheet> {
    return request<CostSheet>("/costsheets", {
      method: "POST",
      body: JSON.stringify(costSheet)
    });
  },

  async updateCostSheetStatus(id: number, status: "Approved" | "Rejected"): Promise<CostSheet> {
    return request<CostSheet>(`/costsheets/${id}/status`, {
      method: "PUT",
      body: JSON.stringify({ status })
    });
  },

  async deleteCostSheet(id: number): Promise<boolean> {
    return request<boolean>(`/costsheets/${id}`, {
      method: "DELETE"
    });
  },

  // Master Data - Brands
  async getBrands(): Promise<Brand[]> {
    return request<Brand[]>("/masterdata/brands");
  },
  async addBrand(name: string): Promise<Brand> {
    return request<Brand>("/masterdata/brands", {
      method: "POST",
      body: JSON.stringify({ name })
    });
  },
  async updateBrand(id: number, name: string): Promise<Brand> {
    return request<Brand>(`/masterdata/brands/${id}`, {
      method: "PUT",
      body: JSON.stringify({ name })
    });
  },
  async deleteBrand(id: number): Promise<boolean> {
    return request<boolean>(`/masterdata/brands/${id}`, {
      method: "DELETE"
    });
  },

  // Master Data - Products
  async getProducts(): Promise<Product[]> {
    return request<Product[]>("/masterdata/products");
  },
  async addProduct(product: Omit<Product, "id">): Promise<Product> {
    return request<Product>("/masterdata/products", {
      method: "POST",
      body: JSON.stringify(product)
    });
  },
  async updateProduct(id: number, product: Partial<Product>): Promise<Product> {
    return request<Product>(`/masterdata/products/${id}`, {
      method: "PUT",
      body: JSON.stringify(product)
    });
  },
  async deleteProduct(id: number): Promise<boolean> {
    return request<boolean>(`/masterdata/products/${id}`, {
      method: "DELETE"
    });
  },

  // Master Data - Antivirus Prices
  async getAntivirusPrices(): Promise<AntivirusPrice[]> {
    return request<AntivirusPrice[]>("/masterdata/antivirus-prices");
  },
  async addAntivirusPrice(price: Omit<AntivirusPrice, "id">): Promise<AntivirusPrice> {
    return request<AntivirusPrice>("/masterdata/antivirus-prices", {
      method: "POST",
      body: JSON.stringify(price)
    });
  },
  async updateAntivirusPrice(id: number, price: Partial<AntivirusPrice>): Promise<AntivirusPrice> {
    return request<AntivirusPrice>(`/masterdata/antivirus-prices/${id}`, {
      method: "PUT",
      body: JSON.stringify(price)
    });
  },
  async deleteAntivirusPrice(id: number): Promise<boolean> {
    return request<boolean>(`/masterdata/antivirus-prices/${id}`, {
      method: "DELETE"
    });
  },

  // Master Data - Business Types
  async getBusinessTypes(): Promise<Category[]> {
    // mapped to a similar id/name structure in UI
    const list = await request<{ id: number; type: string; dtl: string }[]>("/masterdata/business-types");
    return list.map((bt) => ({ id: bt.id, name: bt.type }));
  },
  async addBusinessType(type: string): Promise<any> {
    return request<any>("/masterdata/business-types", {
      method: "POST",
      body: JSON.stringify({ type })
    });
  },
  async updateBusinessType(id: number, type: string): Promise<any> {
    return request<any>(`/masterdata/business-types/${id}`, {
      method: "PUT",
      body: JSON.stringify({ type })
    });
  },
  async deleteBusinessType(id: number): Promise<boolean> {
    return request<boolean>(`/masterdata/business-types/${id}`, {
      method: "DELETE"
    });
  },

  // Master Data - Categories
  async getCategories(): Promise<Category[]> {
    return request<Category[]>("/masterdata/categories");
  },
  async addCategory(name: string): Promise<Category> {
    return request<Category>("/masterdata/categories", {
      method: "POST",
      body: JSON.stringify({ name })
    });
  },
  async updateCategory(id: number, name: string): Promise<Category> {
    return request<Category>(`/masterdata/categories/${id}`, {
      method: "PUT",
      body: JSON.stringify({ name })
    });
  },
  async deleteCategory(id: number): Promise<boolean> {
    return request<boolean>(`/masterdata/categories/${id}`, {
      method: "DELETE"
    });
  },

  // Master Data - Competitors
  async getCompetitors(): Promise<Competitor[]> {
    return request<Competitor[]>("/masterdata/competitors");
  },
  async addCompetitor(comp: Omit<Competitor, "id">): Promise<Competitor> {
    return request<Competitor>("/masterdata/competitors", {
      method: "POST",
      body: JSON.stringify(comp)
    });
  },
  async updateCompetitor(id: number, comp: Partial<Competitor>): Promise<Competitor> {
    return request<Competitor>(`/masterdata/competitors/${id}`, {
      method: "PUT",
      body: JSON.stringify(comp)
    });
  },
  async deleteCompetitor(id: number): Promise<boolean> {
    return request<boolean>(`/masterdata/competitors/${id}`, {
      method: "DELETE"
    });
  },

  // Master Data - Profiles
  async getProfiles(): Promise<Profile[]> {
    return request<Profile[]>("/masterdata/profiles");
  },
  async addProfile(profile: Omit<Profile, "id">): Promise<Profile> {
    return request<Profile>("/masterdata/profiles", {
      method: "POST",
      body: JSON.stringify(profile)
    });
  },
  async updateProfile(id: number, profile: Partial<Profile>): Promise<Profile> {
    return request<Profile>(`/masterdata/profiles/${id}`, {
      method: "PUT",
      body: JSON.stringify(profile)
    });
  },
  async deleteProfile(id: number): Promise<boolean> {
    return request<boolean>(`/masterdata/profiles/${id}`, {
      method: "DELETE"
    });
  },

  // Reports
  async getReports(): Promise<{ projectLedger: any[]; agentPerformance: any[] }> {
    return request<{ projectLedger: any[]; agentPerformance: any[] }>("/customers/reports/all");
  },

  // Customer Import API calls
  async previewCustomerImport(file: File): Promise<{
    columns: string[];
    sampleRows: Record<string, string>[];
    totalRows: number;
    fileId: string;
  }> {
    const formData = new FormData();
    formData.append("file", file);
    const response = await fetch(`${API_BASE}/import/customers/preview`, {
      method: "POST",
      body: formData
    });
    if (!response.ok) {
      const errorText = await response.text();
      throw new ApiError(response.status, errorText || `API error ${response.status}`);
    }
    return response.json();
  },

  async suggestMappings(columns: string[]): Promise<{ mappings: { column: string; targetField: string }[]; confidence: number }> {
    return request<{ mappings: { column: string; targetField: string }[]; confidence: number }>("/import/customers/suggest-mappings", {
      method: "POST",
      body: JSON.stringify({ columns })
    });
  },

  async extractUnstructuredData(text: string): Promise<any> {
    return request<any>("/import/customers/extract-unstructured", {
      method: "POST",
      body: JSON.stringify({ text })
    });
  },

  async validateImportFilePage(
    fileId: string,
    page: number,
    pageSize: number,
    mappings: Record<string, string>,
    policy: string,
    mappingConfidence: number
  ): Promise<{ rows: any[]; summary: any }> {
    return request<{ rows: any[]; summary: any }>("/import/customers/validate-file", {
      method: "POST",
      body: JSON.stringify({ fileId, page, pageSize, mappings, policy, mappingConfidence })
    });
  },

  async validateImportRows(rows: any[]): Promise<{ rows: any[]; summary: any }> {
    const normalized = rows.map((r: any) => ({
      name: r.name,
      address: r.address,
      phone: r.phone,
      capital: r.capital,
      businessType: r.businessType !== undefined ? r.businessType : r.business_type,
      contactName: r.contactName !== undefined ? r.contactName : r.contact_name,
      contactEmail: r.contactEmail !== undefined ? r.contactEmail : r.contact_email,
      contactTel: r.contactTel !== undefined ? r.contactTel : r.contact_tel,
      contactPosition: r.contactPosition !== undefined ? r.contactPosition : r.contact_position,
      code: r.code,
      unstructuredCompanyInfo: r.unstructuredCompanyInfo !== undefined ? r.unstructuredCompanyInfo : r.unstructured_company_info
    }));
    return request<{ rows: any[]; summary: any }>("/import/customers/validate", {
      method: "POST",
      body: JSON.stringify(normalized)
    });
  },

  async previewCustomerImportPage(fileId: string, page: number, pageSize: number): Promise<Record<string, string>[]> {
    return request<Record<string, string>[]>(`/import/customers/preview-page?fileId=${encodeURIComponent(fileId)}&page=${page}&pageSize=${pageSize}`);
  },

  async explainIssue(
    issueType: string,
    fieldName: string,
    fieldValue: string,
    issueDetails: string,
    matchedCustomerDetails?: string
  ): Promise<{ explanation: string }> {
    return request<{ explanation: string }>("/import/customers/explain-issue", {
      method: "POST",
      body: JSON.stringify({ issueType, fieldName, fieldValue, issueDetails, matchedCustomerDetails })
    });
  },

  async commitImportRowsStream(
    payload: {
      fileId: string;
      mappings: Record<string, string>;
      saleId: number | null;
      telesaleId: number | null;
      fileName: string;
      rowOverrides: Record<number, string>;
    },
    onProgress: (progress: any) => void,
    signal?: AbortSignal
  ): Promise<void> {
    const url = `${API_BASE}/import/customers/commit-stream`;
    const response = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(payload),
      signal
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new ApiError(response.status, errorText || `API error ${response.status}`);
    }

    const reader = response.body?.getReader();
    if (!reader) {
      throw new Error("Response body is not readable.");
    }

    const decoder = new TextDecoder("utf-8");
    let buffer = "";

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() || "";

        for (const line of lines) {
          if (line.trim()) {
            try {
              const progress = JSON.parse(line);
              onProgress(progress);
            } catch (err) {
              console.error("Failed to parse progress chunk:", err, line);
            }
          }
        }
      }

      if (buffer.trim()) {
        try {
          const progress = JSON.parse(buffer);
          onProgress(progress);
        } catch (err) {
          console.error("Failed to parse final progress chunk:", err, buffer);
        }
      }
    } finally {
      reader.releaseLock();
    }
  },

  async commitImportRows(payload: {
    rows: any[];
    saleId: number | null;
    telesaleId: number | null;
    fileName: string;
  }): Promise<any> {
    return request<any>("/import/customers/commit", {
      method: "POST",
      body: JSON.stringify(payload)
    });
  },

  exportErrorsUrl(fileId: string, mappings: Record<string, string>): string {
    const mappingsJson = JSON.stringify(mappings);
    return `${API_BASE}/import/customers/export-errors?fileId=${encodeURIComponent(fileId)}&mappingsJson=${encodeURIComponent(mappingsJson)}`;
  },

  async getImportHistory(): Promise<any[]> {
    return request<any[]>("/import/history");
  },

  async getImportHistoryPaginated(params: { page: number; pageSize: number }): Promise<PaginatedResponse<any>> {
    const res = await request<any>(`/import/history?page=${params.page}&pageSize=${params.pageSize}`);
    if (res && Array.isArray(res)) {
      return {
        items: res,
        totalCount: res.length,
        page: params.page,
        pageSize: params.pageSize,
        totalPages: Math.ceil(res.length / params.pageSize)
      };
    }
    return res;
  }
};
