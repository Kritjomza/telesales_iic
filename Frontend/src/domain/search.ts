export type SearchField = {
  value: unknown;
  label: string;
  fuzzy?: boolean;
  normalizePhone?: boolean;
};

export type SearchRank = {
  rank: number;
  matchedField: string;
};

const FUZZY_DISTANCE_LIMIT = 2;

export function normalizeSearchText(value: unknown): string {
  return String(value ?? "").trim().toLowerCase();
}

export function normalizePhoneText(value: unknown): string {
  return normalizeSearchText(value).replace(/[\s\-()[\]{}.+#/\\,;:]+/g, "");
}

export function rankSearchFields(keyword: string, fields: SearchField[]): SearchRank | null {
  const term = normalizeSearchText(keyword);
  const phoneTerm = normalizePhoneText(keyword);
  if (!term) return { rank: 0, matchedField: "" };

  const matches = fields
    .map((field) => rankField(field, term, phoneTerm))
    .filter((rank): rank is SearchRank => rank !== null)
    .sort((a, b) => a.rank - b.rank);

  return matches[0] ?? null;
}

export function matchesSearchFields(keyword: string, fields: SearchField[]): boolean {
  return rankSearchFields(keyword, fields) !== null;
}

function rankField(field: SearchField, term: string, phoneTerm: string): SearchRank | null {
  const value = field.normalizePhone ? normalizePhoneText(field.value) : normalizeSearchText(field.value);
  const compareTerm = field.normalizePhone ? phoneTerm : term;

  if (!value || !compareTerm) return null;
  if (value === compareTerm) return { rank: 0, matchedField: field.label };
  if (value.startsWith(compareTerm)) return { rank: 1, matchedField: field.label };
  if (value.includes(compareTerm)) return { rank: 2, matchedField: field.label };
  if (field.fuzzy && isFuzzyMatch(value, compareTerm)) return { rank: 3, matchedField: field.label };

  return null;
}

function isFuzzyMatch(value: string, term: string): boolean {
  if (term.length < 4) return false;

  const valueParts = splitTerms(value);
  const termParts = splitTerms(term);

  if (termParts.length > 1) {
    return termParts.every((termPart) =>
      termPart.length < 4 ||
      valueParts.some((valuePart) => isFuzzyPartMatch(valuePart, termPart))
    );
  }

  return [...valueParts, value].some((part) => isFuzzyPartMatch(part, term));
}

function splitTerms(value: string): string[] {
  return value.split(/\s+/).filter(Boolean);
}

function isFuzzyPartMatch(value: string, term: string): boolean {
  return Math.abs(value.length - term.length) <= FUZZY_DISTANCE_LIMIT &&
    levenshteinDistance(value, term) <= FUZZY_DISTANCE_LIMIT;
}

function levenshteinDistance(left: string, right: string): number {
  const previous = Array.from({ length: right.length + 1 }, (_, index) => index);
  const current = Array.from({ length: right.length + 1 }, () => 0);

  for (let i = 1; i <= left.length; i += 1) {
    current[0] = i;

    for (let j = 1; j <= right.length; j += 1) {
      const cost = left[i - 1] === right[j - 1] ? 0 : 1;
      current[j] = Math.min(
        current[j - 1] + 1,
        previous[j] + 1,
        previous[j - 1] + cost
      );
    }

    for (let j = 0; j <= right.length; j += 1) {
      previous[j] = current[j];
    }
  }

  return previous[right.length];
}

export type TokenMatch = {
  token: string;
  field: string;
  rank: number;
};

export type MultiTokenSearchRank = {
  rank: number;
  matchedField: string;
  matchedFields: string[];
  tokenMatches: TokenMatch[];
};

export function normalizeQuery(keyword: string): string[] {
  const trimmed = keyword.trim();
  if (!trimmed) return [];
  return trimmed.replace(/\s+/g, " ").toLowerCase().split(" ").filter(Boolean);
}

export function rankSearchFieldsMultiToken(
  keyword: string,
  fields: SearchField[]
): MultiTokenSearchRank | null {
  const tokens = normalizeQuery(keyword);
  if (tokens.length === 0) return { rank: 0, matchedField: "", matchedFields: [], tokenMatches: [] };

  const tokenMatches: TokenMatch[] = [];

  for (const token of tokens) {
    const tokenTerm = token.toLowerCase();
    const phoneTerm = tokenTerm.replace(/[\s-]+/g, "");

    let bestMatch: { field: string; rank: number } | null = null;

    for (const field of fields) {
      const value = field.normalizePhone
        ? normalizePhoneText(field.value)
        : normalizeSearchText(field.value);
      const compareTerm = field.normalizePhone ? phoneTerm : tokenTerm;

      if (!value || !compareTerm) continue;

      let fieldRank: number | null = null;
      if (value === compareTerm) fieldRank = 0;
      else if (value.startsWith(compareTerm)) fieldRank = 1;
      else if (value.includes(compareTerm)) fieldRank = 2;
      else if (field.fuzzy && isFuzzyMatch(value, compareTerm)) fieldRank = 3;

      if (fieldRank !== null && (bestMatch === null || fieldRank < bestMatch.rank)) {
        bestMatch = { field: field.label, rank: fieldRank };
        if (fieldRank === 0) break;
      }
    }

    if (bestMatch) {
      tokenMatches.push({ token, field: bestMatch.field, rank: bestMatch.rank });
    }
  }

  if (tokenMatches.length === 0) return null;

  const totalTokens = tokens.length;
  const matchedCount = tokenMatches.length;
  const sumRanks = tokenMatches.reduce((sum, m) => sum + m.rank, 0);

  const compositeRank = (totalTokens - matchedCount) * 1000 + sumRanks;

  const matchedFields = [...new Set(tokenMatches.map(m => m.field))];
  const matchedField = matchedFields.join(", ");

  return { rank: compositeRank, matchedField, matchedFields, tokenMatches };
}
