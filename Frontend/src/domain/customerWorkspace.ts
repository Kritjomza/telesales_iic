export type AssignmentFilter = "all" | "assigned" | "unassigned";

export type CustomerStatus = "New" | "Assigned" | "Booking" | "Wait" | "Sent" | "Win" | "Lost";

export type CustomerRecord = {
  id: number;
  name: string;
  address: string;
  businessType: string;
  sale: string | null;
  telesale: string | null;
  status: CustomerStatus;
  renewalDays: number;
  hasCostSheet: boolean;
  updatedAt: string;
};

export type CustomerFilters = {
  query?: string;
  businessType?: string;
  saleStatus?: AssignmentFilter;
  telesaleStatus?: AssignmentFilter;
};

export type CustomerMetrics = {
  totalCustomers: number;
  unassigned: number;
  nearRenewal: number;
  pendingCostSheets: number;
};

const nearRenewalThresholdDays = 30;

function hasAssignee(customer: CustomerRecord, key: "sale" | "telesale"): boolean {
  return Boolean(customer[key]?.trim());
}

function matchesAssignment(customer: CustomerRecord, key: "sale" | "telesale", filter = "all"): boolean {
  if (filter === "all") {
    return true;
  }

  return filter === "assigned" ? hasAssignee(customer, key) : !hasAssignee(customer, key);
}

export function buildCustomerMetrics(customers: CustomerRecord[]): CustomerMetrics {
  return {
    totalCustomers: customers.length,
    unassigned: customers.filter((customer) => !hasAssignee(customer, "sale") && !hasAssignee(customer, "telesale")).length,
    nearRenewal: customers.filter((customer) => customer.renewalDays <= nearRenewalThresholdDays).length,
    pendingCostSheets: customers.filter((customer) => !customer.hasCostSheet).length
  };
}

export function filterCustomers(customers: CustomerRecord[], filters: CustomerFilters): CustomerRecord[] {
  const query = filters.query?.trim().toLowerCase();
  const businessType = filters.businessType?.trim().toLowerCase();

  return customers.filter((customer) => {
    const searchable = `${customer.name} ${customer.address}`.toLowerCase();
    const matchesText = query ? searchable.includes(query) : true;
    const matchesBusiness = businessType ? customer.businessType.toLowerCase() === businessType : true;

    return (
      matchesText &&
      matchesBusiness &&
      matchesAssignment(customer, "sale", filters.saleStatus) &&
      matchesAssignment(customer, "telesale", filters.telesaleStatus)
    );
  });
}
