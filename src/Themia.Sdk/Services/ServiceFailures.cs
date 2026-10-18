using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Accounts;
using Themia.Core.Widgets;

namespace Themia.Sdk.Services;

/// <summary>
/// [S-9921] What a reply means, in the one failure vocabulary ([S-9900]).
/// </summary>
/// <param name="Status"><see cref="FetchStatus.Ok"/> for a usable answer (a 304 included), else what went wrong.</param>
/// <param name="RetryAt">When the reply's bucket may be asked again: a throttle's wait, or the reset of a budget the success spent.</param>
/// <param name="Bucket">The bucket the reply counted against, when the service named one.</param>
/// <param name="Detail">One line for the log ("HTTP 404", the service's message, an unknown code); never rendered as is.</param>
/// <param name="UnknownCode">A code the scheme does not know: read as unexpected and logged once, so a new code is seen.</param>
public sealed record ServiceVerdict(FetchStatus Status, DateTimeOffset? RetryAt = null, string? Bucket = null, string? Detail = null, string? UnknownCode = null)
{
    public bool Ok => Status == FetchStatus.Ok;
}

/// <summary>
/// [S-9921] The one mapping from a service's reply to the failure vocabulary, read through its
/// descriptor's schemes. Order: a throttle the headers show (the rate-limit scheme), then a throttle
/// the body shows (codes, messages, a body wait), then the body's code, then the status (401 sign in
/// again, 403 not allowed, 404 and 410 not found, anything else unexpected). A GraphQL 200 with
/// errors and nothing at the list's path is classified by its codes like any failure; with the list
/// present it is used, the errors carried as the detail. Nothing downstream knows which service it was.
/// </summary>
public static class ServiceFailures
{
    /// <param name="anonymous">The request carried no credential: a 401 is a refusal, there is no sign-in to mend.</param>
    /// <param name="graphQL">For a GraphQL reply that parsed: its envelope.</param>
    public static ServiceVerdict Classify(ServiceDescriptor descriptor, ServiceReply reply, DateTimeOffset now, bool anonymous = false, GraphQLAnswer? graphQL = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(reply);

        var state = RateLimitRules.Read(descriptor.Limits, reply.Headers, now, reply.Status);

        if (reply.IsSuccess || reply.IsNotModified)
        {
            var spent = RateLimitRules.WaitUntil(descriptor.Limits, reply.Status, state, now);

            if (graphQL is { HasList: false, Errors.Count: > 0 } failed)
            {
                return FromCodes(descriptor, failed.Errors.Select(e => e.Code), failed.Errors.Select(e => e.Message), state, null, now, reply.Status)
                    ?? new ServiceVerdict(FetchStatus.BadResponse, spent, state.Bucket, Join(failed.Errors));
            }

            return new ServiceVerdict(FetchStatus.Ok, spent, state.Bucket, graphQL is { Errors.Count: > 0 } partial ? Join(partial.Errors) : null);
        }

        var body = ErrorBodies.Read(descriptor.Errors, reply.Body.Span);

        if (RateLimitRules.IsThrottle(descriptor.Limits, reply.Status, state))
        {
            return new ServiceVerdict(FetchStatus.RateLimited, RateLimitRules.ThrottledUntil(state, body.Wait, now), state.Bucket, Http(reply.Status));
        }

        if (FromCodes(descriptor, body.Codes, body.Messages, state, body.Wait, now, reply.Status) is { } coded)
        {
            return coded;
        }

        var detail = body.Messages.FirstOrDefault(m => m is { Length: > 0 }) is { } said ? $"{Http(reply.Status)}: {said}" : Http(reply.Status);
        var unknown = body.Codes.FirstOrDefault(c => c is { Length: > 0 });

        var status = reply.Status switch
        {
            401 => anonymous ? FetchStatus.Forbidden : FetchStatus.SignInRequired,
            403 => FetchStatus.Forbidden,
            404 or 410 => FetchStatus.NotFound,
            _ => FetchStatus.BadResponse,
        };

        // A code the scheme does not know only matters when the status alone says nothing better.
        return new ServiceVerdict(status, null, state.Bucket, detail, status == FetchStatus.BadResponse ? unknown : null);
    }

