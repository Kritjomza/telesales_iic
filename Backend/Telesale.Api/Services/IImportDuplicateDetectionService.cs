using System.Threading.Tasks;

namespace Telesale.Api.Services;

public class DuplicateMatchResult
{
    public string Status { get; set; } = "unique"; // "duplicate" (95+), "warning" (85-94), "unique" (<85)
    public int Score { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? MatchedCustomerName { get; set; }
    public string? MatchedCustomerCode { get; set; }
    public uint? MatchedCustomerId { get; set; }
}

public interface IImportDuplicateDetectionService
{
    Task<DuplicateMatchResult> DetectDuplicateAsync(string? name, string? phone, string? code);
}
