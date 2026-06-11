export type AppRole =
  | "Super Admin"
  | "Admin"
  | "Manager"
  | "Supervisor"
  | "Sale"
  | "Tele Sale"
  | "Tele sale"
  | "Viewer";

type CanonicalRole = "Super Admin" | "Admin" | "Manager" | "Supervisor" | "Sale" | "Tele Sale" | "Viewer" | "";

type Permission =
  | "masterData"
  | "customerManage"
  | "booking"
  | "reports"
  | "costSheet";

const rolePermissions: Record<Exclude<CanonicalRole, "">, Permission[]> = {
  "Super Admin": ["masterData", "customerManage", "booking", "reports", "costSheet"],
  Admin: ["masterData", "customerManage", "booking", "reports", "costSheet"],
  Manager: ["customerManage", "booking", "reports", "costSheet"],
  Supervisor: ["customerManage", "booking", "reports", "costSheet"],
  Sale: ["customerManage", "booking", "costSheet"],
  "Tele Sale": ["customerManage", "booking", "costSheet"],
  Viewer: ["customerManage", "booking", "reports"]
};

const groupPermissions: Record<string, Permission> = {
  "Master Data": "masterData",
  Customer: "customerManage",
  Report: "reports",
  "Sale Manager": "costSheet"
};

const viewPermissions: Record<string, Permission | "public"> = {
  forbidden: "public",
  "master-data": "masterData",
  manage: "customerManage",
  booking: "booking",
  reports: "reports",
  "cost-sheet": "costSheet"
};

export function normalizeRole(role?: string | null): CanonicalRole {
  switch ((role || "").trim().toLowerCase()) {
    case "super admin":
      return "Super Admin";
    case "admin":
      return "Admin";
    case "manager":
      return "Manager";
    case "supervisor":
      return "Supervisor";
    case "sale":
      return "Sale";
    case "tele sale":
    case "telesale":
    case "tele-sale":
      return "Tele Sale";
    case "viewer":
      return "Viewer";
    default:
      return "";
  }
}

export function hasPermission(role: string | null | undefined, permission: Permission): boolean {
  const normalized = normalizeRole(role);
  if (!normalized) return false;
  return rolePermissions[normalized].includes(permission);
}

export function isAdminRole(role: string | null | undefined): boolean {
  const normalized = normalizeRole(role);
  return normalized === "Admin" || normalized === "Super Admin";
}

export function isSupervisorRole(role: string | null | undefined): boolean {
  const normalized = normalizeRole(role);
  return normalized === "Manager" || normalized === "Supervisor";
}

export function isAgentRole(role: string | null | undefined): boolean {
  const normalized = normalizeRole(role);
  return normalized === "Sale" || normalized === "Tele Sale";
}

export function canManageAssignments(role: string | null | undefined): boolean {
  return isAdminRole(role) || isSupervisorRole(role);
}

export function canAccessGroup(groupLabel: string, role: string | null | undefined): boolean {
  const permission = groupPermissions[groupLabel];
  return permission ? hasPermission(role, permission) : false;
}

export function canAccessView(viewKey: string, role: string | null | undefined): boolean {
  const permission = viewPermissions[viewKey];
  if (permission === "public") return true;
  return permission ? hasPermission(role, permission) : false;
}

export function getRoleMenuMatrix() {
  return rolePermissions;
}