    private static ServiceVerdict? FromCodes(
        ServiceDescriptor descriptor, IEnumerable<string?> codes, IEnumerable<string?> messages, RateLimitState state, TimeSpan? bodyWait, DateTimeOffset now, int httpStatus)
    {
        var scheme = descriptor.Errors;
        FetchStatus? found = null;
        string? matched = null;

        foreach (var code in codes)
        {
            if (code is { Length: > 0 } && scheme.Codes.TryGetValue(code, out var mapped))
            {
                // A throttle among the codes wins: it carries the one wait that matters.
                if (found is null || mapped == FetchStatus.RateLimited)
                {
                    found = mapped;
                    matched = code;
                }
            }
        }

        if (found != FetchStatus.RateLimited)
        {
            foreach (var message in messages)
            {
                if (message is not { Length: > 0 })
                {
                    continue;
                }

                foreach (var rule in scheme.Messages)
                {
                    if (message.Contains(rule.Contains, StringComparison.OrdinalIgnoreCase) && (found is null || rule.Status == FetchStatus.RateLimited))
                    {
                        found = rule.Status;
                        matched = rule.Contains;
                    }
                }
            }
        }

        if (found is not { } status)
        {
            return bodyWait is { } wait && httpStatus == 429
                ? new ServiceVerdict(FetchStatus.RateLimited, RateLimitRules.ThrottledUntil(state, wait, now), state.Bucket, Http(httpStatus))
                : null;
        }

        var retryAt = status == FetchStatus.RateLimited ? RateLimitRules.ThrottledUntil(state, bodyWait, now) : (DateTimeOffset?)null;
        return new ServiceVerdict(status, retryAt, state.Bucket, $"{Http(httpStatus)}: {matched}");
    }

    private static string Http(int status) => "HTTP " + status.ToString(CultureInfo.InvariantCulture);

    private static string Join(IReadOnlyList<GraphQLError> errors)
        => string.Join("; ", errors.Take(3).Select(e => e.Code is { Length: > 0 } code ? $"{code}: {e.Message}" : e.Message ?? "error"));
}

/// <summary>What an error body said, per the service's scheme.</summary>
internal sealed record ErrorBodyRead(IReadOnlyList<string?> Codes, IReadOnlyList<string?> Messages, TimeSpan? Wait);

/// <summary>[S-9921] Reading an error body: the codes where the scheme says they are, the messages, a body wait.</summary>
internal static class ErrorBodies
{
    public static ErrorBodyRead Read(ErrorScheme scheme, ReadOnlySpan<byte> body)
    {
        if (body.IsEmpty)
        {
            return new ErrorBodyRead([], [], null);
        }

        try
        {
            using var document = JsonDocument.Parse(body.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new ErrorBodyRead([], [], null);
            }

            var codes = new List<string?>();
            var messages = new List<string?>();

            if (scheme.Body == ErrorBodyKind.GraphQLErrors && root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in GraphQLEnvelope.Errors(errors))
                {
                    codes.Add(error.Code);
                    messages.Add(error.Message);
                }
            }
            else if (scheme.Body == ErrorBodyKind.JsonCode && scheme.CodeField is { } field && Path(root, field) is { ValueKind: JsonValueKind.String } code)
            {
                codes.Add(code.GetString());
            }

            messages.Add(Text(root, "message") ?? (root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object ? Text(e, "message") : Text(root, "error")));

            TimeSpan? wait = null;
            if (scheme.RetryAfterField is { } waitField && Path(root, waitField) is { } waitValue)
            {
                var seconds = waitValue.ValueKind switch
                {
                    JsonValueKind.Number when waitValue.TryGetDouble(out var n) => n,
                    JsonValueKind.String when double.TryParse(waitValue.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var t) => t,
                    _ => double.NaN,
                };

                if (seconds is >= 0 and <= 86_400)
                {
                    wait = TimeSpan.FromSeconds(seconds);
                }
            }

            return new ErrorBodyRead(codes, messages, wait);
        }
        catch (JsonException)
        {
            // ignore: a body that is not JSON carries no code; the status says what it was.
            return new ErrorBodyRead([], [], null);
        }
    }

    /// <summary>A dotted path's value (<c>error_extra.retry_after</c>), or null.</summary>
    internal static JsonElement? Path(JsonElement root, string dotted)
    {
        if (dotted.Length == 0)
        {
            return root;
        }

        var at = root;
        foreach (var part in dotted.Split('.'))
        {
            if (at.ValueKind != JsonValueKind.Object || !at.TryGetProperty(part, out var next))
            {
                return null;
            }

            at = next;
        }

        return at;
    }

    private static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var line = value.GetString()?.Split('\n')[0].Trim();
        return string.IsNullOrEmpty(line) ? null : line.Length <= 300 ? line : line[..300];
    }
}

