using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Dataisland.Serilog.RequestLogging;

public class SerilogMiddleware(RequestDelegate next, BodyCaptureConfig bodyCapture)
{
    private const int AbsoluteMaxCaptureBytes = 64 * 1024;
    private static readonly string[] AlwaysRedactedFields =
    [
        "access_token",
        "api_key",
        "authorization",
        "client_secret",
        "cookie",
        "credential",
        "id_token",
        "password",
        "refresh_token",
        "secret",
        "token"
    ];

    public SerilogMiddleware(RequestDelegate next)
        : this(next, new BodyCaptureConfig())
    {
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!bodyCapture.Enabled)
        {
            await next(ctx);
            return;
        }

        var originalRequestBody = ctx.Request.Body;
        var requestLimit = NormalizeLimit(bodyCapture.MaxRequestBytes);
        BoundedReadCaptureStream? requestCapture = null;
        if (requestLimit > 0 &&
            bodyCapture.AllowedRequestFields.Count > 0 &&
            IsJson(ctx.Request.ContentType))
        {
            requestCapture = new BoundedReadCaptureStream(originalRequestBody, requestLimit);
            ctx.Request.Body = requestCapture;
        }

        var responseLimit = NormalizeLimit(bodyCapture.MaxResponseBytes);
        var originalResponseBody = ctx.Response.Body;
        BoundedWriteCaptureStream? responseCapture = null;
        if (responseLimit > 0 && bodyCapture.AllowedResponseFields.Count > 0)
        {
            responseCapture = new BoundedWriteCaptureStream(
                originalResponseBody,
                responseLimit,
                () => IsJson(ctx.Response.ContentType));
            ctx.Response.Body = responseCapture;
        }

        try
        {
            await next(ctx);
        }
        finally
        {
            ctx.Request.Body = originalRequestBody;
            ctx.Response.Body = originalResponseBody;

            if (requestCapture is not null)
            {
                AddCapturedJson(
                    ctx,
                    "RequestBody",
                    "RequestBodyCaptureTruncated",
                    requestCapture.CapturedBytes.Span,
                    RequestCaptureIsIncomplete(ctx.Request.ContentLength, requestCapture),
                    bodyCapture.AllowedRequestFields);
            }

            if (responseCapture is not null && IsJson(ctx.Response.ContentType))
            {
                AddCapturedJson(
                    ctx,
                    "ResponseBody",
                    "ResponseBodyCaptureTruncated",
                    responseCapture.CapturedBytes.Span,
                    responseCapture.Truncated,
                    bodyCapture.AllowedResponseFields);
            }
        }
    }

    private void AddCapturedJson(
        HttpContext ctx,
        string bodyItemName,
        string truncatedItemName,
        ReadOnlySpan<byte> bytes,
        bool truncated,
        IReadOnlyCollection<string> allowedFields)
    {
        if (truncated)
        {
            ctx.Items[truncatedItemName] = true;
            return;
        }

        if (bytes.IsEmpty)
            return;

        try
        {
            using var document = JsonDocument.Parse(bytes.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;

            var allowed = new HashSet<string>(allowedFields, StringComparer.OrdinalIgnoreCase);
            var redacted = new HashSet<string>(bodyCapture.RedactedFields, StringComparer.OrdinalIgnoreCase);
            redacted.UnionWith(AlwaysRedactedFields);
            var safeFields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!allowed.Contains(property.Name))
                    continue;

                if (redacted.Contains(property.Name))
                {
                    safeFields[property.Name] = "[REDACTED]";
                    continue;
                }

                if (property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null)
                    safeFields[property.Name] = property.Value.GetString();
            }

            if (safeFields.Count > 0)
                ctx.Items[bodyItemName] = JsonSerializer.Serialize(safeFields);
        }
        catch (JsonException)
        {
            // Invalid or incomplete JSON is intentionally omitted from logs.
        }
    }

    private static int NormalizeLimit(int configuredLimit) =>
        Math.Clamp(configuredLimit, 0, AbsoluteMaxCaptureBytes);

    private static bool RequestCaptureIsIncomplete(
        long? contentLength,
        BoundedReadCaptureStream capture)
    {
        if (capture.Truncated)
            return true;

        if (contentLength is { } length)
            return length > capture.CapturedBytes.Length;

        return !capture.ReachedEnd;
    }

    private static bool IsJson(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
               mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class BoundedReadCaptureStream(Stream inner, int captureLimit) : Stream
    {
        private readonly MemoryStream capture = new(captureLimit);

        public ReadOnlyMemory<byte> CapturedBytes => capture.GetBuffer().AsMemory(0, checked((int)capture.Length));
        public bool ReachedEnd { get; private set; }
        public bool Truncated { get; private set; }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            CaptureRead(buffer.AsSpan(offset, read));
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            CaptureRead(buffer.AsSpan(offset, read));
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            CaptureRead(buffer.Span[..read]);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            inner.Seek(offset, origin);

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        private void CaptureRead(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty)
            {
                ReachedEnd = true;
                return;
            }

            var remaining = captureLimit - checked((int)capture.Length);
            if (remaining <= 0)
            {
                Truncated = true;
                return;
            }

            var count = Math.Min(remaining, bytes.Length);
            capture.Write(bytes[..count]);
            Truncated |= count < bytes.Length;
        }
    }

    private sealed class BoundedWriteCaptureStream(
        Stream inner,
        int captureLimit,
        Func<bool> shouldCapture) : Stream
    {
        private readonly MemoryStream capture = new(captureLimit);

        public ReadOnlyMemory<byte> CapturedBytes => capture.GetBuffer().AsMemory(0, checked((int)capture.Length));
        public bool Truncated { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            inner.FlushAsync(cancellationToken);

        public override void Write(byte[] buffer, int offset, int count)
        {
            Capture(buffer.AsSpan(offset, count));
            inner.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            Capture(buffer.AsSpan(offset, count));
            await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Capture(buffer.Span);
            return inner.WriteAsync(buffer, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        private void Capture(ReadOnlySpan<byte> bytes)
        {
            if (!shouldCapture() || bytes.IsEmpty)
                return;

            var remaining = captureLimit - checked((int)capture.Length);
            if (remaining <= 0)
            {
                Truncated = true;
                return;
            }

            var count = Math.Min(remaining, bytes.Length);
            capture.Write(bytes[..count]);
            Truncated |= count < bytes.Length;
        }
    }
}
