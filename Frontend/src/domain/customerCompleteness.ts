import type { Customer } from "./types";

export type MissingField =
  | "phone"
  | "contact"
  | "officePhone"
  | "email";

export type CustomerQuickFilter =
  | "all"
  | "incomplete"
  | "complete"
  | "noPhone"
  | "noContact"
  | "noOfficePhone"
  | "noEmail";

export const missingFieldLabels: Record<MissingField, string> = {
  phone: "Phone",
  contact: "Contact",
  officePhone: "Office Tel",
  email: "Email"
};

const hasValue = (value: unknown): boolean => {
  if (value === null || value === undefined) return false;
  return String(value).trim().length > 0;
};

const responseHasField = (customer: Customer, field: keyof Customer): boolean =>
  Object.prototype.hasOwnProperty.call(customer, field);

export const getCustomerMissingFields = (customer: Customer): MissingField[] => {
  const missing: MissingField[] = [];

  if (responseHasField(customer, "primary_contact_name") && !hasValue(customer.primary_contact_name)) {
    missing.push("contact");
  }

  if (responseHasField(customer, "primary_contact_email") && !hasValue(customer.primary_contact_email)) {
    missing.push("email");
  }

  if (responseHasField(customer, "primary_contact_tel") && !hasValue(customer.primary_contact_tel)) {
    missing.push("phone");
  }

  if (responseHasField(customer, "primary_contact_tel_office") && !hasValue(customer.primary_contact_tel_office)) {
    missing.push("officePhone");
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
  if (filter === "noOfficePhone") return missingFields.includes("officePhone");
  if (filter === "noEmail") return missingFields.includes("email");

  return true;
};
