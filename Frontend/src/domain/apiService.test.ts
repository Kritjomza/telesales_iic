import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiService } from "./apiService";

describe("apiService customer search", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 25,
        totalPages: 0
      }))
    }));
  });

  it("requests paginated customer search with an encoded keyword", async () => {
    const result = await apiService.getCustomers({
      search: "  Apex-001  ",
      page: 1,
      pageSize: 25
    });

    expect(result).toEqual([]);
    expect(fetch).toHaveBeenCalledWith(
      "/api/customers?page=1&pageSize=25&search=++Apex-001++",
      expect.objectContaining({ credentials: "same-origin" })
    );
  });
});

describe("apiService customer create contract", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify({ id: 1 }))
    }));
  });

  it("sends structured address, company phone, and first contact", async () => {
    const payload = {
      name: "บริษัท ตัวอย่าง จำกัด",
      address: "99 ถนนสุขุมวิท",
      phone: "021234567",
      subdistrict: "คลองเตย",
      district: "คลองเตย",
      province: "กรุงเทพมหานคร",
      postal_code: "10110",
      capital: "5,000,000",
      bt_type: "Industrial",
      firstContact: {
        contact_name: "สมชาย",
        contact_tel: "0812345678",
        contact_email: "somchai@example.com",
        contact_position: "IT Manager",
        contact_tel_office: ""
      }
    };

    await apiService.addCustomer(payload);

    expect(fetch).toHaveBeenCalledWith(
      "/api/customers",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify(payload)
      })
    );
  });
});

describe("apiService manual import contract", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify({
        rows: [],
        summary: {
          total: 0,
          valid: 0,
          warning: 0,
          error: 0,
          duplicateCount: 0,
          duplicateWarningCount: 0,
          uniqueCount: 0
        }
      }))
    }));
  });

  it("normalizes snake-case manual draft fields for the .NET API", async () => {
    await apiService.validateImportRows([{
      name: "Example",
      business_type: "Industrial",
      contact_name: "Somchai",
      contact_email: "somchai@example.com",
      contact_tel: "0812345678",
      contact_position: "IT Manager"
    }]);

    expect(fetch).toHaveBeenCalledWith(
      "/api/import/customers/validate",
      expect.objectContaining({
        body: JSON.stringify([{
          name: "Example",
          address: undefined,
          phone: undefined,
          capital: undefined,
          businessType: "Industrial",
          contactName: "Somchai",
          contactEmail: "somchai@example.com",
          contactTel: "0812345678",
          contactPosition: "IT Manager",
          code: undefined,
          unstructuredCompanyInfo: undefined
        }])
      })
    );
  });
});
