import { describe, expect, it } from "vitest";
import {
  customerMatchesQuickFilter,
  getCustomerMissingFields,
  hasProductLicenseCompletenessData,
  type CustomerQuickFilter
} from "./customerCompleteness";
import type { Customer } from "./types";

const baseCustomer = {
  id: 1,
  name: "ACME",
  address: "Bangkok",
  phone: "021234567",
  subdistrict: "Bang Rak",
  district: "Bang Rak",
  province: "Bangkok",
  postal_code: "10500",
  telesale_id: null,
  sale_id: null,
  status: "New",
  is_active: true,
  start_dt: null,
  bt_type: "Technology",
  renewalDays: 30,
  hasCostSheet: false,
  updatedAt: "2026-06-17",
  primary_contact_name: "Suda",
  primary_contact_email: "suda@example.com",
  primary_contact_tel: "0812345678",
  hasProductLicenseInfo: true
} satisfies Customer;

const customer = (overrides: Partial<Customer> = {}): Customer => ({
  ...baseCustomer,
  ...overrides
});

describe("getCustomerMissingFields", () => {
  it("returns no missing fields for complete available customer data", () => {
    expect(getCustomerMissingFields(customer())).toEqual([]);
  });

  it("marks phone missing only when company and primary contact phones are empty", () => {
    expect(getCustomerMissingFields(customer({ phone: "", primary_contact_tel: "" }))).toContain("phone");
    expect(getCustomerMissingFields(customer({ phone: "", primary_contact_tel: "0812345678" }))).not.toContain("phone");
  });

  it("detects missing contact, business type, address, email, and product license data", () => {
    expect(
      getCustomerMissingFields(
        customer({
          primary_contact_name: "",
          bt_type: "",
          address: "   ",
          primary_contact_email: null,
          hasProductLicenseInfo: false
        })
      )
    ).toEqual([
      "contact",
      "businessType",
      "address",
      "email",
      "productLicense"
    ]);
  });

  it("only treats missing main address as no address for quick filtering", () => {
    expect(customerMatchesQuickFilter(customer({ address: "" }), "noAddress")).toBe(true);
    expect(customerMatchesQuickFilter(customer({ address: "Bangkok", subdistrict: "" }), "noAddress")).toBe(false);
    expect(customerMatchesQuickFilter(customer({ address: "Bangkok", district: "" }), "noAddress")).toBe(false);
    expect(customerMatchesQuickFilter(customer({ address: "Bangkok", province: "" }), "noAddress")).toBe(false);
    expect(customerMatchesQuickFilter(customer({ address: "Bangkok", postal_code: "" }), "noAddress")).toBe(false);
  });

  it("does not require product license data when the response omits that capability", () => {
    const { hasProductLicenseInfo: _ignored, ...withoutProductFlag } = customer();

    expect(getCustomerMissingFields(withoutProductFlag)).not.toContain("productLicense");
  });
});

describe("customer quick filters", () => {
  it.each<CustomerQuickFilter>(["all", "complete", "incomplete", "noPhone", "noContact", "noBusinessType", "noAddress", "noEmail", "noProductLicense"])(
    "evaluates %s without throwing",
    (filter) => {
      expect(typeof customerMatchesQuickFilter(customer({ phone: "", hasProductLicenseInfo: false }), filter)).toBe("boolean");
    }
  );

  it("identifies whether product/license quick filtering can be shown for a result set", () => {
    expect(hasProductLicenseCompletenessData([customer()])).toBe(true);
    expect(hasProductLicenseCompletenessData([customer({ hasProductLicenseInfo: undefined })])).toBe(false);
  });
});
