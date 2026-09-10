using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace AuditLineage.Core;

public sealed record MaskProgress(int CompletedParts, int TotalParts);
public sealed record MaskResult(string OutputPath, long ReplacedValues, long ClearedCaches,
    long PreservedFormulas, int Worksheets, bool HasOtherDataParts);

/// <summary>Numeric worksheet cells only. This is not a full anonymization engine.</summary>
public static class NumericMasker
{
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string StrictMain = "http://purl.oclc.org/ooxml/spreadsheetml/main";
    private const long MaxExpandedBytes = 8L * 1024 * 1024 * 1024;
    private static XmlReaderSettings ReaderSettings => new()
    {
        DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
        MaxCharactersInDocument = MaxExpandedBytes, CloseInput = false
    };

    public static MaskResult Process(string inputPath, string outputPath,
        IProgress<MaskProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        inputPath = Path.GetFullPath(inputPath);
        outputPath = Path.GetFullPath(outputPath);
        var extension = Path.GetExtension(inputPath).ToLowerInvariant();
        if (extension is not (".xlsx" or ".xlsm"))
            throw new NotSupportedException("Only .xlsx and .xlsm are supported.");
        if (!string.Equals(extension, Path.GetExtension(outputPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output must keep the input extension.");
        if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Input and output must differ.");
        if (File.Exists(outputPath)) throw new IOException("Output already exists.");
        var directory = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, ".audit-" + Guid.NewGuid().ToString("N") + ".tmp");
        long values = 0, caches = 0, formulas = 0;
        int worksheets = 0;
        bool otherParts;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var source = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var zip = new ZipArchive(source, ZipArchiveMode.Read))
            {
                if (zip.Entries.Count > 100_000 || zip.Entries.Sum(e => e.Length) > MaxExpandedBytes)
                    throw new InvalidDataException("Package exceeds the processing budget.");
                if (zip.Entries.Select(e => e.FullName).Distinct(StringComparer.Ordinal).Count() != zip.Entries.Count)
                    throw new InvalidDataException("Duplicate package entries.");
                if (zip.Entries.Any(e => e.FullName.StartsWith("_xmlsignatures/", StringComparison.OrdinalIgnoreCase)))
                    throw new NotSupportedException("Digitally signed packages are not supported.");
                var contentTypes = zip.GetEntry("[Content_Types].xml")
                    ?? throw new InvalidDataException("Not an OOXML workbook.");
                if (contentTypes.Length > 4 * 1024 * 1024)
                    throw new InvalidDataException("Content types exceed the processing budget.");
                HashSet<string> worksheetParts;
                using (var stream = contentTypes.Open())
                using (var reader = XmlReader.Create(stream, ReaderSettings))
                {
                    var types = XDocument.Load(reader);
                    worksheetParts = types.Descendants()
                        .Where(e => e.Name.LocalName == "Override" &&
                            ((string?)e.Attribute("ContentType"))?.EndsWith("spreadsheetml.worksheet+xml", StringComparison.Ordinal) == true)
                        .Select(e => Uri.UnescapeDataString(((string?)e.Attribute("PartName") ?? "").TrimStart('/')))
                        .ToHashSet(StringComparer.Ordinal);
                }
                if (worksheetParts.Count == 0 || worksheetParts.Any(p => zip.GetEntry(p) is null))
                    throw new InvalidDataException("Worksheet parts are missing.");
                otherParts = zip.Entries.Any(e => e.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase)
                    || e.FullName.StartsWith("xl/pivotCache/", StringComparison.OrdinalIgnoreCase)
                    || e.FullName.StartsWith("xl/charts/", StringComparison.OrdinalIgnoreCase)
                    || e.FullName.StartsWith("xl/embeddings/", StringComparison.OrdinalIgnoreCase)
                    || e.FullName.StartsWith("xl/model/", StringComparison.OrdinalIgnoreCase)
                    || e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase));
                using var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var output = new ZipArchive(destination, ZipArchiveMode.Create);
                int completed = 0;
                foreach (var entry in zip.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var newEntry = output.CreateEntry(entry.FullName, CompressionLevel.Fastest);
                    newEntry.LastWriteTime = entry.LastWriteTime;
                    using var from = entry.Open();
                    using var to = newEntry.Open();
                    if (worksheetParts.Contains(entry.FullName))
                    {
                        TransformWorksheet(from, to, ref values, ref caches, ref formulas, cancellationToken);
                        worksheets++;
                    }
                    else Copy(from, to, cancellationToken);
                    progress?.Report(new(++completed, zip.Entries.Count));
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, outputPath, overwrite: false);
            return new(outputPath, values, caches, formulas, worksheets, otherParts);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void Copy(Stream source, Stream destination, CancellationToken token)
    {
        var buffer = new byte[128 * 1024];
        int count;
        while ((count = source.Read(buffer)) > 0)
        {
            token.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, count);
        }
    }

    private static void TransformWorksheet(Stream source, Stream destination, ref long values,
        ref long caches, ref long formulas, CancellationToken token)
    {
        using var reader = XmlReader.Create(source, ReaderSettings);
        using var writer = XmlWriter.Create(destination, new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(false), CloseOutput = false,
            Indent = false, OmitXmlDeclaration = false
        });
        reader.MoveToContent();
        if (reader.LocalName != "worksheet" || reader.NamespaceURI is not (Main or StrictMain))
            throw new InvalidDataException("Unsupported worksheet XML.");
        var mainNamespace = reader.NamespaceURI;
        while (!reader.EOF)
        {
            token.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "c" && reader.NamespaceURI == mainNamespace)
            {
                // Only one cell is materialized; no whole worksheet DOM or formula parsing.
                var cell = (XElement)XNode.ReadFrom(reader);
                XNamespace ns = mainNamespace;
                var formula = cell.Element(ns + "f");
                var value = cell.Element(ns + "v");
                var type = (string?)cell.Attribute("t");
                if (formula is not null) formulas++;
                if ((type is null or "n") && value is not null && !string.IsNullOrWhiteSpace(value.Value))
                {
                    if (formula is not null) { value.Remove(); caches++; }
                    else { value.Value = "0"; values++; }
                }
                cell.WriteTo(writer);
                continue;
            }
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    bool empty = reader.IsEmptyElement;
                    writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                    writer.WriteAttributes(reader, false);
                    if (empty) writer.WriteEndElement();
                    break;
                case XmlNodeType.EndElement: writer.WriteFullEndElement(); break;
                case XmlNodeType.Text: writer.WriteString(reader.Value); break;
                case XmlNodeType.CDATA: writer.WriteCData(reader.Value); break;
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace: writer.WriteWhitespace(reader.Value); break;
                case XmlNodeType.Comment: writer.WriteComment(reader.Value); break;
                case XmlNodeType.ProcessingInstruction: writer.WriteProcessingInstruction(reader.Name, reader.Value); break;
            }
            reader.Read();
        }
    }
}
