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
  primary_contact_tel_office: "021234567",
  hasProductLicenseInfo: true
} satisfies Customer & { primary_contact_tel_office?: string | null };

const customer = (overrides: Partial<Customer> & { primary_contact_tel_office?: string | null } = {}): Customer => ({
  ...baseCustomer,
  ...overrides
});

describe("getCustomerMissingFields", () => {
  it("returns no missing fields for complete available customer data", () => {
    expect(getCustomerMissingFields(customer())).toEqual([]);
  });

  it("checks only primary contact name, email, mobile tel, and office tel", () => {
    expect(
      getCustomerMissingFields(
        customer({
          phone: "",
          bt_type: "",
          address: "",
          hasProductLicenseInfo: false
        })
      )
    ).toEqual([]);
  });

  it("detects missing primary contact name, email, mobile tel, and office tel", () => {
    expect(
      getCustomerMissingFields(
        customer({
          primary_contact_name: "",
          primary_contact_email: null,
          primary_contact_tel: " ",
          primary_contact_tel_office: ""
        })
      )
    ).toEqual([
      "contact",
      "email",
      "phone",
      "officePhone"
    ]);
  });
});

describe("customer quick filters", () => {
  it.each<CustomerQuickFilter>(["all", "complete", "incomplete", "noPhone", "noContact", "noBusinessType", "noAddress", "noEmail", "noOfficePhone", "noProductLicense"])(
    "evaluates %s without throwing",
    (filter) => {
      expect(typeof customerMatchesQuickFilter(customer({ primary_contact_tel: "", primary_contact_tel_office: "" }), filter)).toBe("boolean");
    }
  );

  it("matches only contact-field filters for missing contact data", () => {
    const missingContact = customer({
      primary_contact_name: "",
      primary_contact_email: "",
      primary_contact_tel: "",
      primary_contact_tel_office: ""
    });

    expect(customerMatchesQuickFilter(missingContact, "incomplete")).toBe(true);
    expect(customerMatchesQuickFilter(missingContact, "noContact")).toBe(true);
    expect(customerMatchesQuickFilter(missingContact, "noEmail")).toBe(true);
    expect(customerMatchesQuickFilter(missingContact, "noPhone")).toBe(true);
    expect(customerMatchesQuickFilter(missingContact, "noOfficePhone")).toBe(true);
    expect(customerMatchesQuickFilter(customer({ address: "" }), "noAddress")).toBe(false);
    expect(customerMatchesQuickFilter(customer({ bt_type: "" }), "noBusinessType")).toBe(false);
    expect(customerMatchesQuickFilter(customer({ hasProductLicenseInfo: false }), "noProductLicense")).toBe(false);
  });

  it("identifies whether product/license quick filtering can be shown for a result set", () => {
    expect(hasProductLicenseCompletenessData([customer()])).toBe(true);
    expect(hasProductLicenseCompletenessData([customer({ hasProductLicenseInfo: undefined })])).toBe(false);
  });
});
