using System;
using System.Collections.Generic;
using System.Linq;

namespace Telesale.Api.Helpers;

public static class StatusPolicy
{
    public static readonly HashSet<string> CustomerStatuses = new(StringComparer.Ordinal)
    {
        "Called", "Not Called"
    };

    public static readonly HashSet<string> DeviceStatuses = new(StringComparer.Ordinal)
    {
        "New", "Win", "Lost"
    };

    public static readonly HashSet<string> ProjectStatuses = new(StringComparer.Ordinal)
    {
        "Discuss", "Quotation", "Win", "Lost", "Hold", "Cancel"
    };

    public static bool IsValidCustomerStatus(string? status)
    {
        return status != null && CustomerStatuses.Contains(status);
    }

    public static bool IsValidDeviceStatus(string? status)
    {
        return status != null && DeviceStatuses.Contains(status);
    }

    public static bool IsValidProjectStatus(string? status)
    {
        return status != null && ProjectStatuses.Contains(status);
    }

    public static string GetInvalidStatusMessage(string entity, string? status, IEnumerable<string> allowedValues)
    {
        var allowedStr = string.Join(", ", allowedValues);
        return $"Invalid {entity.ToLowerInvariant()} status '{status}'. Allowed values: {allowedStr}.";
    }
}
