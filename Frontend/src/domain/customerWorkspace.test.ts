import { describe, expect, it } from "vitest";
import {
  buildCustomerMetrics,
  filterCustomers,
  type CustomerRecord
} from "./customerWorkspace";

const customers: CustomerRecord[] = [
  {
    id: 1,
    name: "Apex Manufacturing",
    address: "Bangkok",
    businessType: "Commercial",
    sale: "Narin",
    telesale: "May",
    status: "Assigned",
    renewalDays: 45,
    hasCostSheet: false,
    updatedAt: "2026-06-01"
  },
  {
    id: 2,
    name: "Siam Healthcare",
    address: "Chiang Mai",
    businessType: "Government",
    sale: null,
    telesale: null,
    status: "New",
    renewalDays: 7,
    hasCostSheet: false,
    updatedAt: "2026-06-03"
  },
  {
    id: 3,
    name: "Arrow Retail",
    address: "Bangkok",
    businessType: "Commercial",
    sale: "Ploy",
    telesale: "June",
    status: "Sent",
    renewalDays: 12,
    hasCostSheet: true,
    updatedAt: "2026-06-04"
  }
];

describe("customer workspace data helpers", () => {
  it("builds operational metrics from the customer list", () => {
    expect(buildCustomerMetrics(customers)).toEqual({
      totalCustomers: 3,
      unassigned: 1,
      nearRenewal: 2,
      pendingCostSheets: 2
    });
  });

  it("filters by text, business, and assignment state", () => {
    const result = filterCustomers(customers, {
      query: "bangkok",
      businessType: "Commercial",
      saleStatus: "assigned",
      telesaleStatus: "assigned"
    });

    expect(result.map((customer) => customer.name)).toEqual([
      "Apex Manufacturing",
      "Arrow Retail"
    ]);
  });
});
