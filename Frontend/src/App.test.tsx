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
              start_dt: "2026-06-01"
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
              start_dt: "2026-06-03"
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
              start_dt: "2026-06-04"
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

    expect(screen.getByRole("heading", { name: /customer workspace/i })).toBeInTheDocument();
    expect(screen.getByRole("navigation", { name: /primary navigation/i })).toHaveTextContent("Master Data");
    expect(screen.getByRole("navigation", { name: /primary navigation/i })).toHaveTextContent("Cost Sheet");
    expect(screen.getByText("Total Customers")).toBeInTheDocument();
    expect(screen.getByText("Pending Cost Sheets")).toBeInTheDocument();

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
});
