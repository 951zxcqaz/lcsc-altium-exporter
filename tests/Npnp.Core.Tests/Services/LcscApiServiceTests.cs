using FluentAssertions;
using RichardSzalay.MockHttp;
using Npnp.Core.Services;
using Xunit;

namespace Npnp.Core.Tests.Services;

public class LcscApiServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsResults_WhenApiReturnsValidJson()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://lcsc.com/api/global/search/list*")
                .Respond("application/json", """
                    {
                        "result": {
                            "total": 2,
                            "list": [
                                {
                                    "productCode": "C2040",
                                    "name": "STM32F103C8T6",
                                    "description": "MCU",
                                    "package": "LQFP48",
                                    "brand": "ST"
                                }
                            ]
                        }
                    }
                    """);

        var httpClient = mockHttp.ToHttpClient();
        var service = new LcscApiService(httpClient);

        // Act
        var result = await service.SearchAsync("STM32", 10);

        // Assert
        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(1);
        result.Items[0].LcscId.Should().Be("C2040");
        result.Items[0].Name.Should().Be("STM32F103C8T6");
        result.Items[0].Description.Should().Be("MCU");
        result.Items[0].Package.Should().Be("LQFP48");
        result.Items[0].Manufacturer.Should().Be("ST");
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenNoResults()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://lcsc.com/api/global/search/list*")
                .Respond("application/json", """
                    {
                        "result": {
                            "total": 0,
                            "list": []
                        }
                    }
                    """);

        var httpClient = mockHttp.ToHttpClient();
        var service = new LcscApiService(httpClient);

        // Act
        var result = await service.SearchAsync("nonexistent", 10);

        // Assert
        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ThrowsException_WhenApiReturnsError()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://lcsc.com/api/global/search/list*")
                .Respond(System.Net.HttpStatusCode.InternalServerError);

        var httpClient = mockHttp.ToHttpClient();
        var service = new LcscApiService(httpClient);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await service.SearchAsync("test", 10));
    }
}