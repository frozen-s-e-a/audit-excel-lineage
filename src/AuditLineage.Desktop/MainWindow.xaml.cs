using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AuditLineage.Core;
using Microsoft.Win32;

namespace AuditLineage.Desktop;

public sealed class FileRow : INotifyPropertyChanged
{
    public required string Path { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);
    private string status = "待处理";
    public string Status { get => status; set { status = value; PropertyChanged?.Invoke(this, new(nameof(Status))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class MainWindow : Window
{
    private readonly ObservableCollection<FileRow> files = [];
    private string? outputDirectory;
    private CancellationTokenSource? cancellation;
    public MainWindow() { InitializeComponent(); FilesList.ItemsSource = files; }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel 工作簿|*.xlsx;*.xlsm", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        foreach (var path in dialog.FileNames)
            if (!files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
                files.Add(new() { Path = path });
    }
    private void Clear_Click(object sender, RoutedEventArgs e) => files.Clear();
    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择输出文件夹" };
        if (dialog.ShowDialog(this) != true) return;
        outputDirectory = dialog.FolderName;
        OutputLabel.Text = "输出文件夹：" + outputDirectory;
    }
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (files.Count == 0 || outputDirectory is null)
        { MessageBox.Show(this, "请添加文件并选择输出文件夹。", "准备文件"); return; }
        cancellation = new();
        var token = cancellation.Token;
        AddButton.IsEnabled = ClearButton.IsEnabled = FolderButton.IsEnabled = StartButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        Progress.Value = 0;
        int completed = 0, failed = 0;
        foreach (var row in files) row.Status = "待处理";
        try
        {
            foreach (var row in files)
            {
                token.ThrowIfCancellationRequested();
                row.Status = "正在处理…";
                StatusLabel.Text = $"正在处理第 {completed + failed + 1} / {files.Count} 个文件";
                var progress = new Progress<MaskProgress>(p => Progress.Value =
                    100.0 * (completed + failed + (double)p.CompletedParts / p.TotalParts) / files.Count);
                var target = Path.Combine(outputDirectory,
                    Path.GetFileNameWithoutExtension(row.Path) + ".numeric-only." +
                    Guid.NewGuid().ToString("N")[..8] + Path.GetExtension(row.Path));
                try
                {
                    var result = await Task.Run(() => NumericMasker.Process(row.Path, target, progress, token));
                    row.Status = $"完成：{result.ReplacedValues:N0} 数值，{result.ClearedCaches:N0} 缓存" +
                        (result.HasOtherDataParts ? "；附属数据保留" : "");
                    completed++;
                }
                catch (OperationCanceledException) { row.Status = "已取消，未生成该文件"; throw; }
                catch (Exception ex)
                {
                    failed++;
                    row.Status = ex switch
                    {
                        UnauthorizedAccessException => "失败：无读写权限",
                        NotSupportedException => "失败：格式或数字签名不支持",
                        InvalidDataException => "失败：文件加密、损坏或超出处理预算",
                        IOException => "失败：文件被占用、磁盘不足或输出冲突",
                        _ => "失败：无法处理该文件"
                    };
                }
            }
            Progress.Value = 100;
            StatusLabel.Text = $"完成 {completed} 个，失败 {failed} 个。输出仅作数值处理测试，重新计算会改变显示结果。";
        }
        catch (OperationCanceledException)
        { StatusLabel.Text = $"已取消；已完成的 {completed} 个文件保留，其他文件未处理。"; }
        finally
        {
            cancellation?.Dispose(); cancellation = null;
            AddButton.IsEnabled = ClearButton.IsEnabled = FolderButton.IsEnabled = StartButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e)
    { cancellation?.Cancel(); CancelButton.IsEnabled = false; StatusLabel.Text = "正在取消并清理当前临时文件…"; }
    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (outputDirectory is null || !Directory.Exists(outputDirectory)) return;
        try { Process.Start(new ProcessStartInfo(outputDirectory) { UseShellExecute = true }); }
        catch (Exception) { MessageBox.Show(this, "无法打开输出文件夹，请手动访问。", "打开文件夹"); }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (cancellation is null) return;
        e.Cancel = true;
        cancellation.Cancel();
        StatusLabel.Text = "正在取消并清理临时文件，请稍后关闭窗口。";
    }
}
