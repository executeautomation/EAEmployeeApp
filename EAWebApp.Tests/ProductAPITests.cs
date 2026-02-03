using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EAWebApp;
using Newtonsoft.Json;
using Xunit;

namespace EAWebApp.Tests
{
    public sealed class ProductAPITests
    {
        [Fact]
        public async Task GetProductByIdAsync_SendsExpectedRequestAndParsesResponse()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                capturedRequest = request;
                var product = new Product
                {
                    Id = 123,
                    Name = "Widget",
                    Description = "Test",
                    Price = 42,
                    ProductType = ProductType.CPU
                };

                return Task.FromResult(JsonResponse(product));
            });

            var client = new HttpClient(handler);
            var api = new ProductAPI("http://localhost:5000", client);

            var result = await api.GetProductByIdAsync(123);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
            Assert.Equal("http://localhost:5000/Product/GetProductById/123", capturedRequest.RequestUri!.ToString());
            Assert.Contains(capturedRequest.Headers.Accept, h => h.MediaType == "text/plain");
            Assert.Equal(123, result.Id);
            Assert.Equal("Widget", result.Name);
        }

        [Fact]
        public async Task GetProductsAsync_ParsesCollection()
        {
            var handler = new StubHttpMessageHandler((_, _) =>
            {
                var products = new List<Product>
                {
                    new Product { Id = 1, Name = "A", Price = 10, ProductType = ProductType.MONITOR },
                    new Product { Id = 2, Name = "B", Price = 20, ProductType = ProductType.CPU }
                };

                return Task.FromResult(JsonResponse(products));
            });

            var api = new ProductAPI("http://localhost:5000", new HttpClient(handler));

            var result = await api.GetProductsAsync();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, p => p.Id == 1 && p.Name == "A");
            Assert.Contains(result, p => p.Id == 2 && p.Name == "B");
        }

        [Fact]
        public async Task CreateAsync_SendsPostWithJsonPatchContentType()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpMessageHandler(async (request, _) =>
            {
                capturedRequest = request;
                var body = await request.Content!.ReadAsStringAsync();
                var parsed = JsonConvert.DeserializeObject<Product>(body);

                Assert.NotNull(parsed);
                Assert.Equal("New", parsed!.Name);

                return JsonResponse(parsed);
            });

            var api = new ProductAPI("http://localhost:5000", new HttpClient(handler));

            var created = await api.CreateAsync(new Product
            {
                Id = 10,
                Name = "New",
                Description = "Created",
                Price = 99,
                ProductType = ProductType.EXTERNAL
            });

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
            Assert.Equal("http://localhost:5000/Product/Create", capturedRequest.RequestUri!.ToString());
            Assert.Equal("application/json-patch+json", capturedRequest.Content!.Headers.ContentType!.MediaType);
            Assert.Equal("New", created.Name);
        }

        [Fact]
        public async Task UpdateAsync_SendsPutWithJsonPatchContentType()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpMessageHandler(async (request, _) =>
            {
                capturedRequest = request;
                var body = await request.Content!.ReadAsStringAsync();
                var parsed = JsonConvert.DeserializeObject<Product>(body);

                return JsonResponse(parsed!);
            });

            var api = new ProductAPI("http://localhost:5000", new HttpClient(handler));

            var updated = await api.UpdateAsync(new Product
            {
                Id = 5,
                Name = "Updated",
                Description = "Updated",
                Price = 123,
                ProductType = ProductType.PERIPHARALS
            });

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Put, capturedRequest!.Method);
            Assert.Equal("http://localhost:5000/Product/Update", capturedRequest.RequestUri!.ToString());
            Assert.Equal("application/json-patch+json", capturedRequest.Content!.Headers.ContentType!.MediaType);
            Assert.Equal(5, updated.Id);
        }

        [Fact]
        public async Task DeleteAsync_SendsDeleteWithIdQuery()
        {
            HttpRequestMessage? capturedRequest = null;
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                capturedRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

            var api = new ProductAPI("http://localhost:5000", new HttpClient(handler));

            await api.DeleteAsync(7);

            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Delete, capturedRequest!.Method);
            Assert.Equal("http://localhost:5000/Product/Delete?id=7", capturedRequest.RequestUri!.ToString());
        }

        [Fact]
        public async Task GetProductsAsync_NonSuccessStatusThrowsApiException()
        {
            var handler = new StubHttpMessageHandler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("server error", Encoding.UTF8, "text/plain")
                };

                return Task.FromResult(response);
            });

            var api = new ProductAPI("http://localhost:5000", new HttpClient(handler));

            var ex = await Assert.ThrowsAsync<ApiException>(() => api.GetProductsAsync());

            Assert.Equal((int)HttpStatusCode.InternalServerError, ex.StatusCode);
            Assert.Contains("server error", ex.Response);
        }

        [Fact]
        public async Task GetProductByIdAsync_ReadResponseAsStringInvalidJsonThrowsApiException()
        {
            var handler = new StubHttpMessageHandler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("not-json", Encoding.UTF8, "text/plain")
                };

                return Task.FromResult(response);
            });

            var api = new ProductAPI("http://localhost:5000", new HttpClient(handler))
            {
                ReadResponseAsString = true
            };

            var ex = await Assert.ThrowsAsync<ApiException>(() => api.GetProductByIdAsync(1));

            Assert.Contains("Could not deserialize", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        private static HttpResponseMessage JsonResponse(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _handler(request, cancellationToken);
            }
        }
    }
}
