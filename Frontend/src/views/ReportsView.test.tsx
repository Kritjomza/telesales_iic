import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { Customer } from "../domain/types";
import { ReportsView } from "./ReportsView";

const customer = (id: number, renewalDays = 45): Customer => ({
  id,
  name: `Customer ${id}`,
  address: `Address ${id}`,
  telesale_id: null,
  sale_id: null,
  status: "Called",
  is_active: true,
  start_dt: "2026-01-01",
  bt_type: "Commercial",
  renewalDays,
  hasCostSheet: false,
  updatedAt: "2026-07-19"
});

describe("ReportsView", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("paginates operation records without requesting assignment users", async () => {
    const customers = Array.from({ length: 26 }, (_, index) => customer(index + 1));
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("/api/customers")) {
        return Promise.resolve({ ok: true, text: () => Promise.resolve(JSON.stringify(customers)) });
      }
      if (url.includes("/api/reports")) {
        return Promise.resolve({ ok: true, text: () => Promise.resolve(JSON.stringify({ projectLedger: [] })) });
      }
      return Promise.resolve({ ok: true, text: () => Promise.resolve("[]") });
    });
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();

    render(<ReportsView activeTab="operation" />);

    await screen.findByText("Customer 1");
    expect(screen.queryByText("Customer 26")).not.toBeInTheDocument();
    expect(screen.getByText(/showing/i)).toHaveTextContent("1-25");
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes("/api/users"))).toBe(false);

    await user.click(screen.getByRole("button", { name: "Page 2" }));

    expect(await screen.findByText("Customer 26")).toBeInTheDocument();
    expect(screen.getByText(/showing/i)).toHaveTextContent("26-26");
  });

  it("offers an Advance action for renewal customers", async () => {
    const renewalCustomer = customer(7, 7);
    const onAdvanceCustomer = vi.fn();
    vi.stubGlobal("fetch", vi.fn().mockImplementation((url: string) => {
      if (url.includes("/api/customers")) {
        return Promise.resolve({ ok: true, text: () => Promise.resolve(JSON.stringify([renewalCustomer])) });
      }
      if (url.includes("/api/reports")) {
        return Promise.resolve({ ok: true, text: () => Promise.resolve(JSON.stringify({ projectLedger: [] })) });
      }
      return Promise.resolve({ ok: true, text: () => Promise.resolve("[]") });
    }));
    const user = userEvent.setup();

    render(<ReportsView activeTab="renewal" onAdvanceCustomer={onAdvanceCustomer} />);

    const advance = await screen.findByRole("button", { name: "Open advance data for Customer 7" });
    await user.click(advance);

    await waitFor(() => expect(onAdvanceCustomer).toHaveBeenCalledWith(renewalCustomer));
  });
});
