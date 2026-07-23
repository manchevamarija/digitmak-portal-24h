using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;

namespace DigitMak.Portal.Api.Application;

public interface IFileStorage
{
    Task<FileObject> SaveAsync(
        IFormFile file,
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken ct
    );
    Task<(Stream Stream, FileObject File)?> OpenAsync(Guid id, CancellationToken ct);
    Task<FileObject?> DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class ClamAvFileScanner(IConfiguration config, ILogger<ClamAvFileScanner> logger)
    : IFileScanner
{
    public async Task EnsureSafeAsync(Stream content, string filename, CancellationToken ct)
    {
        var host = config["CLAMAV_HOST"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogDebug(
                "ClamAV is not configured; skipping scan for {Filename} in development.",
                filename
            );
            return;
        }
        var port = int.TryParse(config["CLAMAV_PORT"], out var configuredPort) ? configuredPort : 3310;
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        await using var network = client.GetStream();
        await network.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), ct);
        var buffer = new byte[8192];
        var length = new byte[4];
        int read;
        while ((read = await content.ReadAsync(buffer, ct)) > 0)
        {
            BinaryPrimitives.WriteInt32BigEndian(length, read);
            await network.WriteAsync(length, ct);
            await network.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        Array.Clear(length);
        await network.WriteAsync(length, ct);
        await network.FlushAsync(ct);
        var responseBuffer = new byte[1024];
        var responseLength = await network.ReadAsync(responseBuffer, ct);
        var response = Encoding.UTF8.GetString(responseBuffer, 0, responseLength);
        if (!response.Contains("OK", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                response.Contains("FOUND", StringComparison.OrdinalIgnoreCase)
                    ? "The uploaded file was rejected because malware was detected."
                    : "The uploaded file could not be verified by the antivirus service."
            );
        if (content.CanSeek)
            content.Position = 0;
    }
}

public class DiskFileStorage(IConfiguration config, PortalDbContext db, IFileScanner scanner)
    : IFileStorage
{
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = [".pdf"],
        ["image/png"] = [".png"],
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["text/plain"] = [".txt"],
        ["text/csv"] = [".csv"],
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"],
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [".xlsx"],
    };

    public async Task<FileObject> SaveAsync(
        IFormFile file,
        string entityType,
        Guid entityId,
        Guid userId,
        CancellationToken ct
    )
    {
        if (file.Length <= 0 || file.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("File must be between 1 byte and 10 MB.");
        var contentType = file.ContentType.Split(';', 2)[0].Trim();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (
            !Allowed.TryGetValue(contentType, out var extensions)
            || !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
        )
            throw new InvalidOperationException("Unsupported file type or filename extension.");
        var id = Guid.NewGuid();
        var root = config["UPLOADS_ROOT"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        var relative = Path.Combine(
            config["ASPNETCORE_ENVIRONMENT"] ?? "Development",
            entityType,
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            id.ToString("N")
        );
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var input = file.OpenReadStream();
        await EnsureSignatureAsync(input, contentType, ct);
        await scanner.EnsureSafeAsync(input, file.FileName, ct);
        if (input.CanSeek)
            input.Position = 0;
        await using var output = File.Create(path);
        using var sha = SHA256.Create();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            sha.TransformBlock(buffer, 0, read, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        var obj = new FileObject
        {
            Id = id,
            OriginalFilename = Path.GetFileName(file.FileName),
            StoredPath = relative,
            ContentType = contentType,
            SizeBytes = file.Length,
            Checksum = Convert.ToHexString(sha.Hash!),
            UploadedBy = userId,
            EntityType = entityType,
            EntityId = entityId,
        };
        db.Files.Add(obj);
        await db.SaveChangesAsync(ct);
        return obj;
    }

    public async Task<(Stream Stream, FileObject File)?> OpenAsync(Guid id, CancellationToken ct)
    {
        var f = await db.Files.FindAsync([id], ct);
        if (f is null)
            return null;
        var root = Path.GetFullPath(
            config["UPLOADS_ROOT"] ?? Path.Combine(AppContext.BaseDirectory, "uploads")
        );
        var full = Path.GetFullPath(Path.Combine(root, f.StoredPath));
        if (!IsWithinRoot(root, full) || !File.Exists(full))
            return null;
        return (File.OpenRead(full), f);
    }

    public async Task<FileObject?> DeleteAsync(Guid id, CancellationToken ct)
    {
        var file = await db.Files.FindAsync([id], ct);
        if (file is null)
            return null;
        var root = Path.GetFullPath(
            config["UPLOADS_ROOT"] ?? Path.Combine(AppContext.BaseDirectory, "uploads")
        );
        var full = Path.GetFullPath(Path.Combine(root, file.StoredPath));
        if (!IsWithinRoot(root, full))
            throw new InvalidOperationException("Invalid storage path.");
        if (File.Exists(full))
            File.Delete(full);
        db.Files.Remove(file);
        await db.SaveChangesAsync(ct);
        return file;
    }

    private static bool IsWithinRoot(string root, string full) =>
        full.StartsWith(
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase
        );

    private static async Task EnsureSignatureAsync(Stream input, string contentType, CancellationToken ct)
    {
        var header = new byte[512];
        var read = await input.ReadAsync(header, ct);
        if (input.CanSeek)
            input.Position = 0;
        var bytes = header.AsSpan(0, read);
        var valid = contentType switch
        {
            "application/pdf" => bytes.StartsWith("%PDF-"u8),
            "image/png" => bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => bytes.StartsWith(new byte[] { 255, 216, 255 }),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                bytes.StartsWith(new byte[] { 80, 75, 3, 4 }),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => bytes.StartsWith(
                new byte[] { 80, 75, 3, 4 }
            ),
            "text/plain" or "text/csv" => read > 0 && !bytes.Contains((byte)0) && IsUtf8(bytes),
            _ => false,
        };
        if (!valid)
            throw new InvalidOperationException("The file content does not match its declared type.");
    }

    private static bool IsUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
