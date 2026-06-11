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
  CostSheet
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

  if (response.status === 401) {
    if (onUnauthorizedCallback) {
      onUnauthorizedCallback();
    }
    throw new Error("Session expired. Please log in again.");
  }

  if (response.status === 403) {
    if (onForbiddenCallback) {
      onForbiddenCallback();
    }
    throw new Error("Access denied. You do not have permission to perform this action.");
  }

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
          errorMessage = "An internal server error occurred. Please contact support.";
        }
      }
    } catch {
      // Ignored
    }
    throw new Error(errorMessage);
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
  async getCustomers(): Promise<Customer[]> {
    return request<Customer[]>("/customers");
  },

  async addCustomer(customer: Omit<Customer, "id" | "updatedAt" | "renewalDays" | "hasCostSheet">): Promise<Customer> {
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
  }
};
