import React from "react";
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from "lucide-react";

interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

export const Pagination: React.FC<PaginationProps> = ({
  page,
  pageSize,
  totalCount,
  totalPages,
  onPageChange,
  onPageSizeChange
}) => {
  if (totalCount === 0) return null;

  const startRecord = (page - 1) * pageSize + 1;
  const endRecord = Math.min(page * pageSize, totalCount);

  // Generate range of page numbers to show
  const pageRange = [];
  const maxVisiblePages = 5;
  let startPage = Math.max(1, page - Math.floor(maxVisiblePages / 2));
  let endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);

  if (endPage - startPage + 1 < maxVisiblePages) {
    startPage = Math.max(1, endPage - maxVisiblePages + 1);
  }

  for (let i = startPage; i <= endPage; i++) {
    pageRange.push(i);
  }

  return (
    <div className="pagination-container">
      <div className="pagination-info">
        Showing <span className="pagination-highlight">{startRecord}-{endRecord}</span> of{" "}
        <span className="pagination-highlight">{totalCount}</span> records
      </div>

      <div className="pagination-controls">
        <div className="pagination-size-select">
          <label htmlFor="page-size-selector">Rows per page:</label>
          <select
            id="page-size-selector"
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
            className="pagination-select"
          >
            {[10, 25, 50, 100].map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </div>

        <div className="pagination-buttons">
          <button
            onClick={() => onPageChange(1)}
            disabled={page === 1}
            className="pagination-btn"
            aria-label="First page"
            type="button"
          >
            <ChevronsLeft size={16} />
          </button>
          <button
            onClick={() => onPageChange(page - 1)}
            disabled={page === 1}
            className="pagination-btn"
            aria-label="Previous page"
            type="button"
          >
            <ChevronLeft size={16} />
          </button>

          {pageRange.map((p) => (
            <button
              key={p}
              onClick={() => onPageChange(p)}
              className={`pagination-btn ${p === page ? "active" : ""}`}
              aria-label={`Page ${p}`}
              type="button"
            >
              {p}
            </button>
          ))}

          <button
            onClick={() => onPageChange(page + 1)}
            disabled={page === totalPages || totalPages === 0}
            className="pagination-btn"
            aria-label="Next page"
            type="button"
          >
            <ChevronRight size={16} />
          </button>
          <button
            onClick={() => onPageChange(totalPages)}
            disabled={page === totalPages || totalPages === 0}
            className="pagination-btn"
            aria-label="Last page"
            type="button"
          >
            <ChevronsRight size={16} />
          </button>
        </div>
      </div>
    </div>
  );
};
