using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using AuditLineage.Core;

var root = Path.Combine(Path.GetTempPath(), "audit-synthetic-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks++;
}
void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); } catch (Exception) { rejected = true; }
    Check(rejected, name);
}
const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
const string sheet = """
<?xml version="1.0" encoding="utf-8"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
 <sheetData><row r="1" hidden="1">
  <c r="A1"><v>918273.5</v></c>
  <c r="B1" t="n"><v>-4.25E2</v></c>
  <c r="C1" t="s"><v>0</v></c>
  <c r="D1"><f>A1+123456</f><v>1041729.5</v></c>
  <c r="E1" t="str"><f>"KEEP_TEXT"</f><v>KEEP_TEXT</v></c>
  <c r="F1" t="b"><v>1</v></c>
  <c r="G1" t="e"><v>#N/A</v></c>
  <c r="H1" s="1"><v>45000</v></c>
  <c r="I1" t="inlineStr"><is><t>=A1+98765</t></is></c>
  <c r="J1"><f t="shared" si="0" ref="J1:J2">A1*2</f><v>1836547</v></c>
  <c r="K1"><f t="array" ref="K1:K2">A1:A2*3</f><v>2754820.5</v></c>
  <c r="L1"><f>SUM(A1:B1)</f></c>
  <c r="M1" t="d"><v>2020-01-01T00:00:00</v></c>
  <c r="N1" s="1"/>
  <c r="O1"><v>0</v></c>
 </row><row r="2">
  <c r="J2"><f t="shared" si="0"/><v>88</v></c>
  <c r="K2"><v>99</v></c>
 </row></sheetData><mergeCells count="1"><mergeCell ref="N2:O2"/></mergeCells>
</worksheet>
""";
Dictionary<string, string> Parts(string worksheet) => new()
{
    ["[Content_Types].xml"] = """
    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
     <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
     <Default Extension="xml" ContentType="application/xml"/>
     <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
     <Override PartName="/xl/worksheets/custom.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
     <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
     <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
    </Types>
    """,
    ["_rels/.rels"] = """
    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
    """,
    ["xl/workbook.xml"] = """
    <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Synthetic" sheetId="1" r:id="rId1"/></sheets></workbook>
    """,
    ["xl/_rels/workbook.xml.rels"] = """
    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/custom.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
    """,
    ["xl/worksheets/custom.xml"] = worksheet,
    ["xl/sharedStrings.xml"] = $"<sst xmlns=\"{ns}\" count=\"1\" uniqueCount=\"1\"><si><t>SYNTHETIC_TEXT_987654</t></si></sst>",
    ["xl/styles.xml"] = $"<styleSheet xmlns=\"{ns}\"><fonts count=\"1\"><font/></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"0\"/><xf numFmtId=\"14\" applyNumberFormat=\"1\"/></cellXfs></styleSheet>"
};
void Create(string path, Dictionary<string, string> parts)
{
    using var z = ZipFile.Open(path, ZipArchiveMode.Create);
    foreach (var (name, text) in parts)
    { using var writer = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(text); }
}
Dictionary<string, string> Read(string path)
{
    using var z = ZipFile.OpenRead(path);
    return z.Entries.ToDictionary(e => e.FullName, e => { using var r = new StreamReader(e.Open()); return r.ReadToEnd(); });
}
try
{
    var input = Path.Combine(root, "虚构输入.xlsx");
    var output = Path.Combine(root, "output.xlsx");
    var original = Parts(sheet);
    Create(input, original);
    var hash = SHA256.HashData(File.ReadAllBytes(input));
    var result = NumericMasker.Process(input, output);
    Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(input))), "input unchanged");
    Check(result.ReplacedValues == 5 && result.ClearedCaches == 4 && result.PreservedFormulas == 6, "counts");
    var processed = Read(output);
    Check(processed.Count == original.Count, "package parts retained");
    foreach (var p in original.Keys.Where(p => p != "xl/worksheets/custom.xml"))
        Check(processed[p] == original[p], "non worksheet unchanged");
    XNamespace x = ns;
    var before = XDocument.Parse(sheet);
    var after = XDocument.Parse(processed["xl/worksheets/custom.xml"]);
    Check(before.Descendants(x + "f").Select(e => e.ToString()).SequenceEqual(after.Descendants(x + "f").Select(e => e.ToString())), "formula text and attributes retained");
    var cells = after.Descendants(x + "c").ToDictionary(c => (string)c.Attribute("r")!);
    foreach (var address in new[] { "A1", "B1", "H1", "O1", "K2" })
        Check(cells[address].Element(x + "v")?.Value == "0", "numeric replaced");
    foreach (var address in new[] { "D1", "J1", "K1", "J2", "L1" })
        Check(cells[address].Element(x + "v") is null, "numeric formula cache absent");
    foreach (var address in new[] { "C1", "E1", "F1", "G1", "I1", "M1", "N1" })
        Check(XNode.DeepEquals(before.Descendants(x + "c").Single(c => (string?)c.Attribute("r") == address), cells[address]), "nonnumeric cell retained");
    Check((string?)after.Descendants(x + "row").First().Attribute("hidden") == "1", "hidden row retained");
    Check(XNode.DeepEquals(before.Root!.Element(x + "mergeCells"), after.Root!.Element(x + "mergeCells")), "merges retained");
    Reject(() => NumericMasker.Process(input, input), "same input rejected");
    Reject(() => NumericMasker.Process(input, output), "overwrite rejected");
    Reject(() => NumericMasker.Process(input, Path.Combine(root, "wrong.xlsm")), "extension mismatch rejected");
    var cancelPath = Path.Combine(root, "cancel.xlsx");
    using var cancel = new CancellationTokenSource();
    cancel.Cancel();
    Reject(() => NumericMasker.Process(input, cancelPath, cancellationToken: cancel.Token), "cancel rejected");
    Check(!File.Exists(cancelPath), "cancel has no output");
    using var midCancel = new CancellationTokenSource();
    var midPath = Path.Combine(root, "mid-cancel.xlsx");
    Reject(() => NumericMasker.Process(input, midPath, new CancelProgress(midCancel), midCancel.Token), "mid process cancel");
    Check(!File.Exists(midPath) && !Directory.EnumerateFiles(root, ".audit-*.tmp").Any(), "mid cancel cleanup");
    var broken = Path.Combine(root, "broken.xlsx");
    Create(broken, Parts("<worksheet xmlns=\"" + ns + "\"><broken>"));
    var brokenOutput = Path.Combine(root, "broken-output.xlsx");
    Reject(() => NumericMasker.Process(broken, brokenOutput), "malformed rejected");
    Check(!File.Exists(brokenOutput) && !Directory.EnumerateFiles(root, ".audit-*.tmp").Any(), "failure cleanup");
    var strict = Path.Combine(root, "strict.xlsx");
    Create(strict, Parts(sheet.Replace(ns, "http://purl.oclc.org/ooxml/spreadsheetml/main")));
    Check(NumericMasker.Process(strict, Path.Combine(root, "strict-output.xlsx")).ReplacedValues == 5, "strict namespace");
    var signed = Path.Combine(root, "signed.xlsx");
    var signedParts = Parts(sheet); signedParts["_xmlsignatures/sig1.xml"] = "<signature/>";
    Create(signed, signedParts);
    Reject(() => NumericMasker.Process(signed, Path.Combine(root, "signed-output.xlsx")), "signature rejected");
    var macro = Path.Combine(root, "synthetic.xlsm");
    var macroParts = Parts(sheet);
    macroParts["[Content_Types].xml"] = macroParts["[Content_Types].xml"].Replace("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", "application/vnd.ms-excel.sheet.macroEnabled.main+xml");
    macroParts["xl/vbaProject.bin"] = "SYNTHETIC_OPAQUE_BYTES";
    macroParts["xl/externalLinks/externalLink1.xml"] = "<syntheticCache>123456</syntheticCache>";
    Create(macro, macroParts);
    var macroOutput = Path.Combine(root, "macro-output.xlsm");
    var macroResult = NumericMasker.Process(macro, macroOutput);
    Check(macroResult.HasOtherDataParts && macroResult.ReplacedValues == 5, "xlsm and retained data indication");
    Check(Read(macroOutput)["xl/vbaProject.bin"] == macroParts["xl/vbaProject.bin"], "opaque part unchanged");
    Check(Read(macroOutput)["xl/externalLinks/externalLink1.xml"] == macroParts["xl/externalLinks/externalLink1.xml"], "external cache deliberately retained");
    var sampleDir = Environment.GetEnvironmentVariable("AUDIT_SYNTHETIC_OUTPUT");
    if (!string.IsNullOrEmpty(sampleDir))
    {
        Directory.CreateDirectory(sampleDir);
        File.Copy(input, Path.Combine(sampleDir, "synthetic-input.xlsx"), true);
        File.Copy(output, Path.Combine(sampleDir, "synthetic-output.xlsx"), true);
    }
    Console.WriteLine($"PASS: {checks} synthetic assertions. No real audit files were used.");
}
finally { Directory.Delete(root, recursive: true); }

sealed class CancelProgress(CancellationTokenSource source) : IProgress<MaskProgress>
{
    public void Report(MaskProgress value) => source.Cancel();
}
