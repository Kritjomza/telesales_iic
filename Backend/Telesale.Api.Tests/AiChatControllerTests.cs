using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Telesale.Api.Controllers;
using Telesale.Api.Models;

namespace Telesale.Api.Tests;

public class AiChatControllerTests
{
    [Fact]
    public void SendMessage_ReturnsMockResponse_WhenMessageIsValid()
    {
        var controller = new AiChatController();
        var request = new AiChatRequestDto { Message = "ขอข้อมูลบริษัท Apex" };

        var result = controller.SendMessage(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AiChatResponseDto>(ok.Value);
        Assert.Equal(
            "AI Chat Assistant endpoint is ready. Customer context retrieval will be added in Sprint 2.",
            response.Reply);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SendMessage_ReturnsBadRequest_WhenMessageIsMissing(string? message)
    {
        var controller = new AiChatController();
        var request = new AiChatRequestDto { Message = message };

        var result = controller.SendMessage(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public void SendMessage_ReturnsBadRequest_WhenMessageIsTooLong()
    {
        var controller = new AiChatController();
        var request = new AiChatRequestDto { Message = new string('x', 501) };

        var result = controller.SendMessage(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }
}
