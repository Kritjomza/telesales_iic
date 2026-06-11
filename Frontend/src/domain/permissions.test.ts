import { describe, expect, it } from "vitest";
import { canAccessGroup, canAccessView, normalizeRole } from "./permissions";

describe("role permissions", () => {
  it("normalizes Tele Sale role variants", () => {
    expect(normalizeRole("Tele sale")).toBe("Tele Sale");
    expect(normalizeRole("Tele Sale")).toBe("Tele Sale");
    expect(normalizeRole("telesale")).toBe("Tele Sale");
  });

  it("allows Super Admin to access every protected menu group", () => {
    expect(canAccessGroup("Master Data", "Super Admin")).toBe(true);
    expect(canAccessGroup("Customer", "Super Admin")).toBe(true);
    expect(canAccessGroup("Report", "Super Admin")).toBe(true);
    expect(canAccessGroup("Sale Manager", "Super Admin")).toBe(true);
  });

  it("limits Tele Sale to daily workflow menus", () => {
    expect(canAccessGroup("Customer", "Tele sale")).toBe(true);
    expect(canAccessView("manage", "Tele sale")).toBe(true);
    expect(canAccessView("booking", "Tele sale")).toBe(true);
    expect(canAccessGroup("Master Data", "Tele sale")).toBe(false);
    expect(canAccessGroup("Report", "Tele sale")).toBe(false);
  });

  it("denies missing or unknown roles", () => {
    expect(canAccessView("manage", "")).toBe(false);
    expect(canAccessView("manage", "Unknown")).toBe(false);
  });
});
