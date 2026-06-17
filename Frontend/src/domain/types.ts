export interface User {
  id: number;
  username: string;
  name: string;
  email: string;
  roles: "Admin" | "Super Admin" | "Manager" | "Supervisor" | "Sale" | "Tele Sale" | "Tele sale" | "Viewer";
  tel?: string;
  position?: string;
  linetoken?: string;
}

export interface Customer {
  id: number;
  name: string;
  address: string;
  capital?: string;
  telesale_id: number | null;
  telesale?: string | null;
  sale_id: number | null;
  sale?: string | null;
  status: string;
  is_active: boolean;
  start_dt: string | null;
  bt_type: string;
  renewalDays: number;
  hasCostSheet: boolean;
  updatedAt: string;
  phone?: string | null;
  subdistrict?: string | null;
  district?: string | null;
  province?: string | null;
  postal_code?: string | null;
  primary_contact_name?: string | null;
  primary_contact_tel?: string | null;
  primary_contact_email?: string | null;
  hasProductLicenseInfo?: boolean;
  matchedField?: string | null;
}

export interface ContactDetail {
  id: number;
  cust_id: number;
  contact_name: string;
  contact_email: string;
  contact_tel: string;
  contact_tel_office?: string;
  point: number;
  total_point: number;
}

export interface DetailDevice {
  id: number;
  dtl_id: number;
  full_name: string;
  full_name2?: string;
  dtl?: string;
  desktop_qty: number;
  server_qty: number;
  equipment_expire: string | null;
  point: number;
  progress_status: string;
  competitor_name?: string;
}

export interface DetailProject {
  id: number;
  dtl_id: number;
  dtl: string;
  close_date: string | null;
  point: number;
  progress_status: string;
}

export interface Brand {
  id: number;
  name: string;
  code: string;
}

export interface Product {
  id: number;
  brand_id: number;
  category_id: number;
  name: string;
  cost: number;
  price: number;
}

export interface AntivirusPrice {
  id: number;
  brand_id: number;
  brand: string;
  code: string;
  edition: string;
  start: number;
  end: number;
  cost: number;
  types: string;
}

export interface Category {
  id: number;
  name: string;
}

export interface Competitor {
  id: number;
  name: string;
  year: number;
  amt: number;
  compare: "Smaller" | "Equal" | "Larger";
}

export interface Profile {
  id: number;
  name: string;
  type: string;
  item: string;
  edition: string;
}

export interface CostSheet {
  id: number;
  cust_id: number;
  project_name: string;
  brand_id: number;
  product_id: number;
  qty: number;
  cost_price: number;
  sale_price: number;
  discount: number;
  margin: number;
  gp_amount: number;
  gp_percent: number;
  owner_share_percent: number;
  employee_share_percent: number;
  status: "Pending" | "Approved" | "Rejected";
  created_at: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  metrics?: {
    total: number;
    unassigned: number;
    nearRenewal: number;
    pendingCostSheets: number;
  };
}
