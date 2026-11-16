using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace EquipmentBorrowing.Lab6.Tests.Support;

// What one HTTP call returned: the status code, the body text and the Location header.
public class ApiResponse
{
    public int Status { get; set; }
    public string Body { get; set; } = "";
    public string? Location { get; set; }

    // The body as JSON (a copy that stays valid after this method returns).
    public JsonElement Json()
    {
        using JsonDocument document = JsonDocument.Parse(Body);
        return document.RootElement.Clone();
    }
}

// Base class for every Lab 6 HTTP test.
// xUnit creates a NEW instance for every test, so every test:
//   1. resets the PostgreSQL test database to the S1-S4 / E1-E7 fixtures,
//   2. starts its own copy of the real API.
// That is why no test depends on another test or on the execution order.
public abstract class ApiTestBase : IDisposable
{
    protected readonly LabApiFactory Factory;
    protected readonly HttpClient Client;

    protected ApiTestBase()
    {
        LabDatabase.ResetAndSeed();

        Factory = new LabApiFactory();
        Client = Factory.CreateClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    // ---------- the documented test date D ----------

    protected DateOnly D
    {
        get { return LabDatabase.D; }
    }

    // D plus or minus some days, written like "2026-11-16".
    protected string Day(int offset)
    {
        return LabDatabase.Iso(LabDatabase.D.AddDays(offset));
    }

    // ---------- sending requests ----------

    protected async Task<ApiResponse> SendAsync(HttpMethod method, string path, HttpContent? content = null)
    {
        using HttpRequestMessage request = new HttpRequestMessage(method, path);
        request.Content = content;

        using HttpResponseMessage response = await Client.SendAsync(request);

        ApiResponse result = new ApiResponse();
        result.Status = (int)response.StatusCode;
        result.Body = await response.Content.ReadAsStringAsync();
        result.Location = response.Headers.Location?.ToString();
        return result;
    }

    protected Task<ApiResponse> GetAsync(string path)
    {
        return SendAsync(HttpMethod.Get, path);
    }

    protected Task<ApiResponse> PostAsync(string path, object body)
    {
        return SendAsync(HttpMethod.Post, path, JsonContent.Create(body));
    }

    protected Task<ApiResponse> PutAsync(string path, object body)
    {
        return SendAsync(HttpMethod.Put, path, JsonContent.Create(body));
    }

    protected Task<ApiResponse> PatchAsync(string path, object body)
    {
        return SendAsync(HttpMethod.Patch, path, JsonContent.Create(body));
    }

    // Sends text exactly as written (used for invalid JSON).
    protected Task<ApiResponse> PostRawAsync(string path, string rawBody)
    {
        StringContent content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        return SendAsync(HttpMethod.Post, path, content);
    }

    // ---------- looking up Ids through the API (the suite says: use IDs resolved from the API) ----------

    protected async Task<int> StudentIdAsync(string studentNumber)
    {
        ApiResponse response = await GetAsync("/api/v1/students?search=" + Uri.EscapeDataString(studentNumber));
        Assert.Equal(200, response.Status);

        foreach (JsonElement student in response.Json().EnumerateArray())
        {
            if (student.GetProperty("studentNumber").GetString() == studentNumber)
            {
                return student.GetProperty("id").GetInt32();
            }
        }

        throw new InvalidOperationException("Student " + studentNumber + " was not returned by the API.");
    }

    protected async Task<int> EquipmentIdAsync(string equipmentCode)
    {
        ApiResponse response = await GetAsync("/api/v1/equipment");
        Assert.Equal(200, response.Status);

        foreach (JsonElement item in response.Json().EnumerateArray())
        {
            if (item.GetProperty("equipmentCode").GetString() == equipmentCode)
            {
                return item.GetProperty("id").GetInt32();
            }
        }

        throw new InvalidOperationException("Equipment " + equipmentCode + " was not returned by the API.");
    }

    // ---------- small helpers for reading JSON lists ----------

    // The equipment codes of a list of equipment, in the order returned. Example: ["EQ-002", "EQ-003"].
    protected static List<string> CodesOf(JsonElement list)
    {
        List<string> codes = new List<string>();

        foreach (JsonElement item in list.EnumerateArray())
        {
            codes.Add(item.GetProperty("equipmentCode").GetString()!);
        }

        return codes;
    }

    // The borrowing equipment codes of a list of borrowings, in the order returned.
    protected static List<string> BorrowedCodesOf(JsonElement list)
    {
        return CodesOf(list);
    }

    // Checks that an error response has a safe body: it has "code" and "message" and
    // shows no stack trace, exception type, SQL or connection details (BR-18).
    protected static void AssertSafeErrorBody(ApiResponse response)
    {
        JsonElement body = response.Json();
        Assert.True(body.TryGetProperty("code", out _), "The error body has no 'code'.");
        Assert.True(body.TryGetProperty("message", out _), "The error body has no 'message'.");

        string text = response.Body;
        Assert.DoesNotContain("Exception", text);
        Assert.DoesNotContain("StackTrace", text);
        Assert.DoesNotContain("   at ", text);
        Assert.DoesNotContain("Npgsql", text);
        Assert.DoesNotContain("Host=", text);
        Assert.DoesNotContain("Password", text);
    }
}
