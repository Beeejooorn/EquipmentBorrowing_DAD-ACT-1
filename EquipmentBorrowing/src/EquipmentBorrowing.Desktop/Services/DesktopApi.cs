namespace EquipmentBorrowing.Desktop.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

// The desktop has no database credentials. Every read and write goes through the API.
public sealed class DesktopApi : IDesktopApi
{
    private readonly HttpClient _http;

    public DesktopApi(HttpClient http) => _http = http;

    public Task<QueryResult<Equipment>> GetAllEquipmentAsync() => GetListAsync("api/v1/equipment", ReadEquipment);
    public Task<QueryResult<Equipment>> GetAvailableEquipmentAsync() => GetListAsync("api/v1/equipment?available=true", ReadEquipment);
    public async Task<QueryResult<Equipment>> SearchEquipmentAsync(string? searchText)
    {
        QueryResult<Equipment> result = await GetAllEquipmentAsync();
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(searchText)) return result;
        return QueryResult<Equipment>.Ok(result.Items.Where(item =>
            item.EquipmentCode.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            (item.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)).ToList());
    }
    public Task<QueryResult<Student>> GetAllStudentsAsync() => GetListAsync("api/v1/students", ReadStudent);
    public Task<QueryResult<Student>> SearchStudentsAsync(string? searchText) =>
        GetListAsync("api/v1/students?search=" + Uri.EscapeDataString(searchText ?? ""), ReadStudent);
    public Task<QueryResult<BorrowingRecord>> GetActiveBorrowingsAsync() =>
        GetListAsync("api/v1/borrowings?status=Active", ReadBorrowing);
    public Task<QueryResult<BorrowingRecord>> GetStudentHistoryAsync(int studentId) =>
        GetListAsync($"api/v1/students/{studentId}/borrowings", ReadBorrowing);
    public Task<QueryResult<BorrowingRecord>> GetEquipmentHistoryAsync(int equipmentId) =>
        GetListAsync($"api/v1/equipment/{equipmentId}/borrowings", ReadBorrowing);

    public Task<QueryResult<BorrowingRecord>> SearchBorrowingsAsync(BorrowingSearchCriteria criteria)
    {
        string search = Uri.EscapeDataString(criteria.SearchText ?? "");
        string sort = criteria.SortBy == BorrowingSortField.ExpectedReturnDate ? "expectedReturnDate" : "borrowedDate";
        string direction = criteria.SortDescending ? "desc" : "asc";
        return GetListAsync($"api/v1/borrowings?search={search}&status={criteria.Status}&sort={sort}&direction={direction}", ReadBorrowing);
    }

    public Task<OperationResult> BorrowAsync(int studentId, int equipmentId, DateOnly? expectedReturnDate) =>
        SendAsync(HttpMethod.Post, "api/v1/borrowings", new { studentId, equipmentId, expectedReturnDate });
    public Task<OperationResult> CheckOutMultipleAsync(int studentId, List<int> equipmentIds, DateOnly? expectedReturnDate) =>
        SendAsync(HttpMethod.Post, "api/v1/checkout-requests", new { studentId, equipmentIds, expectedReturnDate });
    public Task<OperationResult> ReturnAsync(int borrowingId) =>
        SendAsync(HttpMethod.Post, $"api/v1/borrowings/{borrowingId}/return", null);
    public Task<OperationResult> RegisterStudentAsync(string? studentNumber, string? fullName, string? contactInfo) =>
        SendAsync(HttpMethod.Post, "api/v1/students", new { studentNumber, fullName, contact = contactInfo });
    public Task<OperationResult> UpdateStudentAsync(int studentId, string? studentNumber, string? fullName, string? contactInfo) =>
        SendAsync(HttpMethod.Put, $"api/v1/students/{studentId}", new { studentNumber, fullName, contact = contactInfo });
    public Task<OperationResult> ChangeEligibilityAsync(int studentId, bool allowToBorrow) =>
        SendAsync(HttpMethod.Patch, $"api/v1/students/{studentId}/eligibility", new { isEligible = allowToBorrow });
    public Task<OperationResult> RegisterEquipmentAsync(string? equipmentCode, string? name, string? description) =>
        SendAsync(HttpMethod.Post, "api/v1/equipment", new { equipmentCode, name, description });
    public Task<OperationResult> UpdateEquipmentAsync(int equipmentId, string? equipmentCode, string? name, string? description) =>
        SendAsync(HttpMethod.Put, $"api/v1/equipment/{equipmentId}", new { equipmentCode, name, description });

    private async Task<QueryResult<T>> GetListAsync<T>(string path, Func<JsonElement, T> read)
    {
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return QueryResult<T>.NotFound(await ReadMessageAsync(response));
            if (!response.IsSuccessStatusCode)
                return QueryResult<T>.Error("The API responded with an error: " + await ReadMessageAsync(response));
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return QueryResult<T>.Ok(document.RootElement.EnumerateArray().Select(read).ToList());
        }
        catch (HttpRequestException)
        {
            return QueryResult<T>.Error("Cannot reach the API. Start the API, then refresh this screen.");
        }
        catch (TaskCanceledException)
        {
            return QueryResult<T>.Error("The API did not respond in time. Try refreshing this screen.");
        }
        catch (JsonException)
        {
            return QueryResult<T>.Error("The API returned data this desktop could not read.");
        }
    }

    private async Task<OperationResult> SendAsync(HttpMethod method, string path, object? body)
    {
        try
        {
            using HttpRequestMessage request = new(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            using HttpResponseMessage response = await _http.SendAsync(request);
            string text = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            if (response.IsSuccessStatusCode)
            {
                int? id = root.TryGetProperty("id", out JsonElement value) && value.ValueKind == JsonValueKind.Number
                    ? value.GetInt32() : null;
                return OperationResult.Success("Saved through the API.", id);
            }
            string message = root.TryGetProperty("message", out JsonElement error) ? error.GetString() ?? "Request failed." : "Request failed.";
            string code = root.TryGetProperty("code", out JsonElement codeValue) ? codeValue.GetString() ?? "" : "";
            if (response.StatusCode == HttpStatusCode.NotFound) return OperationResult.NotFound(message);
            if (code == "unknown_outcome" || code == "readback_failed") return new OperationResult(ResultKind.UnknownOutcome, message);
            if (response.StatusCode == HttpStatusCode.Conflict) return new OperationResult(ResultKind.Conflict, message);
            if (response.StatusCode == HttpStatusCode.BadRequest) return OperationResult.Rejected(message);
            return new OperationResult(ResultKind.Failed, "The API responded with an error: " + message);
        }
        catch (HttpRequestException)
        {
            return new OperationResult(ResultKind.UnknownOutcome, "Cannot reach the API. Check the current list before retrying this change.");
        }
        catch (TaskCanceledException)
        {
            return new OperationResult(ResultKind.UnknownOutcome, "The API did not respond in time. Check the current list before retrying this change.");
        }
        catch (JsonException)
        {
            return new OperationResult(ResultKind.UnknownOutcome, "The API response could not be read. Check the current list before retrying this change.");
        }
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.TryGetProperty("message", out JsonElement value)
                ? value.GetString() ?? response.ReasonPhrase ?? "Request failed."
                : response.ReasonPhrase ?? "Request failed.";
        }
        catch (JsonException) { return response.ReasonPhrase ?? "Request failed."; }
    }

    private static Equipment ReadEquipment(JsonElement value) => new(
        value.GetProperty("id").GetInt32(), value.GetProperty("equipmentCode").GetString() ?? "",
        value.GetProperty("name").GetString() ?? "", OptionalString(value, "description"),
        value.GetProperty("isAvailable").GetBoolean());

    private static Student ReadStudent(JsonElement value) => new(
        value.GetProperty("id").GetInt32(), value.GetProperty("studentNumber").GetString() ?? "",
        value.GetProperty("fullName").GetString() ?? "", OptionalString(value, "contact"),
        value.GetProperty("isEligible").GetBoolean());

    private static BorrowingRecord ReadBorrowing(JsonElement value) => new()
    {
        BorrowingId = value.GetProperty("id").GetInt32(),
        StudentId = value.GetProperty("studentId").GetInt32(),
        StudentNumber = value.GetProperty("studentNumber").GetString() ?? "",
        StudentName = value.GetProperty("studentName").GetString() ?? "",
        EquipmentId = value.GetProperty("equipmentId").GetInt32(),
        EquipmentCode = value.GetProperty("equipmentCode").GetString() ?? "",
        EquipmentName = value.GetProperty("equipmentName").GetString() ?? "",
        DateBorrowed = DateOnly.Parse(value.GetProperty("borrowedDate").GetString()!),
        ExpectedReturnDate = DateOnly.Parse(value.GetProperty("expectedReturnDate").GetString()!),
        ReturnedAt = value.TryGetProperty("returnedDate", out JsonElement returned) && returned.ValueKind != JsonValueKind.Null
            ? DateOnly.Parse(returned.GetString()!) : null,
        Status = Enum.Parse<BorrowingStatus>(value.GetProperty("status").GetString() ?? "Active", true)
    };

    private static string? OptionalString(JsonElement value, string property) =>
        value.TryGetProperty(property, out JsonElement item) && item.ValueKind != JsonValueKind.Null ? item.GetString() : null;
}
