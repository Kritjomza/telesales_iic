import type { Customer } from "./types";

export type MissingField =
  | "phone"
  | "contact"
  | "businessType"
  | "address"
  | "subdistrict"
  | "district"
  | "province"
  | "postalCode"
  | "email"
  | "productLicense";

export type CustomerQuickFilter =
  | "all"
  | "incomplete"
  | "complete"
  | "noPhone"
  | "noContact"
  | "noBusinessType"
  | "noAddress"
  | "noEmail"
  | "noProductLicense";

export const missingFieldLabels: Record<MissingField, string> = {
  phone: "Phone",
  contact: "Contact",
  businessType: "Business Type",
  address: "Address",
  subdistrict: "Subdistrict",
  district: "District",
  province: "Province",
  postalCode: "Postal Code",
  email: "Email",
  productLicense: "Product / License"
};

const hasValue = (value: unknown): boolean => {
  if (value === null || value === undefined) return false;
  return String(value).trim().length > 0;
};

const responseHasField = (customer: Customer, field: keyof Customer): boolean =>
  Object.prototype.hasOwnProperty.call(customer, field);

export const hasProductLicenseCompletenessData = (customers: Customer[]): boolean =>
  customers.some((customer) => typeof customer.hasProductLicenseInfo === "boolean");

export const getCustomerMissingFields = (customer: Customer): MissingField[] => {
  const missing: MissingField[] = [];

  if (!hasValue(customer.phone) && !hasValue(customer.primary_contact_tel)) {
    missing.push("phone");
  }

  if (responseHasField(customer, "primary_contact_name") && !hasValue(customer.primary_contact_name)) {
    missing.push("contact");
  }

  if (!hasValue(customer.bt_type) && !hasValue((customer as Customer & { businessTypeName?: unknown }).businessTypeName)) {
    missing.push("businessType");
  }

  if (!hasValue(customer.address)) {
    missing.push("address");
  }

  if (!hasValue(customer.subdistrict)) {
    missing.push("subdistrict");
  }

  if (!hasValue(customer.district)) {
    missing.push("district");
  }

  if (!hasValue(customer.province)) {
    missing.push("province");
  }

  if (!hasValue(customer.postal_code)) {
    missing.push("postalCode");
  }

  if (responseHasField(customer, "primary_contact_email") && !hasValue(customer.primary_contact_email)) {
    missing.push("email");
  }

  if (typeof customer.hasProductLicenseInfo === "boolean" && !customer.hasProductLicenseInfo) {
    missing.push("productLicense");
  }

  return missing;
};

export const customerMatchesQuickFilter = (customer: Customer, filter: CustomerQuickFilter): boolean => {
  if (filter === "all") return true;

  const missingFields = getCustomerMissingFields(customer);

  if (filter === "complete") return missingFields.length === 0;
  if (filter === "incomplete") return missingFields.length > 0;
  if (filter === "noPhone") return missingFields.includes("phone");
  if (filter === "noContact") return missingFields.includes("contact");
  if (filter === "noBusinessType") return missingFields.includes("businessType");
  if (filter === "noAddress") {
    return missingFields.some(field =>
      field === "address" ||
      field === "subdistrict" ||
      field === "district" ||
      field === "province" ||
      field === "postalCode"
    );
  }
  if (filter === "noEmail") return missingFields.includes("email");
  if (filter === "noProductLicense") return missingFields.includes("productLicense");

  return true;
};
