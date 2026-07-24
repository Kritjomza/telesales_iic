import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiService } from "../domain/apiService";
import { MasterDataView } from "./MasterDataView";

const users = [
  {
    id: 1,
    name: "Root Admin",
    username: "root",
    email: "root@example.com",
    roles: "Super Admin" as const,
    tel: "",
    position: "",
    is_active: true
  },
  {
    id: 2,
    name: "Sales User",
    username: "sales",
    email: "sales@example.com",
    roles: "Sale" as const,
    tel: "0123456789",
    position: "Sales",
    is_active: true
  }
];

describe("MasterDataView user management", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(apiService, "getBrands").mockResolvedValue([]);
    vi.spyOn(apiService, "getProducts").mockResolvedValue([]);
    vi.spyOn(apiService, "getCategories").mockResolvedValue([]);
    vi.spyOn(apiService, "getUsers").mockResolvedValue(users);
  });

  it("shows CRUD controls only to Admin and Super Admin", async () => {
    const { rerender } = render(
      <MasterDataView tableType="users" userRole="Admin" showToast={vi.fn()} />
    );

    expect(await screen.findByRole("button", { name: "Add User" })).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Edit user" })).toHaveLength(1);
    expect(screen.getAllByRole("button", { name: "Delete user" })).toHaveLength(1);

    rerender(<MasterDataView tableType="users" userRole="Sale" showToast={vi.fn()} />);
    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Add User" })).not.toBeInTheDocument()
    );
    expect(screen.queryByRole("button", { name: "Edit user" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete user" })).not.toBeInTheDocument();
  });

  it("opens create form with required password and hides Super Admin from Admin", async () => {
    const user = userEvent.setup();
    render(<MasterDataView tableType="users" userRole="Admin" showToast={vi.fn()} />);

    await user.click(await screen.findByRole("button", { name: "Add User" }));

    expect(screen.getByRole("heading", { name: "Add User" })).toBeInTheDocument();
    expect(screen.getByLabelText("Password *")).toBeRequired();
    expect(screen.queryByRole("option", { name: "Super Admin" })).not.toBeInTheDocument();
  });

  it("edits with an empty optional password and submits an explicit safe payload", async () => {
    const updateUser = vi.spyOn(apiService as any, "updateUser").mockResolvedValue(users[1]);
    const user = userEvent.setup();
    render(<MasterDataView tableType="users" userRole="Super Admin" showToast={vi.fn()} />);

    const editButtons = await screen.findAllByRole("button", { name: "Edit user" });
    await user.click(editButtons[1]);
    const password = screen.getByLabelText("Password");
    expect(password).toHaveValue("");
    expect(password).not.toBeRequired();
    await user.clear(screen.getByLabelText("Name *"));
    await user.type(screen.getByLabelText("Name *"), "Updated Sales");
    await user.click(screen.getByRole("button", { name: "Save User" }));

    await waitFor(() =>
      expect(updateUser).toHaveBeenCalledWith(2, {
        name: "Updated Sales",
        username: "sales",
        email: "sales@example.com",
        role: "Sale",
        tel: "0123456789",
        position: "Sales",
        isActive: true
      })
    );
  });

  it("confirms deletion, disables the action while pending, and refreshes", async () => {
    let resolveDelete!: () => void;
    const deletePromise = new Promise<void>((resolve) => {
      resolveDelete = resolve;
    });
    const deleteUser = vi.spyOn(apiService as any, "deleteUser").mockReturnValue(deletePromise);
    vi.spyOn(window, "confirm").mockReturnValue(true);
    const user = userEvent.setup();
    render(<MasterDataView tableType="users" userRole="Admin" showToast={vi.fn()} />);

    const deleteButton = await screen.findByRole("button", { name: "Delete user" });
    await user.click(deleteButton);
    expect(deleteButton).toBeDisabled();
    resolveDelete();

    await waitFor(() => expect(deleteUser).toHaveBeenCalledWith(2));
    await waitFor(() => expect(apiService.getUsers).toHaveBeenCalledTimes(2));
  });
});
