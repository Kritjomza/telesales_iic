import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MasterDataView } from "./MasterDataView";
import { apiService } from "../domain/apiService";

const users = [
  { id: 1, name: "Root", username: "root", email: "root@example.com", roles: "Super Admin", is_active: true },
  { id: 2, name: "Agent", username: "agent", email: "agent@example.com", roles: "Sale", is_active: true }
] as any[];

describe("MasterDataView user management", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(apiService, "getBrands").mockResolvedValue([]);
    vi.spyOn(apiService, "getProducts").mockResolvedValue([]);
    vi.spyOn(apiService, "getCategories").mockResolvedValue([]);
    vi.spyOn(apiService, "getUsers").mockResolvedValue(users);
  });

  it("shows user controls only to Admin and Super Admin", async () => {
    const { rerender } = render(<MasterDataView tableType="users" userRole="Sale" showToast={vi.fn()} />);
    await screen.findByText("Agent");
    expect(screen.queryByRole("button", { name: /add user/i })).not.toBeInTheDocument();

    rerender(<MasterDataView tableType="users" userRole="Admin" showToast={vi.fn()} />);
    expect(await screen.findByRole("button", { name: /add user/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /edit root/i })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /edit agent/i })).toBeInTheDocument();
  });

  it("creates a user and requires a password", async () => {
    const add = vi.spyOn(apiService as any, "addUser").mockResolvedValue(users[1]);
    render(<MasterDataView tableType="users" userRole="Super Admin" showToast={vi.fn()} />);
    await userEvent.click(await screen.findByRole("button", { name: /add user/i }));
    await userEvent.type(screen.getByLabelText(/^name \*$/i), "New User");
    await userEvent.type(screen.getByLabelText(/username/i), "new");
    await userEvent.type(screen.getByLabelText(/email/i), "new@example.com");
    await userEvent.click(screen.getByRole("button", { name: /save/i }));
    expect(add).not.toHaveBeenCalled();
    await userEvent.type(screen.getByLabelText(/password/i), "SecurePass1!");
    await userEvent.click(screen.getByRole("button", { name: /save/i }));
    await waitFor(() => expect(add).toHaveBeenCalledWith(expect.objectContaining({ username: "new", password: "SecurePass1!" })));
  });

  it("confirms deletion and reports API errors", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    const remove = vi.spyOn(apiService as any, "deleteUser").mockRejectedValue(new Error("User is referenced"));
    const toast = vi.fn();
    render(<MasterDataView tableType="users" userRole="Super Admin" showToast={toast} />);
    await userEvent.click(await screen.findByRole("button", { name: /delete agent/i }));
    await waitFor(() => expect(remove).toHaveBeenCalledWith(2));
    expect(toast).toHaveBeenCalledWith(expect.stringMatching(/referenced|failed/i), "error");
  });
});
