using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Themia.Core.Habits;
using Themia.Core.Mcp;
using Themia.Core.Notes;
using Themia.Core.Office;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-9960]..[S-9978] The assistants' server: JSON-RPC at the trust boundary, the HTTP rules, the
/// protocol, the tools over a made-up host, the token and the setups, and one real loopback round trip.
/// </summary>
public sealed class McpTests
{
    private const string Token = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ";

    // ---- [S-9963] [S-9964] JSON-RPC ---------------------------------------------------------------

    private static JsonRpcIncoming Parse(string json) => JsonRpc.Parse(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void S9963_a_request_is_parsed_with_its_id_method_and_params()
    {
        var call = Assert.IsType<JsonRpcCall>(Parse("""{"jsonrpc":"2.0","id":7,"method":"tools/list","params":{"cursor":"x"}}"""));
        Assert.Equal(JsonRpcId.Of(7), call.Id);
        Assert.Equal("tools/list", call.Method);
        Assert.Equal("x", call.Params!.Value.GetProperty("cursor").GetString());

        var named = Assert.IsType<JsonRpcCall>(Parse("""{"jsonrpc":"2.0","id":"a-1","method":"ping"}"""));
        Assert.Equal("a-1", named.Id.Text);
        Assert.Null(named.Params);
    }

    [Fact]
    public void S9963_a_message_without_an_id_is_a_notification_and_a_result_is_a_client_reply()
    {
        Assert.IsType<JsonRpcNotification>(Parse("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""));
        Assert.IsType<JsonRpcClientReply>(Parse("""{"jsonrpc":"2.0","id":3,"result":{}}"""));
    }

    [Theory]
    [InlineData("not json", JsonRpcCodes.ParseError)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"", JsonRpcCodes.ParseError)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\",}", JsonRpcCodes.ParseError)]
    [InlineData("[{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}]", JsonRpcCodes.InvalidRequest)]
    [InlineData("42", JsonRpcCodes.InvalidRequest)]
    [InlineData("{\"id\":1,\"method\":\"ping\"}", JsonRpcCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"1.0\",\"id\":1,\"method\":\"ping\"}", JsonRpcCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":null,\"method\":\"ping\"}", JsonRpcCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1.5,\"method\":\"ping\"}", JsonRpcCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":{},\"method\":\"ping\"}", JsonRpcCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":5}", JsonRpcCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"\"}", JsonRpcCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\",\"params\":\"x\"}", JsonRpcCodes.InvalidRequest)]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1}", JsonRpcCodes.InvalidRequest)]
    public void S9963_malformed_messages_are_refused_with_the_code_json_rpc_gives(string json, int code)
        => Assert.Equal(code, Assert.IsType<JsonRpcRejected>(Parse(json)).Code);