/// <summary>One GraphQL error: its message, its <c>extensions.code</c>, its path.</summary>
public sealed record GraphQLError(string? Message, string? Code, string? Path);

/// <summary>[S-9922] A GraphQL answer's envelope: the <c>data</c> member, the errors, and whether the list's own path is present.</summary>
public sealed record GraphQLAnswer(ReadOnlyMemory<byte> Data, IReadOnlyList<GraphQLError> Errors, bool HasList);

/// <summary>
/// [S-9922] The GraphQL transport's envelope: a request is <c>{"query": …, "variables": …}</c> written
/// through the service's own context; an answer's <c>data</c> goes to the rules, its <c>errors</c> to
/// the error scheme. A 200 with both is used when the list's own path is present.
/// </summary>
public static class GraphQLEnvelope
{
    public static ServiceBody Request<TVariables>(string query, TVariables variables, JsonTypeInfo<TVariables> contract)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(contract);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("query", query);
            writer.WritePropertyName("variables");
            JsonSerializer.Serialize(writer, variables, contract);
            writer.WriteEndObject();
        }

        return new ServiceBody(buffer.WrittenMemory.ToArray(), "application/json");
    }

    /// <summary>The envelope of an answer, or null when it is not one (no object, neither data nor errors).</summary>
    /// <param name="listPath">The dotted path under <c>data</c> the widget's list lives at (<c>viewer.assignedIssues</c>); empty for <c>data</c> itself.</param>
    public static GraphQLAnswer? Read(ReadOnlySpan<byte> body, string listPath)
    {
        ArgumentNullException.ThrowIfNull(listPath);

        if (body.IsEmpty)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var errors = root.TryGetProperty("errors", out var list) && list.ValueKind == JsonValueKind.Array ? Errors(list) : [];
            var hasData = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object;

            if (!hasData && errors.Count == 0)
            {
                return null;
            }

            var hasList = hasData && ErrorBodies.Path(data, listPath) is { } at && at.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
            var raw = hasData ? Encoding.UTF8.GetBytes(data.GetRawText()) : [];
            return new GraphQLAnswer(raw, errors, hasList);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static IReadOnlyList<GraphQLError> Errors(JsonElement array)
    {
        var errors = new List<GraphQLError>();
        foreach (var error in array.EnumerateArray().Take(20))
        {
            if (error.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            var code = error.TryGetProperty("extensions", out var x) && x.ValueKind == JsonValueKind.Object
                && x.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            var path = error.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.Array
                ? string.Join('.', p.EnumerateArray().Select(s => s.ValueKind == JsonValueKind.String ? s.GetString() : s.ToString()))
                : null;
            errors.Add(new GraphQLError(message, code, path));
        }

        return errors;
    }
}

/// <summary>[S-9923] The form-post transport's body: <c>application/x-www-form-urlencoded</c> fields, in order.</summary>
public static class FormBody
{
    public static ServiceBody Encode(IEnumerable<KeyValuePair<string, string>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var text = string.Join('&', fields.Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}"));
        return new ServiceBody(Encoding.UTF8.GetBytes(text), "application/x-www-form-urlencoded");
    }
}
