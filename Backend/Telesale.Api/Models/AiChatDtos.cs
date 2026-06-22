namespace Telesale.Api.Models;

public class AiChatRequestDto
{
    public string? Message { get; set; }
}

public class AiChatResponseDto
{
    public string Reply { get; set; } = string.Empty;
}
