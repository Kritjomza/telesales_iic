namespace Telesale.Api.Services;

public class LocalGovernmentRawImportRow
{
    public int RowNumber { get; set; }
    public string? Sequence { get; set; }
    public string? Province { get; set; }
    public string? Type { get; set; }
    public string? OrganizationName { get; set; }
    public string? OfficePhone { get; set; }
    public string? MayorName { get; set; }
    public string? MayorPhone { get; set; }
    public string? DeputyMayorName { get; set; }
    public string? DeputyMayorPhone { get; set; }
    public string? EducationDirectorName { get; set; }
    public string? EducationDirectorPhone { get; set; }
    public string? SchoolCount { get; set; }
    public string? Note { get; set; }
}

public class LocalGovernmentCustomerPreview
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "Not Called";
    public string CreateType { get; set; } = "Import";
    public bool IsActive { get; set; } = true;
}

public class LocalGovernmentDetailPreview
{
    public string? ContactName { get; set; }
    public string? ContactTel { get; set; }
    public string ContactPosition { get; set; } = string.Empty;
    public string? ContactTelOffice { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LocalGovernmentImportIssue
{
    public int? RowNumber { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class LocalGovernmentImportRowPreview
{
    public int RowNumber { get; set; }
    public string? OrganizationName { get; set; }
    public string? NormalizedOrganizationName { get; set; }
    public bool IsDuplicate { get; set; }
    public LocalGovernmentCustomerPreview? CustomerPreview { get; set; }
    public List<LocalGovernmentDetailPreview> DetailPreviews { get; set; } = new();
    public List<LocalGovernmentImportIssue> Warnings { get; set; } = new();
    public List<LocalGovernmentImportIssue> Errors { get; set; } = new();
}

public class LocalGovernmentImportPreviewSummary
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int DuplicateRows { get; set; }
    public int ErrorRows { get; set; }
    public int EstimatedCustomersToInsert { get; set; }
    public int EstimatedDetailsToInsert { get; set; }
    public List<LocalGovernmentImportIssue> Warnings { get; set; } = new();
    public List<LocalGovernmentImportIssue> Errors { get; set; } = new();
    public List<LocalGovernmentImportRowPreview> Rows { get; set; } = new();
}

public class LocalGovernmentImportRowResult
{
    public int RowNumber { get; set; }
    public string? OrganizationName { get; set; }
    public string? NormalizedOrganizationName { get; set; }
    public string Status { get; set; } = string.Empty;
    public uint? InsertedCustomerId { get; set; }
    public int InsertedDetailCount { get; set; }
    public List<LocalGovernmentImportIssue> Warnings { get; set; } = new();
    public List<LocalGovernmentImportIssue> Errors { get; set; } = new();
}

public class LocalGovernmentImportConfirmResult
{
    public int TotalRows { get; set; }
    public int InsertedCustomers { get; set; }
    public int InsertedDetails { get; set; }
    public int SkippedDuplicates { get; set; }
    public int ErrorRows { get; set; }
    public List<LocalGovernmentImportIssue> Warnings { get; set; } = new();
    public List<LocalGovernmentImportIssue> Errors { get; set; } = new();
    public List<LocalGovernmentImportRowResult> Rows { get; set; } = new();
}
