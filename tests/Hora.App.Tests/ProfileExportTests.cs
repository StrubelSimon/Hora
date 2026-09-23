using System.IO;
using Hora.App.Tests.TestDoubles;
using Hora.App.ViewModels;
using Hora.Core.Jira;
using Hora.Core.Jira.Models;
using Hora.Core.Storage;
using Hora.Core.Worklogs;

namespace Hora.App.Tests;

/// <summary>
/// Der Export-Abschnitt der Profilseite: Monat wählen, zwei Schalter, CSV ablegen. Als „heute“
/// gilt in allen Tests der 22.09.2026 — der laufende Monat ist also September 2026.
/// </summary>
public sealed class ProfileExportTests : IDisposable
{
    private const string BaseUrl = "https://advisori-ftc.atlassian.net";

    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly TimeSpan Cest = TimeSpan.FromHours(2);
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 17, 38, 0, Cest);

    private static readonly JiraUser User = new()
    {
        AccountId = "me",
        DisplayName = "Simon Strubel",
        EmailAddress = "simon.strubel@advisori.de",
    };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "HoraTests", Guid.NewGuid().ToString("N"));

    public ProfileExportTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string PathInTempFolder(string name = "export.csv") => Path.Combine(_root, name);

    private static WorklogEntry Entry(string issueKey, int day, int durationMinutes, string summary = "Titel") =>
        WorklogEntry.Jira(issueKey, summary, new DateTimeOffset(2026, 9, day, 9, 0, 0, Cest), TimeSpan.FromMinutes(durationMinutes), string.Empty, BaseUrl);

    private sealed record Fixture(
        ProfileViewModel Profile,
        FakeSettingsStore Store,
        FakeWorklogService Worklogs,
        FakeSaveFilePicker Picker,
        RecordingLogger Logger);

    private static Fixture Create(AppSettings? settings = null, WorkSchedule? schedule = null)
    {
        var store = new FakeSettingsStore();
        if (settings is not null)
        {
            store.Settings = settings;
        }

        var worklogs = new FakeWorklogService();
        var picker = new FakeSaveFilePicker();
        var logger = new RecordingLogger();

        var profile = new ProfileViewModel(
            store,
            User,
            BaseUrl,
            schedule ?? WorkSchedule.Default(),
            themeSwitcher: null,
            worklogs: worklogs,
            zone: Berlin,
            logger: logger,
            saveFilePicker: picker,
            now: () => Now);

        return new Fixture(profile, store, worklogs, picker, logger);
    }

    // ===================== Monatswahl =====================

    [Fact]
    public void The_export_starts_on_the_current_month()
    {
        var fixture = Create();

        Assert.Equal(new DateOnly(2026, 9, 1), fixture.Profile.ExportMonth);
        Assert.Equal("September 2026", fixture.Profile.ExportMonthLabel);
    }

    [Fact]
    public void Stepping_back_walks_through_the_previous_months()
    {
        var fixture = Create();

        fixture.Profile.MoveExportMonthBackCommand.Execute(null);
        Assert.Equal("August 2026", fixture.Profile.ExportMonthLabel);

        for (var step = 0; step < 8; step++)
        {
            fixture.Profile.MoveExportMonthBackCommand.Execute(null);
        }

        Assert.Equal("Dezember 2025", fixture.Profile.ExportMonthLabel);
    }

    [Fact]
    public void Stepping_forward_stops_at_the_current_month()
    {
        // Ein künftiger Monat hat nichts zu exportieren.
        var fixture = Create();

        Assert.False(fixture.Profile.MoveExportMonthForwardCommand.CanExecute(null));

        fixture.Profile.MoveExportMonthBackCommand.Execute(null);
        Assert.True(fixture.Profile.MoveExportMonthForwardCommand.CanExecute(null));

        fixture.Profile.MoveExportMonthForwardCommand.Execute(null);
        Assert.Equal("September 2026", fixture.Profile.ExportMonthLabel);
        Assert.False(fixture.Profile.MoveExportMonthForwardCommand.CanExecute(null));
    }

    [Fact]
    public void The_suggested_file_name_carries_year_and_month()
    {
        var fixture = Create();

        Assert.Equal("Hora-Zeiten-2026-09.csv", fixture.Profile.SuggestedFileName);

        fixture.Profile.MoveExportMonthBackCommand.Execute(null);

        Assert.Equal("Hora-Zeiten-2026-08.csv", fixture.Profile.SuggestedFileName);
    }

    // ===================== Schalter =====================

    [Fact]
    public void Both_switches_start_from_the_stored_settings()
    {
        var fixture = Create(new AppSettings { ExportIncludeIssues = false, ExportIncludeTargets = false });

        Assert.False(fixture.Profile.IncludeIssueRows);
        Assert.False(fixture.Profile.IncludeTargets);
    }

    [Fact]
    public void Switching_the_issue_rows_off_is_saved_at_once()
    {
        var fixture = Create();

        fixture.Profile.IncludeIssueRows = false;

        Assert.False(fixture.Store.Settings.ExportIncludeIssues);
        Assert.True(fixture.Store.Settings.ExportIncludeTargets);
    }

    [Fact]
    public void Switching_the_targets_off_is_saved_at_once()
    {
        var fixture = Create();

        fixture.Profile.IncludeTargets = false;

        Assert.False(fixture.Store.Settings.ExportIncludeTargets);
        Assert.True(fixture.Store.Settings.ExportIncludeIssues);
    }

    // ===================== Abruf =====================

    [Fact]
    public async Task The_export_asks_jira_for_exactly_the_chosen_month()
    {
        var fixture = Create();
        fixture.Picker.PathToReturn = PathInTempFolder();
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(new[] { Entry("HORA-12", 1, 480) }, Array.Empty<string>(), Array.Empty<string>());

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        var call = Assert.Single(fixture.Worklogs.Calls);
        Assert.Equal(new DateOnly(2026, 9, 1), call.From);
        Assert.Equal(new DateOnly(2026, 9, 30), call.To);
    }

    // ===================== Erfolg =====================

    [Fact]
    public async Task A_successful_export_writes_the_file_to_the_chosen_path()
    {
        var fixture = Create();
        var path = PathInTempFolder();
        fixture.Picker.PathToReturn = path;
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(
            new[] { Entry("HORA-12", 1, 480), Entry("HORA-7", 2, 405) },
            Array.Empty<string>(),
            Array.Empty<string>());

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        Assert.True(File.Exists(path));
        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("Zeitraum;September 2026", content);
        Assert.Contains("01.09.2026;Di;;;8,00;8,00;0,00", content);
    }

    [Fact]
    public async Task The_written_file_starts_with_the_utf8_byte_order_mark()
    {
        // Ohne die Kennung zeigt Excel Umlaute als Kauderwelsch.
        var fixture = Create();
        var path = PathInTempFolder();
        fixture.Picker.PathToReturn = path;
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(new[] { Entry("HORA-12", 1, 480) }, Array.Empty<string>(), Array.Empty<string>());

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
    }

    [Fact]
    public async Task The_status_line_names_the_exported_hours_and_days()
    {
        var fixture = Create();
        fixture.Picker.PathToReturn = PathInTempFolder();
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(
            new[] { Entry("HORA-12", 1, 480), Entry("HORA-7", 2, 405) },
            Array.Empty<string>(),
            Array.Empty<string>());

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        Assert.Equal("14,75 h aus 2 Tagen gespeichert", fixture.Profile.ExportStatusText);
    }

    [Fact]
    public async Task A_single_day_is_named_in_the_singular()
    {
        var fixture = Create();
        fixture.Picker.PathToReturn = PathInTempFolder();
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(new[] { Entry("HORA-12", 1, 480) }, Array.Empty<string>(), Array.Empty<string>());

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        Assert.Equal("8,00 h aus 1 Tag gespeichert", fixture.Profile.ExportStatusText);
    }

    [Fact]
    public async Task The_switches_reach_the_written_file()
    {
        var fixture = Create(new AppSettings { ExportIncludeIssues = false, ExportIncludeTargets = false });
        var path = PathInTempFolder();
        fixture.Picker.PathToReturn = path;
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(new[] { Entry("HORA-12", 1, 480) }, Array.Empty<string>(), Array.Empty<string>());

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("Datum;Tag;Ticket;Beschreibung;Stunden\r\n", content);
        Assert.DoesNotContain("HORA-12", content);
    }

    [Fact]
    public async Task Issues_that_could_not_be_loaded_are_appended_to_the_success_message()
    {
        // Eine unvollständige Datei darf nicht als vollständig durchgehen.
        var fixture = Create();
        fixture.Picker.PathToReturn = PathInTempFolder();
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(
            new[] { Entry("HORA-12", 1, 480) },
            new[] { "HORA-40", "HORA-41" },
            Array.Empty<string>());

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        Assert.StartsWith("8,00 h aus 1 Tag gespeichert", fixture.Profile.ExportStatusText);
        Assert.Contains("2 Ticket(s) konnten nicht geladen werden", fixture.Profile.ExportStatusText);
    }

    // ===================== Leerer Monat und Abbruch =====================

    [Fact]
    public async Task An_empty_month_never_opens_the_save_dialog()
    {
        var fixture = Create();

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        Assert.Equal(0, fixture.Picker.Calls);
        Assert.Equal("Keine Buchungen im September 2026.", fixture.Profile.ExportStatusText);
    }

    [Fact]
    public async Task Cancelling_the_dialog_writes_nothing_and_says_nothing()
    {
        var fixture = Create();
        fixture.Picker.PathToReturn = null;
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(new[] { Entry("HORA-12", 1, 480) }, Array.Empty<string>(), Array.Empty<string>());

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.Picker.Calls);
        Assert.Empty(fixture.Profile.ExportStatusText);
        Assert.Empty(Directory.GetFiles(_root));
    }

    // ===================== Fehler =====================

    [Fact]
    public async Task A_failed_fetch_is_reported_in_the_status_line_and_written_to_the_log()
    {
        var fixture = Create();
        fixture.Worklogs.ThrowOnce = new JiraException("Jira ist nicht erreichbar.");

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        Assert.Contains("Jira ist nicht erreichbar.", fixture.Profile.ExportStatusText);
        Assert.Equal(0, fixture.Picker.Calls);
        Assert.NotEmpty(fixture.Logger.Errors);
    }

    [Fact]
    public async Task A_path_that_cannot_be_written_is_reported_instead_of_throwing()
    {
        var fixture = Create();
        // Ein Ordner, den es nicht gibt: Das Schreiben scheitert, die Seite darf es überleben.
        fixture.Picker.PathToReturn = Path.Combine(_root, "fehlt", "export.csv");
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(new[] { Entry("HORA-12", 1, 480) }, Array.Empty<string>(), Array.Empty<string>());

        await fixture.Profile.ExportCommand.ExecuteAsync(null);

        Assert.Contains("konnte nicht geschrieben werden", fixture.Profile.ExportStatusText);
        Assert.NotEmpty(fixture.Logger.Errors);
    }

    // ===================== Ohne Abrufdienst =====================

    [Fact]
    public void Without_a_worklog_service_the_export_stays_disabled()
    {
        // Genau der Zustand der bestehenden Tests, die die Profilseite ohne Export bauen.
        var profile = new ProfileViewModel(new FakeSettingsStore(), User, BaseUrl, WorkSchedule.Default());

        Assert.False(profile.CanExport);
        Assert.False(profile.ExportCommand.CanExecute(null));
    }

    // ===================== Ladezustand =====================

    [Fact]
    public async Task The_page_reports_that_it_is_busy_while_the_fetch_runs()
    {
        var fixture = Create();
        fixture.Picker.PathToReturn = PathInTempFolder();
        var gate = new TaskCompletionSource();
        fixture.Worklogs.Gate = gate;
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(new[] { Entry("HORA-12", 1, 480) }, Array.Empty<string>(), Array.Empty<string>());

        var running = fixture.Profile.ExportCommand.ExecuteAsync(null);
        Assert.True(fixture.Profile.IsExporting);

        gate.SetResult();
        await running;

        Assert.False(fixture.Profile.IsExporting);
    }

    [Fact]
    public async Task The_button_says_that_it_is_loading_while_the_fetch_runs()
    {
        var fixture = Create();
        fixture.Picker.PathToReturn = PathInTempFolder();
        var gate = new TaskCompletionSource();
        fixture.Worklogs.Gate = gate;
        fixture.Worklogs.Respond = (_, _) => new WorklogFetchResult(new[] { Entry("HORA-12", 1, 480) }, Array.Empty<string>(), Array.Empty<string>());

        Assert.Equal("Export", fixture.Profile.ExportButtonText);

        var running = fixture.Profile.ExportCommand.ExecuteAsync(null);
        Assert.Equal("Lädt …", fixture.Profile.ExportButtonText);

        gate.SetResult();
        await running;

        Assert.Equal("Export", fixture.Profile.ExportButtonText);
    }
}