    [Fact]
    public void S9963_a_refusal_keeps_the_id_it_could_read()
    {
        var rejected = Assert.IsType<JsonRpcRejected>(Parse("""{"jsonrpc":"1.0","id":"keep","method":"ping"}"""));
        Assert.Equal("keep", rejected.Id!.Value.Text);

        var answer = JsonDocument.Parse(JsonRpc.Error(rejected.Id, rejected.Code, rejected.Message)).RootElement;
        Assert.Equal("keep", answer.GetProperty("id").GetString());
        Assert.Equal(JsonRpcCodes.InvalidRequest, answer.GetProperty("error").GetProperty("code").GetInt32());

        var unknown = JsonDocument.Parse(JsonRpc.Error(null, JsonRpcCodes.ParseError, "x")).RootElement;
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("id").ValueKind);
    }

    [Fact]
    public void S9964_a_body_too_large_too_deep_or_with_a_long_method_or_id_is_refused()
    {
        var big = new byte[JsonRpc.MaxBodyBytes + 1];
        Assert.Equal(JsonRpcCodes.ParseError, Assert.IsType<JsonRpcRejected>(JsonRpc.Parse(big)).Code);

        var deep = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\",\"params\":" + new string('[', 20) + new string(']', 20) + "}";
        Assert.Equal(JsonRpcCodes.ParseError, Assert.IsType<JsonRpcRejected>(Parse(deep)).Code);

        var method = $$"""{"jsonrpc":"2.0","id":1,"method":"{{new string('m', JsonRpc.MaxMethodLength + 1)}}"}""";
        Assert.Equal(JsonRpcCodes.InvalidRequest, Assert.IsType<JsonRpcRejected>(Parse(method)).Code);

        var id = $$"""{"jsonrpc":"2.0","id":"{{new string('i', JsonRpc.MaxIdLength + 1)}}","method":"ping"}""";
        Assert.Equal(JsonRpcCodes.InvalidRequest, Assert.IsType<JsonRpcRejected>(Parse(id)).Code);
    }

    [Fact]
    public void S9964_invalid_utf8_is_a_parse_error()
        => Assert.Equal(JsonRpcCodes.ParseError, Assert.IsType<JsonRpcRejected>(JsonRpc.Parse([0x7B, 0xFF, 0xFE, 0x7D])).Code);

    // ---- [S-9965] [S-9966] HTTP ------------------------------------------------------------------

    private const int Port = 48213;

    private static string Head(string method = "POST", string target = "/mcp", string? host = "127.0.0.1:48213", string? auth = "Bearer " + Token,
        string? type = "application/json", string? length = "20", params string[] extra)
    {
        var text = new StringBuilder($"{method} {target} HTTP/1.1");
        void Add(string name, string? value)
        {
            if (value is not null)
            {
                text.Append("\r\n").Append(name).Append(": ").Append(value);
            }
        }

        Add("Host", host);
        Add("Authorization", auth);
        Add("Content-Type", type);
        Add("Content-Length", length);
        foreach (var line in extra)
        {
            text.Append("\r\n").Append(line);
        }

        return text.ToString();
    }

    private static McpHttpVerdict Check(string head)
    {
        var parsed = McpHttp.ParseHead(Encoding.ASCII.GetBytes(head), out var problem);
        Assert.True(parsed is not null, problem);
        return McpHttp.Check(parsed!, Port, Token);
    }

    [Fact]
    public void S9966_a_well_formed_post_with_the_token_proceeds_with_its_length()
    {
        var verdict = Check(Head(extra: ["Accept: application/json, text/event-stream", "MCP-Protocol-Version: 2025-06-18", "Origin: http://localhost:48213"]));
        Assert.True(verdict.Proceeds);
        Assert.Equal(20, verdict.BodyLength);

        Assert.True(Check(Head(host: "localhost:48213", type: "application/json; charset=utf-8")).Proceeds);
    }

    [Theory]
    [InlineData("other.example:48213", 403)]
    [InlineData("127.0.0.1:1", 403)]
    [InlineData("127.0.0.1", 403)]
    [InlineData("192.168.1.2:48213", 403)]
    public void S9966_a_host_that_is_not_this_pc_at_the_port_is_forbidden(string host, int status)
        => Assert.Equal(status, Check(Head(host: host)).Status);

    [Fact]
    public void S9966_a_missing_host_or_a_foreign_origin_is_forbidden_before_the_token_is_looked_at()
    {
        Assert.Equal(403, Check(Head(host: null)).Status);
        Assert.Equal(403, Check(Head(auth: null, extra: ["Origin: https://evil.example"])).Status);
        Assert.Equal(403, Check(Head(extra: ["Origin: http://127.0.0.1:9"])).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer wrong")]
    [InlineData("Basic " + Token)]
    [InlineData("Bearer " + Token + "x")]
    [InlineData("Bearer")]
    public void S9966_a_missing_or_wrong_token_is_unauthorised(string? auth)
        => Assert.Equal(401, Check(Head(auth: auth)).Status);

    [Fact]
    public void S9966_another_path_method_or_body_is_refused_with_its_status()
    {
        Assert.Equal(404, Check(Head(target: "/")).Status);
        Assert.Equal(404, Check(Head(target: "/mcp?x=1")).Status);
        Assert.Equal(405, Check(Head(method: "GET")).Status);
        Assert.Equal(405, Check(Head(method: "DELETE")).Status);
        Assert.Equal(400, Check(Head(extra: ["MCP-Protocol-Version: 1999-01-01"])).Status);
        Assert.Equal(400, Check(Head(extra: ["Transfer-Encoding: chunked"])).Status);
        Assert.Equal(411, Check(Head(length: null)).Status);
        Assert.Equal(400, Check(Head(length: "-1")).Status);
        Assert.Equal(400, Check(Head(length: "0")).Status);
        Assert.Equal(400, Check(Head(length: "12x")).Status);
        Assert.Equal(413, Check(Head(length: (JsonRpc.MaxBodyBytes + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))).Status);
        Assert.Equal(415, Check(Head(type: "text/plain")).Status);
        Assert.Equal(415, Check(Head(type: null)).Status);
        Assert.Equal(406, Check(Head(extra: ["Accept: text/html"])).Status);
    }

    [Theory]
    [InlineData("POST /mcp HTTP/1.0\r\nHost: 127.0.0.1:48213")]
    [InlineData("POST /mcp\r\nHost: 127.0.0.1:48213")]
    [InlineData("POST  /mcp HTTP/1.1\r\nHost: 127.0.0.1:48213")]
    [InlineData("POST /mcp HTTP/1.1\r\nHost 127.0.0.1:48213")]
    [InlineData("POST /mcp HTTP/1.1\r\n folded: x")]
    [InlineData("POST /mcp HTTP/1.1\r\nHost: 127.0.0.1:48213\r\nHost: evil")]
    [InlineData("POST /mcp HTTP/1.1\r\nContent-Length: 5\r\ncontent-length: 6")]
    [InlineData("POST /mcp HTTP/1.1\r\nX: café")]
    public void S9965_a_malformed_head_is_refused(string head)
        => Assert.Null(McpHttp.ParseHead(Encoding.Latin1.GetBytes(head), out _));

    [Fact]
    public void S9965_a_head_over_its_limits_is_refused()
    {
        var many = "POST /mcp HTTP/1.1" + string.Concat(Enumerable.Range(0, McpHttp.MaxHeaders + 1).Select(i => $"\r\nX-{i}: y"));
        Assert.Null(McpHttp.ParseHead(Encoding.ASCII.GetBytes(many), out _));

        var big = "POST /mcp HTTP/1.1\r\nX: " + new string('y', McpHttp.MaxHeadBytes);
        Assert.Null(McpHttp.ParseHead(Encoding.ASCII.GetBytes(big), out _));
    }

    [Fact]
    public void S9965_an_answer_closes_the_connection_is_never_cached_and_says_its_length()
    {
        var text = Encoding.ASCII.GetString(McpHttp.Response(200, "{}"u8, "application/json"));
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", text, StringComparison.Ordinal);
        Assert.Contains("Content-Length: 2\r\n", text, StringComparison.Ordinal);
        Assert.Contains("Connection: close\r\n", text, StringComparison.Ordinal);
        Assert.Contains("Cache-Control: no-store\r\n", text, StringComparison.Ordinal);
        Assert.EndsWith("\r\n\r\n{}", text, StringComparison.Ordinal);

        Assert.Contains("WWW-Authenticate: Bearer", Encoding.ASCII.GetString(McpHttp.Refusal(new McpHttpVerdict(401, "no", 0))), StringComparison.Ordinal);
        Assert.Contains("Allow: POST", Encoding.ASCII.GetString(McpHttp.Refusal(new McpHttpVerdict(405, "no", 0))), StringComparison.Ordinal);
    }

    // ---- [S-9962] [S-9967] [S-9968] the protocol and the tools -------------------------------------

    private sealed class FakeData : IMcpData
    {
        public McpDesk Desk { get; set; } = new(
            [new McpDisplay(1, "Dell U2720Q", 3840, 2160, Primary: true, Enabled: true, "home")],
            [
                new McpView("home", "Home", Active: true, [new McpWidget("w1", "clock", "Clock", 0.1, 0.2, 0.25, 0.123456)]),
                new McpView("work", "Work", Active: false, []),
            ]);

        public List<NoteMeta> Notes { get; } = [new("n1", "Groceries", 1_700_000_000_000)];

        public string NoteText { get; set; } = "- milk";

        public List<(DateTimeOffset From, DateTimeOffset To)> CalendarAsked { get; } = [];

        public Task<McpDesk> DeskAsync(CancellationToken cancellation) => Task.FromResult(Desk);

        public Task<IReadOnlyList<NoteMeta>> NotesAsync(CancellationToken cancellation) => Task.FromResult<IReadOnlyList<NoteMeta>>(Notes);

        public Task<McpNote?> NoteAsync(string id, CancellationToken cancellation)
            => Task.FromResult(Notes.Find(n => n.Id == id) is { } meta ? new McpNote(meta, NoteReadStatus.Found, NoteText) : null);

        public Task<IReadOnlyList<McpTaskList>> TasksAsync(CancellationToken cancellation)
            => Task.FromResult<IReadOnlyList<McpTaskList>>([new McpTaskList("This PC", "Chores", [new McpTask("Bins", false, null), new McpTask("Dishes", true, "2026-11-08")], null)]);

        public Task<IReadOnlyList<HabitList>> HabitsAsync(CancellationToken cancellation)
            => Task.FromResult<IReadOnlyList<HabitList>>([new HabitList("h", "Health", [new Habit("x", "Run", ["2026-11-01", "2026-11-05", "2026-11-03"])])]);

        public Task<IReadOnlyList<McpCalendar>> CalendarAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellation)
        {
            CalendarAsked.Add((from, to));
            IReadOnlyList<CalendarEvent> events =
            [
                new CalendarEvent { Id = "b", Subject = "Standup", Start = from.AddHours(33), End = from.AddHours(33.5), Location = "Room 1", JoinUrl = "https://meet.example/secret" },
                CalendarEvent.AllDayEvent("a", "Holiday", DateOnly.FromDateTime(from.Date), DateOnly.FromDateTime(from.Date).AddDays(1)),
            ];
            return Task.FromResult<IReadOnlyList<McpCalendar>>([new McpCalendar("Work", events, null, from)]);
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 7, 15, 30, 0, TimeSpan.FromHours(1));

    private static McpProtocol Protocol(IMcpData? data = null) => new(new McpTools(data ?? new FakeData(), () => Now), "1.0.0-test");

    private static async Task<JsonElement> Answer(McpProtocol protocol, string json)
    {
        var body = await protocol.HandleAsync(Parse(json), CancellationToken.None);
        Assert.NotNull(body);
        return JsonDocument.Parse(body!).RootElement.Clone();
    }

    private static async Task<(string Text, bool IsError)> Tool(string name, string arguments = "{}", IMcpData? data = null)
    {
        var answer = await Answer(Protocol(data), "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"" + name + "\",\"arguments\":" + arguments + "}}");
        var result = answer.GetProperty("result");
        return (result.GetProperty("content")[0].GetProperty("text").GetString()!, result.GetProperty("isError").GetBoolean());
    }

    [Theory]
    [InlineData("2025-06-18", "2025-06-18")]
    [InlineData("2025-03-26", "2025-03-26")]
    [InlineData("2024-11-05", "2025-06-18")]
    [InlineData("2099-01-01", "2025-06-18")]
    public async Task S9962_initialize_agrees_the_clients_version_when_spoken_else_the_latest(string asked, string agreed)
    {
        var result = (await Answer(Protocol(), "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"" + asked + "\",\"capabilities\":{},\"clientInfo\":{\"name\":\"t\",\"version\":\"1\"}}}")).GetProperty("result");
        Assert.Equal(agreed, result.GetProperty("protocolVersion").GetString());
        Assert.Equal("themia", result.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.True(result.GetProperty("capabilities").TryGetProperty("tools", out _));
        Assert.False(result.GetProperty("capabilities").TryGetProperty("resources", out _));
    }

    [Fact]
    public async Task S9962_initialize_without_a_version_ping_and_unknown_methods()
    {
        Assert.Equal(JsonRpcCodes.InvalidParams, (await Answer(Protocol(), """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""")).GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Object, (await Answer(Protocol(), """{"jsonrpc":"2.0","id":2,"method":"ping"}""")).GetProperty("result").ValueKind);
        Assert.Equal(JsonRpcCodes.MethodNotFound, (await Answer(Protocol(), """{"jsonrpc":"2.0","id":3,"method":"resources/list"}""")).GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task S9962_notifications_and_client_replies_get_no_answer()
    {
        Assert.Null(await Protocol().HandleAsync(Parse("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""), CancellationToken.None));
        Assert.Null(await Protocol().HandleAsync(Parse("""{"jsonrpc":"2.0","id":9,"result":{}}"""), CancellationToken.None));
    }

    [Fact]
    public async Task S9967_tools_list_names_every_tool_read_only_with_an_object_schema()
    {
        var tools = (await Answer(Protocol(), """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""")).GetProperty("result").GetProperty("tools");
        var names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToArray();
        Assert.Equal(new string?[] { "list_displays", "list_views", "list_widgets", "list_notes", "read_note", "read_tasks", "read_habits", "read_calendar" }, names);
        foreach (var tool in tools.EnumerateArray())
        {
            Assert.Equal("object", tool.GetProperty("inputSchema").GetProperty("type").GetString());
            Assert.True(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
            Assert.False(tool.GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());
        }
    }

    [Fact]
    public async Task S9968_an_unknown_tool_or_arguments_outside_the_schema_are_invalid_params()
    {
        async Task<int> Code(string json) => (await Answer(Protocol(), json)).GetProperty("error").GetProperty("code").GetInt32();
        Assert.Equal(JsonRpcCodes.InvalidParams, await Code("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"delete_everything"}}"""));
        Assert.Equal(JsonRpcCodes.InvalidParams, await Code("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{}}"""));
        Assert.Equal(JsonRpcCodes.InvalidParams, await Code("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_views","arguments":{"x":1}}}"""));
        Assert.Equal(JsonRpcCodes.InvalidParams, await Code("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"read_note","arguments":{}}}"""));
        Assert.Equal(JsonRpcCodes.InvalidParams, await Code("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"read_note","arguments":{"note_id":5}}}"""));
        Assert.Equal(JsonRpcCodes.InvalidParams, await Code("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"read_calendar","arguments":{"days":0}}}"""));
        Assert.Equal(JsonRpcCodes.InvalidParams, await Code("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"read_calendar","arguments":{"days":32}}}"""));
        Assert.Equal(JsonRpcCodes.InvalidParams, await Code("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_views","arguments":[]}}"""));
    }

    [Fact]
    public async Task S9970_list_displays_numbers_them_and_names_the_view_each_shows()
    {
        var (text, error) = await Tool("list_displays");
        Assert.False(error);
        var display = JsonDocument.Parse(text).RootElement.GetProperty("displays")[0];
        Assert.Equal(1, display.GetProperty("number").GetInt32());
        Assert.Equal("Dell U2720Q", display.GetProperty("name").GetString());
        Assert.Equal(3840, display.GetProperty("width_px").GetInt32());
        Assert.Equal("home", display.GetProperty("view_id").GetString());
        Assert.False(display.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task S9971_list_views_and_list_widgets_with_and_without_a_view()
    {
        var views = JsonDocument.Parse((await Tool("list_views")).Text).RootElement.GetProperty("views");
        Assert.Equal(2, views.GetArrayLength());
        Assert.True(views[0].GetProperty("active").GetBoolean());
        Assert.Equal(1, views[0].GetProperty("widgets").GetInt32());
        Assert.Equal(1, views[0].GetProperty("shown_on_displays")[0].GetInt32());

        var all = JsonDocument.Parse((await Tool("list_widgets")).Text).RootElement.GetProperty("views");
        Assert.Equal(2, all.GetArrayLength());

        var home = JsonDocument.Parse((await Tool("list_widgets", """{"view_id":"home"}""")).Text).RootElement.GetProperty("views");
        var widget = Assert.Single(home.EnumerateArray()).GetProperty("widgets")[0];
        Assert.Equal("clock", widget.GetProperty("kind").GetString());
        Assert.Equal("Clock", widget.GetProperty("label").GetString());
        Assert.Equal(0.1235, widget.GetProperty("height").GetDouble());

        var (missing, error) = await Tool("list_widgets", """{"view_id":"nope"}""");
        Assert.True(error);
        Assert.Contains("list_views", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task S9973_notes_are_listed_and_read_and_a_long_body_is_cut_and_says_so()
    {
        var notes = JsonDocument.Parse((await Tool("list_notes")).Text).RootElement.GetProperty("notes");
        Assert.Equal("Groceries", notes[0].GetProperty("title").GetString());

        var note = JsonDocument.Parse((await Tool("read_note", """{"note_id":"n1"}""")).Text).RootElement;
        Assert.Equal("- milk", note.GetProperty("text").GetString());
        Assert.False(note.GetProperty("truncated").GetBoolean());

        var data = new FakeData { NoteText = new string('a', McpTools.MaxNoteChars + 10) };
        var cut = JsonDocument.Parse((await Tool("read_note", """{"note_id":"n1"}""", data)).Text).RootElement;
        Assert.Equal(McpTools.MaxNoteChars, cut.GetProperty("text").GetString()!.Length);
        Assert.True(cut.GetProperty("truncated").GetBoolean());

        Assert.True((await Tool("read_note", """{"note_id":"gone"}""")).IsError);
    }

    [Fact]
    public async Task S9974_S9975_tasks_and_habits_with_the_most_recent_periods_first()
    {
        var list = JsonDocument.Parse((await Tool("read_tasks")).Text).RootElement.GetProperty("lists")[0];
        Assert.Equal("Chores", list.GetProperty("name").GetString());
        Assert.Equal("2026-11-08", list.GetProperty("tasks")[1].GetProperty("due").GetString());
        Assert.False(list.GetProperty("tasks")[0].TryGetProperty("due", out _));

        var habit = JsonDocument.Parse((await Tool("read_habits")).Text).RootElement.GetProperty("lists")[0].GetProperty("habits")[0];
        Assert.Equal(new string?[] { "2026-11-05", "2026-11-03", "2026-11-01" }, habit.GetProperty("done_in").EnumerateArray().Select(p => p.GetString()).ToArray());
    }

    [Fact]
    public async Task S9976_the_calendar_is_read_from_local_midnight_for_the_days_asked_in_start_order_without_join_links()
    {
        var data = new FakeData();
        var (text, error) = await Tool("read_calendar", """{"days":3}""", data);
        Assert.False(error);

        var (from, to) = Assert.Single(data.CalendarAsked);
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.FromHours(1)), from);
        Assert.Equal(from.AddDays(3), to);

        var events = JsonDocument.Parse(text).RootElement.GetProperty("calendars")[0].GetProperty("events");
        Assert.Equal("Holiday", events[0].GetProperty("subject").GetString());
        Assert.Equal("2026-11-07", events[0].GetProperty("first_day").GetString());
        Assert.Equal("Standup", events[1].GetProperty("subject").GetString());
        Assert.Equal("2026-11-08T09:00:00+01:00", events[1].GetProperty("start").GetString());
        Assert.DoesNotContain("meet.example", text, StringComparison.Ordinal);

        _ = await Tool("read_calendar", "{}", data);
        Assert.Equal(from.AddDays(7), data.CalendarAsked[1].To);
    }

    [Fact]
    public async Task S9968_a_read_that_throws_is_a_tool_error_not_a_crash()
    {
        var (text, error) = await Tool("list_views", "{}", new ThrowingData());
        Assert.True(error);
        Assert.DoesNotContain("boom", text, StringComparison.Ordinal);
    }

    private sealed class ThrowingData : IMcpData
    {
        public Task<McpDesk> DeskAsync(CancellationToken cancellation) => throw new InvalidOperationException("boom");

        public Task<IReadOnlyList<NoteMeta>> NotesAsync(CancellationToken cancellation) => throw new InvalidOperationException("boom");

        public Task<McpNote?> NoteAsync(string id, CancellationToken cancellation) => throw new InvalidOperationException("boom");

        public Task<IReadOnlyList<McpTaskList>> TasksAsync(CancellationToken cancellation) => throw new InvalidOperationException("boom");

        public Task<IReadOnlyList<HabitList>> HabitsAsync(CancellationToken cancellation) => throw new InvalidOperationException("boom");

        public Task<IReadOnlyList<McpCalendar>> CalendarAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellation) => throw new InvalidOperationException("boom");
    }

    // ---- [S-9977] [S-9978] the token and the setups -----------------------------------------------

    [Fact]
    public void S9977_a_new_token_is_43_base64url_characters_and_never_the_same()
    {
        var one = McpAccess.NewToken();
        Assert.True(McpAccess.IsToken(one));
        Assert.NotEqual(one, McpAccess.NewToken());
        Assert.False(McpAccess.IsToken("short"));
        Assert.False(McpAccess.IsToken(new string('+', 43)));
        Assert.True(McpAccess.IsPort(48213));
        Assert.False(McpAccess.IsPort(80));
        Assert.False(McpAccess.IsPort(70000));
    }

    [Fact]
    public void S9977_the_address_shown_never_carries_its_token()
    {
        var address = new McpAddress(48213, Token);
        Assert.Equal("http://127.0.0.1:48213/mcp", address.Url);
        Assert.DoesNotContain(Token, address.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void S9978_the_setups_carry_the_url_and_the_token()
    {
        var address = new McpAddress(48213, Token);
        Assert.Equal(
            $"claude mcp add --transport http --scope user themia http://127.0.0.1:48213/mcp --header \"Authorization: Bearer {Token}\"",
            McpAccess.ClaudeCodeCommand(address));

        var server = JsonDocument.Parse(McpAccess.JsonConfig(address)).RootElement.GetProperty("mcpServers").GetProperty("themia");
        Assert.Equal("http", server.GetProperty("type").GetString());
        Assert.Equal("http://127.0.0.1:48213/mcp", server.GetProperty("url").GetString());
        Assert.Equal("Bearer " + Token, server.GetProperty("headers").GetProperty("Authorization").GetString());
    }

    // ---- [S-9961] a real round trip on loopback ----------------------------------------------------

    private static async Task<HttpResponseMessage> Post(HttpClient client, int port, string json, string? token = Token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/mcp")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        request.Headers.Accept.ParseAdd("application/json, text/event-stream");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task S9961_the_listener_serves_on_loopback_refuses_a_wrong_token_and_stops_at_once()
    {
        var listener = McpListener.Start(0, Token, Protocol());
        var port = listener.Port;
        using var client = new HttpClient();

        using (var ok = await Post(client, port, """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_views"}}"""))
        {
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var result = JsonDocument.Parse(await ok.Content.ReadAsStringAsync()).RootElement.GetProperty("result");
            Assert.Contains("Home", result.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
        }

        using (var accepted = await Post(client, port, """{"jsonrpc":"2.0","method":"notifications/initialized"}"""))
        {
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using (var refused = await Post(client, port, """{"jsonrpc":"2.0","id":1,"method":"ping"}""", token: "wrong"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        using (var garbage = await Post(client, port, "nope"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, garbage.StatusCode);
        }

        using (var get = await client.GetAsync($"http://127.0.0.1:{port}/mcp"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        }

        await listener.DisposeAsync();
        using var probe = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(() => probe.ConnectAsync(IPAddress.Loopback, port));
    }

    [Fact]
    public async Task S9961_it_listens_on_127_0_0_1_only_and_a_taken_port_throws()
    {
        await using var first = McpListener.Start(0, Token, Protocol());
        {
            Assert.Throws<SocketException>(() => McpListener.Start(first.Port, Token, Protocol()));

            // Not on another address of this PC: nothing answers on IPv6 loopback.
            using var v6 = new TcpClient(AddressFamily.InterNetworkV6);
            await Assert.ThrowsAnyAsync<SocketException>(() => v6.ConnectAsync(IPAddress.IPv6Loopback, first.Port));
        }
    }

    [Fact]
    public async Task S9965_a_head_without_its_end_is_answered_431_and_a_stalled_client_does_not_hold_the_server()
    {
        await using var listener = McpListener.Start(0, Token, Protocol());
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, listener.Port);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("POST /mcp HTTP/1.1\r\nX: " + new string('y', McpHttp.MaxHeadBytes + 10)));
        var answer = new byte[64];
        var read = await stream.ReadAsync(answer);
        Assert.StartsWith("HTTP/1.1 431", Encoding.ASCII.GetString(answer, 0, read), StringComparison.Ordinal);
    }
}
