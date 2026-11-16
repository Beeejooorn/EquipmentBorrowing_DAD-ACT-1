using System.Text.Json;
using EquipmentBorrowing.Lab6.Tests.Support;

namespace EquipmentBorrowing.Lab6.Tests;

// OpenAPI consistency (rubric R2): the published contract at /openapi/v1.json must list
// exactly the sixteen API operations (API-01 to API-16) that the running API really serves.
public class OpenApiContractTests : ApiTestBase
{
    // API number, HTTP method, route, the success status code the contract must declare.
    private static readonly string[][] ExpectedOperations = new string[][]
    {
        new[] { "API-01", "get",   "/api/v1/equipment",                      "200" },
        new[] { "API-02", "get",   "/api/v1/students",                       "200" },
        new[] { "API-03", "get",   "/api/v1/students/{id}",                  "200" },
        new[] { "API-04", "post",  "/api/v1/students",                       "201" },
        new[] { "API-05", "put",   "/api/v1/students/{id}",                  "200" },
        new[] { "API-06", "patch", "/api/v1/students/{id}/eligibility",      "200" },
        new[] { "API-07", "get",   "/api/v1/equipment/{id}",                 "200" },
        new[] { "API-08", "post",  "/api/v1/equipment",                      "201" },
        new[] { "API-09", "put",   "/api/v1/equipment/{id}",                 "200" },
        new[] { "API-10", "post",  "/api/v1/borrowings",                     "201" },
        new[] { "API-11", "post",  "/api/v1/checkout-requests",              "201" },
        new[] { "API-12", "post",  "/api/v1/borrowings/{id}/return",         "200" },
        new[] { "API-13", "get",   "/api/v1/borrowings/{id}",                "200" },
        new[] { "API-14", "get",   "/api/v1/borrowings",                     "200" },
        new[] { "API-15", "get",   "/api/v1/students/{id}/borrowings",       "200" },
        new[] { "API-16", "get",   "/api/v1/equipment/{id}/borrowings",      "200" },
    };

    private static readonly string[] HttpMethods = { "get", "post", "put", "patch", "delete" };

    [Fact]
    public async Task OpenApi_document_is_served_and_lists_exactly_the_sixteen_operations()
    {
        ApiResponse response = await GetAsync("/openapi/v1.json");

        Assert.Equal(200, response.Status);
        JsonElement paths = response.Json().GetProperty("paths");

        // Collect every "METHOD route" that the contract declares.
        List<string> declared = new List<string>();

        foreach (JsonProperty path in paths.EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                if (HttpMethods.Contains(operation.Name))
                {
                    declared.Add(operation.Name + " " + path.Name);
                }
            }
        }

        Assert.Equal(16, declared.Count);

        foreach (string[] expected in ExpectedOperations)
        {
            Assert.True(declared.Contains(expected[1] + " " + expected[2]),
                expected[0] + " is missing from the OpenAPI contract: " + expected[1] + " " + expected[2]);
        }
    }

    [Fact]
    public async Task OpenApi_declares_the_success_status_code_of_every_operation()
    {
        ApiResponse response = await GetAsync("/openapi/v1.json");
        Assert.Equal(200, response.Status);
        JsonElement paths = response.Json().GetProperty("paths");

        foreach (string[] expected in ExpectedOperations)
        {
            JsonElement operation = paths.GetProperty(expected[2]).GetProperty(expected[1]);
            bool hasStatus = operation.GetProperty("responses").TryGetProperty(expected[3], out _);

            Assert.True(hasStatus, expected[0] + " does not declare status " + expected[3]);
        }
    }

    // The contract is only consistent if the real API answers on each declared route
    // (any status except 404 "unknown route" and 405 "wrong method").
    [Fact]
    public async Task Every_operation_in_the_contract_is_really_routed_by_the_api()
    {
        foreach (string[] expected in ExpectedOperations)
        {
            string route = expected[2].Replace("{id}", "999999");

            HttpMethod method = new HttpMethod(expected[1].ToUpperInvariant());
            ApiResponse answer = await SendAsync(method, route, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

            Assert.NotEqual(405, answer.Status);

            // A 404 is fine when the body is the API's own "not_found" answer (the id 999999 does not exist),
            // but not when the route itself is unknown (ASP.NET returns an empty body then).
            if (answer.Status == 404)
            {
                Assert.Contains("not_found", answer.Body);
            }
        }
    }
}
