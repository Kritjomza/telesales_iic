import { describe, expect, it, vi } from "vitest";
import { CustomerStatuses, DeviceStatuses, ProjectStatuses } from "./statuses";
import { canDeleteCustomer, canDeleteMasterData, canWriteMasterData } from "./permissions";
import { apiService, ApiError } from "./apiService";

describe("Frontend status constants", () => {
  it("defines the exact required canonical statuses", () => {
    expect(CustomerStatuses).toEqual(["New", "Wait", "Sent", "Win", "Lost"]);
    expect(DeviceStatuses).toEqual(["New", "Win", "Lost"]);
    expect(ProjectStatuses).toEqual(["Discuss", "Quotation", "Win", "Lost", "Hold", "Cancel"]);
  });
});

describe("Frontend delete and edit permissions", () => {
  it("only allows Super Admin to delete customers", () => {
    expect(canDeleteCustomer("Super Admin")).toBe(true);
    expect(canDeleteCustomer("Admin")).toBe(false);
    expect(canDeleteCustomer("Manager")).toBe(false);
    expect(canDeleteCustomer("Supervisor")).toBe(false);
    expect(canDeleteCustomer("Sale")).toBe(false);
    expect(canDeleteCustomer("Tele Sale")).toBe(false);
    expect(canDeleteCustomer("Viewer")).toBe(false);
  });

  it("verifies master data delete rules for Super Admin, Admin, and Manager", () => {
    expect(canDeleteMasterData("Super Admin", "categories")).toBe(true);
    expect(canDeleteMasterData("Admin", "brands")).toBe(false);
    expect(canDeleteMasterData("Manager", "brands")).toBe(true);
    expect(canDeleteMasterData("Manager", "products")).toBe(true);
    expect(canDeleteMasterData("Manager", "categories")).toBe(false);
    expect(canDeleteMasterData("Sale", "brands")).toBe(false);
  });

  it("verifies master data write rules for Super Admin, Admin, and Manager", () => {
    expect(canWriteMasterData("Super Admin", "categories")).toBe(true);
    expect(canWriteMasterData("Admin", "categories")).toBe(true);
    expect(canWriteMasterData("Manager", "brands")).toBe(true);
    expect(canWriteMasterData("Manager", "categories")).toBe(false);
    expect(canWriteMasterData("Sale", "brands")).toBe(false);
  });
});

describe("Frontend API error parsing", () => {
  it("parses 400 Bad Request error response message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      text: () => Promise.resolve(JSON.stringify({
        message: "Invalid project status 'New'. Allowed values: Discuss, Quotation, Win, Lost, Hold, Cancel."
      }))
    }));

    try {
      await apiService.deleteCustomer(1);
      expect(true).toBe(false); // should not reach here
    } catch (err: any) {
      expect(err).toBeInstanceOf(ApiError);
      expect(err.status).toBe(400);
      expect(err.message).toBe("Invalid project status 'New'. Allowed values: Discuss, Quotation, Win, Lost, Hold, Cancel.");
    }
  });

  it("parses 403 Forbidden error response message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false,
      status: 403,
      text: () => Promise.resolve(JSON.stringify({
        message: "You do not have permission to delete customer records."
      }))
    }));

    try {
      await apiService.deleteCustomer(1);
      expect(true).toBe(false); // should not reach here
    } catch (err: any) {
      expect(err).toBeInstanceOf(ApiError);
      expect(err.status).toBe(403);
      expect(err.message).toBe("You do not have permission to delete customer records.");
    }
  });
});
