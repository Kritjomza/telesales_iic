import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";
import App from "./App";

describe("ATS React workspace", () => {
  beforeEach(() => {
    localStorage.setItem("ats_user", JSON.stringify({
      id: 1,
      username: "AR9999",
      name: "Narin Admin",
      email: "narin@iic.co.th",
      roles: "Super Admin",
      avatar: "NA"
    }));

    vi.stubGlobal("fetch", vi.fn().mockImplementation((url: string) => {
      if (url.includes("/api/customers")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify([
            {
              id: 1,
              name: "Apex Manufacturing",
              address: "Bangkok",
              bt_type: "Commercial",
              sale_id: 1,
              telesale_id: 2,
              status: "Assigned",
              renewalDays: 45,
              hasCostSheet: false,
              is_active: true,
              start_dt: "2026-06-01",
              primary_contact_name: "Nok",
              primary_contact_email: "nok@example.com",
              primary_contact_tel: "0812345678",
              primary_contact_tel_office: ""
            },
            {
              id: 2,
              name: "Siam Healthcare",
              address: "Chiang Mai",
              bt_type: "Government",
              sale_id: null,
              telesale_id: null,
              status: "New",
              renewalDays: 7,
              hasCostSheet: false,
              is_active: true,
              start_dt: "2026-06-03",
              primary_contact_name: "",
              primary_contact_email: "",
              primary_contact_tel: "",
              primary_contact_tel_office: ""
            },
            {
              id: 3,
              name: "Arrow Retail",
              address: "Bangkok",
              bt_type: "Commercial",
              sale_id: 3,
              telesale_id: 4,
              status: "Sent",
              renewalDays: 12,
              hasCostSheet: true,
              is_active: true,
              start_dt: "2026-06-04",
              primary_contact_name: "Pim",
              primary_contact_email: "",
              primary_contact_tel: "0899999999",
              primary_contact_tel_office: ""
            }
          ]))
        });
      }
      if (url.includes("/api/users")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify([
            { id: 1, name: "Narin", roles: "Sale" },
            { id: 2, name: "May", roles: "Tele sale" },
            { id: 3, name: "Ploy", roles: "Sale" },
            { id: 4, name: "June", roles: "Tele sale" }
          ]))
        });
      }
      if (url.includes("/api/masterdata/business-types")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify([
            { id: 1, type: "Commercial", dtl: "Commercial Business" },
            { id: 2, type: "Government", dtl: "Government Org" }
          ]))
        });
      }
      if (url.includes("/api/masterdata/competitors")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify([]))
        });
      }
      if (url.includes("/api/auth/me")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify({
            id: 1,
            username: "AR9999",
            name: "Narin Admin",
            email: "narin@iic.co.th",
            roles: "Super Admin",
            avatar: "NA"
          }))
        });
      }
      return Promise.resolve({ ok: true, text: () => Promise.resolve("") });
    }));
  });

  it("renders the migrated customer workspace shell and filters rows", async () => {
    const user = userEvent.setup();

    render(<App />);

    // Wait for the data to load
    await screen.findByText("Apex Manufacturing");

    expect(screen.getByRole("heading", { name: /customer manage/i })).toBeInTheDocument();
    expect(screen.getByRole("navigation", { name: /primary navigation/i })).toHaveTextContent("Master Data");
    expect(screen.getByRole("navigation", { name: /primary navigation/i })).not.toHaveTextContent("Cost Sheet");
    expect(screen.getByRole("navigation", { name: /primary navigation/i })).not.toHaveTextContent("Booking");
    expect(screen.getByText("Total Customers")).toBeInTheDocument();
    expect(screen.getByText("Complete Data")).toBeInTheDocument();
    expect(screen.getByText("Incomplete Data")).toBeInTheDocument();

    await user.type(screen.getByLabelText(/customer name or address/i), "bangkok");
    await user.selectOptions(screen.getByLabelText(/business type/i), "Commercial");
    await user.click(screen.getByRole("button", { name: /search/i }));

    const table = screen.getByRole("table", { name: /customer records/i });
    expect(within(table).getByText("Apex Manufacturing")).toBeInTheDocument();
    expect(within(table).getByText("Arrow Retail")).toBeInTheDocument();
    expect(within(table).queryByText("Siam Healthcare")).not.toBeInTheDocument();
  });

  it("labels row workflow actions with the customer name", async () => {
    render(<App />);

    await screen.findByText("Apex Manufacturing");

    expect(
      screen.getByRole("button", { name: /open advance data for apex manufacturing/i })
    ).toBeInTheDocument();
  });

  it("renders customers when optional lookup data fails to load", async () => {
    vi.stubGlobal("fetch", vi.fn().mockImplementation((url: string) => {
      if (url.includes("/api/auth/me")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify({
            id: 1,
            username: "AR9999",
            name: "Narin Admin",
            email: "narin@iic.co.th",
            roles: "Super Admin",
            avatar: "NA"
          }))
        });
      }
      if (url.includes("/api/customers")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify([
            {
              id: 1,
              name: "Apex Manufacturing",
              address: "Bangkok",
              bt_type: "Commercial",
              sale_id: null,
              telesale_id: null,
              status: "New",
              renewalDays: 45,
              hasCostSheet: false,
              is_active: true,
              start_dt: "2026-06-01"
            }
          ]))
        });
      }
      if (url.includes("/api/users")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify([]))
        });
      }
      if (url.includes("/api/masterdata/business-types")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify([
            { id: 1, type: "Commercial", dtl: "Commercial Business" }
          ]))
        });
      }
      if (url.includes("/api/masterdata/competitors")) {
        return Promise.resolve({
          status: 500,
          ok: false,
          text: () => Promise.resolve("Competitor lookup failed")
        });
      }
      return Promise.resolve({ ok: true, text: () => Promise.resolve("") });
    }));

    render(<App />);

    await screen.findByText("Apex Manufacturing");
    expect(screen.queryByText("Failed to load initial customer data")).not.toBeInTheDocument();
  });

  it("blocks unauthorized users and hides restricted sidebar links", async () => {
    localStorage.setItem("ats_user", JSON.stringify({
      id: 3,
      username: "Agent00",
      name: "Sale Agent",
      roles: "Sale",
      avatar: "SA"
    }));

    vi.stubGlobal("fetch", vi.fn().mockImplementation((url: string) => {
      if (url.includes("/api/auth/me")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify({
            id: 3,
            username: "Agent00",
            name: "Sale Agent",
            roles: "Sale",
            avatar: "SA"
          }))
        });
      }
      return Promise.resolve({ ok: true, text: () => Promise.resolve("[]") });
    }));

    render(<App />);

    await screen.findByText("Sale Agent");

    const nav = screen.getByRole("navigation", { name: /primary navigation/i });
    expect(nav).not.toHaveTextContent("Master Data");
  });

  it("handles 401 Unauthorized API responses by clearing session and redirecting to login", async () => {
    vi.stubGlobal("fetch", vi.fn().mockImplementation((url: string) => {
      if (url.includes("/api/auth/me")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify({
            id: 1,
            username: "AR9999",
            name: "Narin Admin",
            roles: "Super Admin",
            avatar: "NA"
          }))
        });
      }
      if (url.includes("/api/customers")) {
        return Promise.resolve({
          status: 401,
          ok: false,
          text: () => Promise.resolve("Unauthorized")
        });
      }
      return Promise.resolve({ ok: true, text: () => Promise.resolve("") });
    }));

    render(<App />);

    await screen.findByRole("heading", { name: /sign in/i });
    expect(localStorage.getItem("ats_user")).toBeNull();
  });

  it("handles 403 Forbidden API responses by rendering Access Denied view", async () => {
    vi.stubGlobal("fetch", vi.fn().mockImplementation((url: string) => {
      if (url.includes("/api/auth/me")) {
        return Promise.resolve({
          ok: true,
          text: () => Promise.resolve(JSON.stringify({
            id: 1,
            username: "AR9999",
            name: "Narin Admin",
            roles: "Super Admin",
            avatar: "NA"
          }))
        });
      }
      if (url.includes("/api/customers")) {
        return Promise.resolve({
          status: 403,
          ok: false,
          text: () => Promise.resolve("Forbidden")
        });
      }
      return Promise.resolve({ ok: true, text: () => Promise.resolve("") });
    }));

    render(<App />);

    await screen.findByText("Access Denied");
    expect(screen.getByText("Reload Console")).toBeInTheDocument();
  });

  it("renders completeness filter chips, missing fields select, and status badges", async () => {
    const user = userEvent.setup();
    render(<App />);

    // Wait for the data to load
    await screen.findByText("Apex Manufacturing");

    // 1. Verify filter elements are in the document
    expect(screen.getByText("Completeness:")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^all$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^incomplete$/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^complete$/i })).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: /filter by missing field/i })).toBeInTheDocument();

    // 2. Verify status badge is displayed in each row
    // Since Apex Manufacturing is incomplete (lacks office tel), it should have an "Incomplete" badge
    const rows = screen.getAllByRole("row");
    const apexRow = rows.find(r => r.textContent?.includes("Apex Manufacturing"));
    expect(apexRow).toBeDefined();
    const incompleteBadge = within(apexRow!).getByRole("button", { name: /show missing fields for apex manufacturing/i });
    expect(incompleteBadge).toHaveTextContent("Incomplete");

    // 3. Click the incomplete badge for Apex Manufacturing and verify popover opens
    await user.click(incompleteBadge);

    // Verify popover displays missing fields
    expect(screen.getByRole("heading", { name: /missing fields/i })).toBeInTheDocument();
    expect(screen.getByText("Office Tel")).toBeInTheDocument();
    expect(screen.queryByText("Phone")).not.toBeInTheDocument();
    expect(screen.queryByText("Address")).not.toBeInTheDocument();

    // 4. Click the "Complete" filter pill
    const completePill = screen.getByRole("button", { name: /^complete$/i });
    await user.click(completePill);

    // Since all three mock customers are incomplete, the table should show No customers found.
    expect(screen.getByText("No customers found.")).toBeInTheDocument();
  });
});
