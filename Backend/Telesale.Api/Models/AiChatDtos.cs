namespace Telesale.Api.Models;

public class AiChatRequestDto
{
    public string? Message { get; set; }

    public uint? ContextCustomerId { get; set; }

    public uint? LastSelectedCustomerId { get; set; }

    public string? LastSelectedCustomerName { get; set; }
}

public class AiChatResponseDto
{
    public string Reply { get; set; } = string.Empty;

    public AiChatMetadataDto Metadata { get; set; } = new();
}

public class AiChatMetadataDto
{
    public string Source { get; set; } = "database";

    public bool UsedAi { get; set; }

    public int MatchedCustomersCount { get; set; }

    public uint? SelectedCustomerId { get; set; }

    public string? SelectedCustomerName { get; set; }

    public string? Intent { get; set; }
}
