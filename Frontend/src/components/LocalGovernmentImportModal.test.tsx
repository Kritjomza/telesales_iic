import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { LocalGovernmentImportModal } from "./LocalGovernmentImportModal";
import { apiService } from "../domain/apiService";

describe("LocalGovernmentImportModal", () => {
  it("uses analyze/commit flow and paginates preview rows", async () => {
    const user = userEvent.setup();
    const rows = Array.from({ length: 30 }, (_, index) => ({
      rowNumber: index + 2,
      organizationName: `Local Gov ${index + 1}`,
      normalizedOrganizationName: `Local Gov ${index + 1}`,
      isDuplicate: false,
      customerPreview: { name: `Local Gov ${index + 1}`, phone: "02-111-1111" },
      detailPreviews: [{ contactName: `Contact ${index + 1}`, contactPosition: "Mayor" }],
      warnings: [],
      errors: []
    }));

    vi.spyOn(apiService, "previewLocalGovernmentImport").mockResolvedValue({
      totalRows: 30,
      validRows: 30,
      duplicateRows: 0,
      errorRows: 0,
      estimatedCustomersToInsert: 30,
      estimatedDetailsToInsert: 30,
      warnings: [],
      errors: [],
      rows
    });
    vi.spyOn(apiService, "confirmLocalGovernmentImport").mockResolvedValue({
      totalRows: 30,
      insertedCustomers: 30,
      insertedDetails: 30,
      skippedDuplicates: 0,
      errorRows: 0,
      warnings: [],
      errors: [],
      rows: []
    });

    const showToast = vi.fn();
    const onImportSuccess = vi.fn();
    const { container } = render(
      <LocalGovernmentImportModal
        isOpen
        onClose={vi.fn()}
        onImportSuccess={onImportSuccess}
        showToast={showToast}
      />
    );

    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(fileInput, new File(["a,b"], "local-government.csv", { type: "text/csv" }));

    expect(screen.getByRole("button", { name: "Analyze & Preview" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Commit Import" })).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Analyze & Preview" }));

    expect(await screen.findByRole("button", { name: "Commit Import" })).toBeInTheDocument();
    expect(screen.getByText("Spreadsheet Data Preview")).toBeInTheDocument();
    expect(screen.getByText((_, node) => node?.textContent === "Showing 1-10 of 30 records")).toBeInTheDocument();
    expect(screen.getByText("Local Gov 1")).toBeInTheDocument();
    expect(screen.queryByText("Local Gov 11")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Next page" }));

    expect(screen.getByText("Local Gov 11")).toBeInTheDocument();
    expect(screen.queryByText("Local Gov 1")).not.toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText("Rows per page:"), "25");

    expect(screen.getByText((_, node) => node?.textContent === "Showing 1-25 of 30 records")).toBeInTheDocument();
    expect(screen.getByText("Local Gov 25")).toBeInTheDocument();
    expect(screen.queryByText("Local Gov 26")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Commit Import" }));

    expect(apiService.confirmLocalGovernmentImport).toHaveBeenCalled();
    expect(onImportSuccess).toHaveBeenCalled();
  });
});
