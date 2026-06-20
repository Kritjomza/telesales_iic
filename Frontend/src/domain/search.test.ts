import { describe, expect, it } from "vitest";
import { normalizeQuery, rankSearchFields, rankSearchFieldsMultiToken } from "./search";

describe("rankSearchFields", () => {
  it("matches a slightly misspelled user name before returning no records", () => {
    const rank = rankSearchFields("chinsamithh", [
      { value: "Chinsamith", label: "Name", fuzzy: true },
      { value: "CS001", label: "Username" },
      { value: "080-123-4567", label: "Tel", normalizePhone: true }
    ]);

    expect(rank).toEqual({ rank: 3, matchedField: "Name" });
  });

  it("matches phone keywords with spaces or dashes without using fuzzy", () => {
    const rank = rankSearchFields("080 123", [
      { value: "Chinsamith", label: "Name", fuzzy: true },
      { value: "080-123-4567", label: "Tel", normalizePhone: true }
    ]);

    expect(rank).toEqual({ rank: 1, matchedField: "Tel" });
  });
});

describe("normalizeQuery", () => {
  it("splits a multi-word query into tokens", () => {
    expect(normalizeQuery("สินค้า นนทบุรี")).toEqual(["สินค้า", "นนทบุรี"]);
  });

  it("collapses repeated whitespace", () => {
    expect(normalizeQuery("  สินค้า   นนทบุรี  ")).toEqual(["สินค้า", "นนทบุรี"]);
  });

  it("returns empty array for empty input", () => {
    expect(normalizeQuery("")).toEqual([]);
    expect(normalizeQuery("   ")).toEqual([]);
  });
});

describe("rankSearchFieldsMultiToken", () => {
  const fields = [
    { value: "องค์การคลังสินค้า", label: "Customer name", fuzzy: true },
    { value: "563 ถนนนนทบุรี ตำบลสวนใหญ่ อำเภอเมืองนนทบุรี นนทบุรี 11000", label: "Address" },
    { value: "021234567", label: "Phone", normalizePhone: true },
    { value: "Government", label: "Business type", fuzzy: true },
  ];

  it("matches multiple tokens across different fields", () => {
    const result = rankSearchFieldsMultiToken("สินค้า นนทบุรี", fields);
    expect(result).not.toBeNull();
    expect(result!.matchedFields).toContain("Customer name");
    expect(result!.matchedFields).toContain("Address");
    expect(result!.tokenMatches).toHaveLength(2);
  });

  it("rejects partial matches where not all tokens match", () => {
    const fieldsOnlyName = [
      { value: "องค์การคลังสินค้า", label: "Customer name", fuzzy: true },
      { value: "กรุงเทพมหานคร", label: "Address" },
    ];

    const multiResult = rankSearchFieldsMultiToken("สินค้า นนทบุรี", fields);
    const oneTokenResult = rankSearchFieldsMultiToken("สินค้า นนทบุรี", fieldsOnlyName);

    expect(multiResult).not.toBeNull();
    expect(oneTokenResult).toBeNull();
  });

  it("produces same ranking regardless of token order if all tokens are matched", () => {
    const result1 = rankSearchFieldsMultiToken("สินค้า นนทบุรี", fields)!;
    const result2 = rankSearchFieldsMultiToken("นนทบุรี สินค้า", fields)!;

    expect(result1.rank).toBe(result2.rank);
    expect(result1.matchedFields.sort()).toEqual(result2.matchedFields.sort());
  });

  it("rejects partial matches regardless of token order", () => {
    const fieldsOnlyName = [
      { value: "องค์การคลังสินค้า", label: "Customer name" },
    ];
    const fieldsOnlyAddress = [
      { value: "นนทบุรี", label: "Address" },
    ];

    expect(rankSearchFieldsMultiToken("สินค้า นนทบุรี", fieldsOnlyName)).toBeNull();
    expect(rankSearchFieldsMultiToken("สินค้า นนทบุรี", fieldsOnlyAddress)).toBeNull();
    expect(rankSearchFieldsMultiToken("นนทบุรี สินค้า", fieldsOnlyName)).toBeNull();
    expect(rankSearchFieldsMultiToken("นนทบุรี สินค้า", fieldsOnlyAddress)).toBeNull();
  });

  it("handles the explicit user examples for strict token matching", () => {
    const exampleFields = [
      { value: "บริษัท มหาชน จำกัด", label: "Customer name" },
      { value: "อำเภอเมืองนนทบุรี นนทบุรี", label: "Address" },
    ];

    // Query: มหาชน -> Should match
    const result1 = rankSearchFieldsMultiToken("มหาชน", exampleFields);
    expect(result1).not.toBeNull();

    // Query: มหาชน นนทบุรี -> Should match (both tokens match)
    const result2 = rankSearchFieldsMultiToken("มหาชน นนทบุรี", exampleFields);
    expect(result2).not.toBeNull();

    // Query: มหาชน นนทบุรี ท่าอิฐ -> Should NOT match (ท่าอิฐ not found)
    const result3 = rankSearchFieldsMultiToken("มหาชน นนทบุรี ท่าอิฐ", exampleFields);
    expect(result3).toBeNull();
  });

  it("still works for single-token queries", () => {
    const result = rankSearchFieldsMultiToken("สินค้า", fields);
    expect(result).not.toBeNull();
    expect(result!.matchedField).toBe("Customer name");
  });

  it("still works for phone number search", () => {
    const result = rankSearchFieldsMultiToken("021234567", fields);
    expect(result).not.toBeNull();
    expect(result!.matchedField).toBe("Phone");
  });

  it("returns null when no tokens match", () => {
    const result = rankSearchFieldsMultiToken("ไม่มี ข้อมูล", fields);
    expect(result).toBeNull();
  });

  it("returns empty rank for empty query", () => {
    const result = rankSearchFieldsMultiToken("", fields);
    expect(result).not.toBeNull();
    expect(result!.rank).toBe(0);
  });
});
