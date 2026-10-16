using System.Net;
using System.Net.Http;
using System.Text;
using CyberGame.Application.DTOs;
using CyberGame.Client.Services;
using CyberGame.Domain.Enums;
using Xunit;

namespace CyberGame.Tests;

public class ApiClientContractTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string? CapturedRequestBody { get; private set; }
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            if (request.Content != null)
            {
                CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _responseFactory(request);
        }
    }

    private static (ApiClient Client, FakeHttpMessageHandler Handler) CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var handler = new FakeHttpMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5227/api/v1/")
        };
        return (new ApiClient(httpClient), handler);
    }

    [Fact]
    public async Task GetCustomersAsync_UsesSearchQueryParam_NotQuery()
    {
        var customerId = Guid.NewGuid();
        var responseJson = $$"""
        {
            "items": [
                {
                    "id": "{{customerId}}",
                    "code": "KH-001",
                    "fullName": "Nguyen Van An",
                    "phone": "0901234567"
                }
            ],
            "totalCount": 1,
            "page": 1,
            "pageSize": 50
        }
        """;

        var (client, handler) = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var result = await client.GetCustomersAsync(search: "Nguyen");

        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
        var query = handler.CapturedRequest.RequestUri?.Query ?? string.Empty;
        // Verify 'search' query parameter is used and NOT 'query'
        Assert.Contains("search=Nguyen", query);
        Assert.DoesNotContain("query=", query);
        Assert.Single(result.Items);
        Assert.Equal("Nguyen Van An", result.Items[0].FullName);
    }

    [Fact]
    public async Task GetProductsAsync_DeserializesPagedResult_AndReturnsList()
    {
        var prod1Id = Guid.NewGuid();
        var prod2Id = Guid.NewGuid();
        var responseJson = $$"""
        {
            "items": [
                {
                    "id": "{{prod1Id}}",
                    "sku": "STING",
                    "name": "Sting Dau",
                    "salePrice": 12000
                },
                {
                    "id": "{{prod2Id}}",
                    "sku": "C2",
                    "name": "Tra Xanh C2",
                    "salePrice": 10000
                }
            ],
            "totalCount": 2,
            "page": 1,
            "pageSize": 20
        }
        """;

        var (client, handler) = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var result = await client.GetProductsAsync();

        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
        Assert.Equal("/api/v1/products", handler.CapturedRequest.RequestUri?.AbsolutePath);
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("STING", result[0].Sku);
        Assert.Equal("C2", result[1].Sku);
    }

    [Fact]
    public async Task ReceiveInventoryAsync_ReturnsInventoryItemDto()
    {
        var prodId = Guid.NewGuid();
        var responseJson = $$"""
        {
            "productId": "{{prodId}}",
            "sku": "STING",
            "name": "Sting Dau",
            "unit": "chai",
            "quantityOnHand": 60,
            "costPrice": 7500,
            "reorderLevel": 10,
            "isLowStock": false
        }
        """;

        var (client, handler) = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var request = new ReceiveInventoryRequest(prodId, 10, 7500, "Nhap kho dot 2");
        InventoryItemDto result = await client.ReceiveInventoryAsync(request);

        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
        Assert.Equal("/api/v1/inventory/receive", handler.CapturedRequest.RequestUri?.AbsolutePath);
        Assert.NotNull(result);
        Assert.Equal(prodId, result.ProductId);
        Assert.Equal(60, result.QuantityOnHand);
    }

    [Fact]
    public async Task AdjustInventoryAsync_ReturnsInventoryItemDto()
    {
        var prodId = Guid.NewGuid();
        var responseJson = $$"""
        {
            "productId": "{{prodId}}",
            "sku": "STING",
            "name": "Sting Dau",
            "unit": "chai",
            "quantityOnHand": 45,
            "costPrice": 7500,
            "reorderLevel": 10,
            "isLowStock": false
        }
        """;

        var (client, handler) = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var request = new AdjustInventoryRequest(prodId, 45, "Kiem ke thuc te");
        InventoryItemDto result = await client.AdjustInventoryAsync(request);

        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
        Assert.Equal("/api/v1/inventory/adjust", handler.CapturedRequest.RequestUri?.AbsolutePath);
        Assert.NotNull(result);
        Assert.Equal(prodId, result.ProductId);
        Assert.Equal(45, result.QuantityOnHand);
    }

    [Fact]
    public async Task GetInvoicesAsync_SendsFromDateAndToDateQueryParams()
    {
        var responseJson = """
        {
            "items": [],
            "totalCount": 0,
            "page": 1,
            "pageSize": 50
        }
        """;

        var (client, handler) = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var fromDate = new DateTime(2026, 9, 1);
        var toDate = new DateTime(2026, 9, 17);
        var result = await client.GetInvoicesAsync(fromDate, toDate);

        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
        var query = handler.CapturedRequest.RequestUri?.Query ?? string.Empty;
        Assert.Contains("fromDate=2026-09-01", query);
        Assert.Contains("toDate=2026-09-17", query);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetWalletTransactionsAsync_CallsCorrectRoute()
    {
        var customerId = Guid.NewGuid();
        var responseJson = """
        {
            "items": [],
            "totalCount": 0,
            "page": 1,
            "pageSize": 20
        }
        """;

        var (client, handler) = CreateClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var result = await client.GetWalletTransactionsAsync(customerId, 1, 20);

        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
        Assert.Equal($"/api/v1/customers/{customerId}/wallet-transactions", handler.CapturedRequest.RequestUri?.AbsolutePath);
        var query = handler.CapturedRequest.RequestUri?.Query ?? string.Empty;
        Assert.Contains("page=1", query);
        Assert.Contains("pageSize=20", query);
        Assert.NotNull(result);
    }
}
