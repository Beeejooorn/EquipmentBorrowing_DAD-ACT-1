using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using EquipmentBorrowing.Lab6.Tests.Support;

namespace EquipmentBorrowing.Lab6.Tests;

// TC6-E08 Fresh connections and restart
//   Part 1 (automated): create a student and an item, issue, return. After every step the state is
//           read through a NEW PostgreSQL connection (the old context is already disposed).
//   Part 2 (restart harness): start the REAL API as its own process, create and return records,
//           STOP the process, START it again and check that the same data comes back.
public class PersistenceTests : ApiTestBase
{
    // ---------------- Part 1: fresh connections ----------------

    [Fact]
    public async Task TC6_E08_Created_issued_and_returned_state_is_visible_through_fresh_connections()
    {
        // 1. Create a student and an equipment item through the API.
        ApiResponse student = await PostAsync("/api/v1/students", new
        {
            studentNumber = "ST-800",
            fullName = "Fresh Student",
            contact = "fresh@example.test"
        });
        Assert.Equal(201, student.Status);
        int studentId = student.Json().GetProperty("id").GetInt32();

        ApiResponse item = await PostAsync("/api/v1/equipment", new
        {
            equipmentCode = "EQ-800",
            name = "Fresh Item",
            description = "created in E08"
        });
        Assert.Equal(201, item.Status);
        int equipmentId = item.Json().GetProperty("id").GetInt32();

        // A fresh connection sees both records with the right values.
        var savedStudent = LabDatabase.FindStudent("ST-800");
        Assert.NotNull(savedStudent);
        Assert.Equal(studentId, savedStudent!.Id);
        Assert.Equal("Fresh Student", savedStudent.Name);
        Assert.True(savedStudent.IsAllowedToBorrow);

        var savedItem = LabDatabase.FindEquipment("EQ-800");
        Assert.NotNull(savedItem);
        Assert.Equal(equipmentId, savedItem!.Id);
        Assert.True(savedItem.IsAvailable);

        // 2. Issue the item.
        ApiResponse issued = await PostAsync("/api/v1/borrowings", new
        {
            studentId = studentId,
            equipmentId = equipmentId,
            expectedReturnDate = Day(2)
        });
        Assert.Equal(201, issued.Status);
        int borrowingId = issued.Json().GetProperty("id").GetInt32();

        // Another fresh connection: one active borrowing linking the right student and item.
        var activeRow = LabDatabase.FindBorrowing(borrowingId);
        Assert.NotNull(activeRow);
        Assert.Equal(studentId, activeRow!.StudentId);
        Assert.Equal(equipmentId, activeRow.EquipmentId);
        Assert.Null(activeRow.ReturnedAt);
        Assert.Equal(1, LabDatabase.CountActiveBorrowings(studentId));
        Assert.False(LabDatabase.FindEquipment("EQ-800")!.IsAvailable);

        // 3. Return the item.
        ApiResponse returned = await PostAsync("/api/v1/borrowings/" + borrowingId + "/return", new { });
        Assert.Equal(200, returned.Status);

        // Yet another fresh connection: the history is kept, the item is available again.
        var returnedRow = LabDatabase.FindBorrowing(borrowingId);
        Assert.NotNull(returnedRow);
        Assert.Equal(D, returnedRow!.ReturnedAt);
        Assert.Equal(0, LabDatabase.CountActiveBorrowings(studentId));
        Assert.True(LabDatabase.FindEquipment("EQ-800")!.IsAvailable);

        // The history API (new request, new database connection) agrees.
        ApiResponse history = await GetAsync("/api/v1/students/" + studentId + "/borrowings");
        Assert.Equal(200, history.Status);
        Assert.Equal(1, history.Json().GetArrayLength());
        Assert.Equal("Returned", history.Json()[0].GetProperty("status").GetString());
    }

    // ---------------- Part 2: real API restart ----------------

    private const string RestartUrl = "http://127.0.0.1:5188";

