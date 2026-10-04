using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using stock_api.Contracts;
using stock_api.Infrastructure;
using Xunit;

namespace stock_api.Tests;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task Unexpected_exceptions_return_a_generic_json_error_response()
    {
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("Sensitive implementation detail."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);

        responseBody.Position = 0;
        var error = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(
            responseBody,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(error);
        Assert.Equal("An unexpected error occurred while processing the request.", error.Message);
    }
}
