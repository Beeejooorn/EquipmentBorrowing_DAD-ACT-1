using System.Text.Json;
using EquipmentBorrowing.Lab6.Tests.Support;

namespace EquipmentBorrowing.Lab6.Tests;

// TC6-U01 Catalog (UC-01, API-01) and TC6-U02 Available filtering (UC-02, API-01).
// Both are read-only: the database must be exactly the same after the requests.
public class CatalogTests : ApiTestBase
{
    // ---------------- TC6-U01 ----------------

    [Fact]
    public async Task TC6_U01_Catalog_returns_exactly_the_seven_items_with_ids_names_and_availability()
    {
        string before = LabDatabase.Snapshot();

        ApiResponse response = await GetAsync("/api/v1/equipment");

        Assert.Equal(200, response.Status);
        JsonElement list = response.Json();
        Assert.Equal(7, list.GetArrayLength());

        // Expected items (the list is sorted by equipment code).
        string[] codes = { "EQ-001", "EQ-002", "EQ-003", "EQ-004", "EQ-005", "EQ-006", "EQ-007" };
        string[] names = { "Camera", "Laptop", "Projector", "Tablet", "Microphone", "Tripod", "Speaker" };
        bool[] available = { false, true, true, false, false, true, true };

        for (int i = 0; i < 7; i++)
        {
            JsonElement item = list[i];

            // The Id must be the real Id stored in PostgreSQL.
            Assert.Equal(LabDatabase.FindEquipment(codes[i])!.Id, item.GetProperty("id").GetInt32());
            Assert.Equal(codes[i], item.GetProperty("equipmentCode").GetString());
            Assert.Equal(names[i], item.GetProperty("name").GetString());
            Assert.Equal(available[i], item.GetProperty("isAvailable").GetBoolean());
        }

        // Expected database state: no changes.
        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public async Task TC6_U01_Catalog_of_an_empty_database_is_an_empty_list_not_an_error()
    {
        LabDatabase.EmptyAllTables();

        ApiResponse response = await GetAsync("/api/v1/equipment");

        Assert.Equal(200, response.Status);
        Assert.Equal(JsonValueKind.Array, response.Json().ValueKind);
        Assert.Equal(0, response.Json().GetArrayLength());
        Assert.Equal(0, LabDatabase.CountEquipment());
    }

    // ---------------- TC6-U02 ----------------

    [Fact]
    public async Task TC6_U02_Available_filter_includes_E2_E3_E6_E7_and_excludes_E1_E4_E5()
    {
        string before = LabDatabase.Snapshot();

        ApiResponse response = await GetAsync("/api/v1/equipment?available=true");

        Assert.Equal(200, response.Status);
        List<string> codes = CodesOf(response.Json());

        Assert.Equal(new List<string> { "EQ-002", "EQ-003", "EQ-006", "EQ-007" }, codes);

        // The borrowed items must not appear (E1 Camera, E4 Tablet, E5 Microphone).
        Assert.DoesNotContain("EQ-001", codes);
        Assert.DoesNotContain("EQ-004", codes);
        Assert.DoesNotContain("EQ-005", codes);

        // Every returned item says it is available.
        foreach (JsonElement item in response.Json().EnumerateArray())
        {
            Assert.True(item.GetProperty("isAvailable").GetBoolean());
        }

        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public async Task TC6_U02_Available_filter_returns_an_empty_list_when_nothing_is_available()
    {
        // Test setup: make every item unavailable directly in PostgreSQL.
        Assert.Null(LabDatabase.TryRawSql("UPDATE \"Equipment\" SET \"IsAvailable\" = false"));
        string before = LabDatabase.Snapshot();

        ApiResponse response = await GetAsync("/api/v1/equipment?available=true");

        Assert.Equal(200, response.Status);
        Assert.Equal(0, response.Json().GetArrayLength());
        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public async Task TC6_U02_Available_filter_also_hides_an_item_that_has_an_active_borrowing()
    {
        // BR-10: "available" means the flag is true AND there is no active borrowing.
        // Test setup: wrongly mark E1 (which S1 still has) as available directly in the database.
        Assert.Null(LabDatabase.TryRawSql("UPDATE \"Equipment\" SET \"IsAvailable\" = true WHERE \"EquipmentCode\" = 'EQ-001'"));

        ApiResponse response = await GetAsync("/api/v1/equipment?available=true");

        Assert.Equal(200, response.Status);
        Assert.DoesNotContain("EQ-001", CodesOf(response.Json()));
    }
}
