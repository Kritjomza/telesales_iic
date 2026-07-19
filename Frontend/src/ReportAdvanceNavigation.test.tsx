import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";

describe("report renewal advance navigation", () => {
  beforeEach(() => {
    localStorage.setItem("ats_user", JSON.stringify({
      id: 1,
      username: "AR9999",
      name: "Narin Admin",
      email: "narin@iic.co.th",
      roles: "Super Admin",
      avatar: "NA"
    }));
  });

  it("opens the existing Manage call-status confirmation from Summary Renewal", async () => {
    const reportCustomer = {
      id: 7,
      name: "Renewal Customer",
      address: "Bangkok",
      telesale_id: null,
      sale_id: null,
      status: "Called",
      is_active: true,
      start_dt: "2026-01-01",
      bt_type: "Commercial",
      renewalDays: 7,
      hasCostSheet: false,
      updatedAt: "2026-07-19"
    };
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
        return Promise.resolve({ ok: true, text: () => Promise.resolve(JSON.stringify([reportCustomer])) });
      }
      if (url.includes("/api/reports")) {
        return Promise.resolve({ ok: true, text: () => Promise.resolve(JSON.stringify({ projectLedger: [] })) });
      }
      if (url.includes("/api/masterdata/business-types") || url.includes("/api/masterdata/competitors") || url.includes("/api/users")) {
        return Promise.resolve({ ok: true, text: () => Promise.resolve("[]") });
      }
      return Promise.resolve({ ok: true, text: () => Promise.resolve("[]") });
    }));
    const user = userEvent.setup();

    render(<App />);

    await screen.findByText("Customer Manage");
    await user.click(screen.getByRole("button", { name: "Report" }));
    await user.click(screen.getByRole("button", { name: "Summary Renewal" }));
    await user.click(await screen.findByRole("button", { name: "Open advance data for Renewal Customer" }));

    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Called" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Not Called" })).toBeInTheDocument();
  });
});