    // Finds EquipmentBorrowing.Api.dll that "dotnet test" has just built.
    private static string FindApiDll()
    {
        // The tests run from  tests/<project>/bin/<Configuration>/net10.0/
        DirectoryInfo outputFolder = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        string framework = outputFolder.Name;
        string configuration = outputFolder.Parent!.Name;

        DirectoryInfo? folder = outputFolder;
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "EquipmentBorrowing.slnx")))
        {
            folder = folder.Parent;
        }

        Assert.NotNull(folder);

        string dll = Path.Combine(folder!.FullName, "src", "EquipmentBorrowing.Api", "bin", configuration, framework, "EquipmentBorrowing.Api.dll");
        Assert.True(File.Exists(dll), "API build not found: " + dll);
        return dll;
    }

    // Starts the API as a separate operating system process and waits until it answers.
    private static async Task<Process> StartApiProcessAsync(string dll)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo("dotnet");
        startInfo.ArgumentList.Add(dll);
        startInfo.WorkingDirectory = Path.GetDirectoryName(dll)!;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.Environment["ConnectionStrings__EquipmentBorrowing"] = LabDatabase.GetConnectionString();
        startInfo.Environment["ASPNETCORE_URLS"] = RestartUrl;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        Process process = Process.Start(startInfo)!;
        process.OutputDataReceived += (sender, e) => { };
        process.ErrorDataReceived += (sender, e) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using HttpClient probe = new HttpClient();
        probe.Timeout = TimeSpan.FromSeconds(2);

        for (int attempt = 0; attempt < 60; attempt++)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException("The API process stopped early. Exit code " + process.ExitCode
                    + ". Is port 5188 already in use?");
            }

            try
            {
                HttpResponseMessage answer = await probe.GetAsync(RestartUrl + "/");

                if (answer.IsSuccessStatusCode)
                {
                    return process;
                }
            }
            catch (HttpRequestException)
            {
                // not ready yet
            }
            catch (TaskCanceledException)
            {
                // not ready yet
            }

            await Task.Delay(500);
        }

        StopApiProcess(process);
        throw new InvalidOperationException("The API process did not answer within 30 seconds.");
    }

    private static void StopApiProcess(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(true);
            process.WaitForExit(10000);
        }
    }

    [Fact]
    public async Task TC6_E08_Real_API_process_restart_keeps_the_created_and_returned_records()
    {
        string dll = FindApiDll();
        HttpClient http = new HttpClient();
        http.BaseAddress = new Uri(RestartUrl);
        http.Timeout = TimeSpan.FromSeconds(10);

        int studentId;
        int equipmentId;
        int borrowingId;

        // ---- First run of the API ----
        Process first = await StartApiProcessAsync(dll);

        try
        {
            HttpResponseMessage student = await http.PostAsJsonAsync("/api/v1/students", new
            {
                studentNumber = "ST-810",
                fullName = "Restart Student",
                contact = "restart@example.test"
            });
            Assert.Equal(201, (int)student.StatusCode);
            studentId = JsonDocument.Parse(await student.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();

            HttpResponseMessage item = await http.PostAsJsonAsync("/api/v1/equipment", new
            {
                equipmentCode = "EQ-810",
                name = "Restart Item",
                description = "created before the restart"
            });
            Assert.Equal(201, (int)item.StatusCode);
            equipmentId = JsonDocument.Parse(await item.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();

            HttpResponseMessage issued = await http.PostAsJsonAsync("/api/v1/borrowings", new
            {
                studentId = studentId,
                equipmentId = equipmentId,
                expectedReturnDate = Day(2)
            });
            Assert.Equal(201, (int)issued.StatusCode);
            borrowingId = JsonDocument.Parse(await issued.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();

            HttpResponseMessage returned = await http.PostAsync("/api/v1/borrowings/" + borrowingId + "/return", JsonContent.Create(new { }));
            Assert.Equal(200, (int)returned.StatusCode);
        }
        finally
        {
            // ---- Stop the API ----
            StopApiProcess(first);
        }

        // While it is stopped, nobody answers (proves the restart is real).
        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync("/api/v1/students/" + studentId));

        // ---- Second run of the API (a brand new process) ----
        Process second = await StartApiProcessAsync(dll);

        try
        {
            HttpResponseMessage student = await http.GetAsync("/api/v1/students/" + studentId);
            Assert.Equal(200, (int)student.StatusCode);
            JsonElement studentJson = JsonDocument.Parse(await student.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("ST-810", studentJson.GetProperty("studentNumber").GetString());
            Assert.Equal("Restart Student", studentJson.GetProperty("fullName").GetString());

            HttpResponseMessage item = await http.GetAsync("/api/v1/equipment/" + equipmentId);
            Assert.Equal(200, (int)item.StatusCode);
            JsonElement itemJson = JsonDocument.Parse(await item.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("EQ-810", itemJson.GetProperty("equipmentCode").GetString());
            Assert.True(itemJson.GetProperty("isAvailable").GetBoolean());

            HttpResponseMessage borrowing = await http.GetAsync("/api/v1/borrowings/" + borrowingId);
            Assert.Equal(200, (int)borrowing.StatusCode);
            JsonElement borrowingJson = JsonDocument.Parse(await borrowing.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("Returned", borrowingJson.GetProperty("status").GetString());
            Assert.Equal(studentId, borrowingJson.GetProperty("studentId").GetInt32());
            Assert.Equal(equipmentId, borrowingJson.GetProperty("equipmentId").GetInt32());
            Assert.Equal(Day(0), borrowingJson.GetProperty("returnedDate").GetString());
        }
        finally
        {
            StopApiProcess(second);
        }

        // ---- Compare with a fresh PostgreSQL read ----
        var savedStudent = LabDatabase.FindStudent("ST-810");
        Assert.NotNull(savedStudent);
        Assert.Equal(studentId, savedStudent!.Id);

        var savedItem = LabDatabase.FindEquipment("EQ-810");
        Assert.NotNull(savedItem);
        Assert.Equal(equipmentId, savedItem!.Id);
        Assert.True(savedItem.IsAvailable);

        var savedBorrowing = LabDatabase.FindBorrowing(borrowingId);
        Assert.NotNull(savedBorrowing);
        Assert.Equal(D, savedBorrowing!.ReturnedAt);
        Assert.Equal(0, LabDatabase.CountActiveBorrowings(studentId));
    }
}
