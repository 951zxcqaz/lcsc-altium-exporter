#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Npnp.Core.Models;
using Npnp.Core.Services;
using Transform.App.Services;
using Transform.App.ViewModels;

namespace Transform.App
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        private readonly ILcscApiService? _lcscApiService;
        private SearchItem? _selectedSearchItem;
        private readonly System.Windows.Threading.DispatcherTimer? _clipboardTimer;
        private string _lastClipboardText = string.Empty;

        public MainWindow(MainViewModel viewModel, ILcscApiService? lcscApiService)
        {
            InitializeComponent();
            ViewModel = viewModel;
            _lcscApiService = lcscApiService;

            SearchResultsList.ItemsSource = ViewModel.SearchResults;
            ComponentsList.ItemsSource = ViewModel.ComponentListViewModel.Components;
            SearchFieldCombo.SelectedIndex = 0;

            // 设置默认输出目录
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            OutputDirectoryBox.Text = Path.Combine(desktop, "LcscExport");

            // 初始化剪贴板监听定时器（每400毫秒检查一次）
            _clipboardTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _clipboardTimer.Tick += ClipboardTimer_Tick;

            // 窗口关闭时停止定时器
            Closing += (s, e) => _clipboardTimer?.Stop();
        }

        private void UpdateStatus(string message)
        {
            BottomStatusText.Text = message;
        }

        private void UpdateSearchResultCount()
        {
            SearchResultCount.Text = $"({ViewModel.SearchResults.Count} 个元件)";
        }

        private void UpdateComponentCount()
        {
            var components = ViewModel.ComponentListViewModel.Components;
            ComponentCount.Text = $"({components.Count} 个)";
            UpdateExportHint();
        }

        private void UpdateExportHint()
        {
            var components = ViewModel.ComponentListViewModel.Components;
            ExportHintText.Text = components.Count == 0
                ? "在搜索结果中选择元件并添加到导出列表，然后点击导出按钮。"
                : $"当前有 {components.Count} 个元件待导出。";
        }

        private void ClipboardAutoReadBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ClipboardAutoReadBox.IsChecked == true)
            {
                _lastClipboardText = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
                _clipboardTimer?.Start();
                UpdateStatus("剪贴板自动读取已开启，复制内容后将自动搜索");
            }
            else
            {
                _clipboardTimer?.Stop();
                UpdateStatus("剪贴板自动读取已关闭");
            }
        }

        private async void ClipboardTimer_Tick(object? sender, EventArgs e)
        {
            if (ClipboardAutoReadBox.IsChecked != true) return;
            if (!Clipboard.ContainsText()) return;

            var currentText = Clipboard.GetText().Trim();
            if (string.IsNullOrWhiteSpace(currentText)) return;
            if (currentText == _lastClipboardText) return;

            _lastClipboardText = currentText;
            await AutoSearchAndAddFirstResult(currentText);
        }

        private async Task AutoSearchAndAddFirstResult(string keyword)
        {
            SearchBox.Text = keyword;
            var selectedTag = (SearchFieldCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "union";
            ViewModel.SearchKeyword = keyword;
            ViewModel.SelectedSearchFieldKey = selectedTag;

            SearchButton.IsEnabled = false;
            try
            {
                await ViewModel.SearchAsync();
                UpdateSearchResultCount();
                AutoUpdateLibraryName(keyword);

                if (ViewModel.SearchResults.Count > 0)
                {
                    var first = ViewModel.SearchResults[0];
                    SearchResultsList.SelectedItem = first;
                    _selectedSearchItem = first;
                    UpdateComponentDetail(first);

                    var component = new ComponentDetail(
                        lcscId: first.LcscId,
                        name: first.Name,
                        description: first.Description ?? string.Empty,
                        manufacturer: first.Manufacturer ?? string.Empty)
                    {
                        Package = first.Package ?? string.Empty,
                        ManufacturerPart = first.ManufacturerPart ?? string.Empty,
                        Supplier = first.Supplier ?? string.Empty,
                        Value = first.Value ?? string.Empty,
                        Tolerance = first.Tolerance ?? string.Empty,
                        Power = first.Power ?? string.Empty,
                        Voltage = first.Voltage ?? string.Empty,
                        SpecSummary = first.SpecSummary ?? string.Empty,
                        Category = first.Category ?? string.Empty,
                        SubCategory = first.SubCategory ?? string.Empty,
                        ComponentType = first.ComponentType ?? string.Empty,
                        SymbolUuid = first.SymbolUuid ?? string.Empty,
                        FootprintUuid = first.FootprintUuid ?? string.Empty,
                        Model3DUuid = first.Model3DUuid ?? string.Empty,
                    };

                    if (ViewModel.ComponentListViewModel.Components.FirstOrDefault(c => c.LcscId == component.LcscId) == null)
                    {
                        ViewModel.ComponentListViewModel.Components.Add(component);
                        UpdateComponentCount();
                        UpdateStatus($"[自动] 已添加: {component.Name} ({component.LcscId})");
                    }
                    else
                    {
                        UpdateStatus($"[自动] 元件『{component.Name}』已在列表中，跳过");
                    }
                }
                else
                {
                    UpdateStatus($"[自动] 未找到匹配结果: {keyword}");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"[自动] 搜索失败: {ex.Message}");
            }
            finally
            {
                SearchButton.IsEnabled = true;
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SearchButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var keyword = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                MessageBox.Show("请输入搜索关键词。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var selectedTag = (SearchFieldCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "union";
            ViewModel.SearchKeyword = keyword;
            ViewModel.SelectedSearchFieldKey = selectedTag;
            SearchButton.IsEnabled = false;
            try { await ViewModel.SearchAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SearchButton.IsEnabled = true;
                UpdateSearchResultCount();
                AutoUpdateLibraryName(keyword);
            }
        }

        /// <summary>
        /// 从搜索关键词自动提取库名称
        /// 优先级：型号 > 名称 > 编号 > 普通关键词
        /// </summary>
        private void AutoUpdateLibraryName(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;

            var normalized = keyword.Trim();
            var libraryName = ExtractFieldValue(normalized, "型号")
                        ?? ExtractFieldValue(normalized, "名称")
                        ?? ExtractFieldValue(normalized, "编号")
                        ?? ExtractFieldValue(normalized, "品牌")
                        ?? ExtractFieldValue(normalized, "封装")
                        ?? normalized;

            // 清理非法字符
            libraryName = SanitizeFileName(libraryName);
            
            // 如果太长，截断
            if (libraryName.Length > 50) libraryName = libraryName.Substring(0, 50);

            LibraryNameBox.Text = libraryName;
        }

        /// <summary>
        /// 从搜索字符串中提取指定字段的值
        /// </summary>
        private string? ExtractFieldValue(string input, string fieldName)
        {
            // 支持中文冒号和英文冒号
            var patterns = new[]
            {
                $"{fieldName}:",
                $"{fieldName}："
            };

            foreach (var pattern in patterns)
            {
                var idx = input.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var start = idx + pattern.Length;
                    var end = input.IndexOf(' ', start);
                    if (end < 0) end = input.Length;
                    
                    var value = input.Substring(start, end - start).Trim();
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
            return null;
        }

        /// <summary>
        /// 清理文件名中的非法字符
        /// </summary>
        private string SanitizeFileName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }

        private void SearchResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            AddSelectedToExport();
        }

        private void AddToExportButton_Click(object sender, RoutedEventArgs e)
        {
            AddSelectedToExport();
        }

        private void AddSelectedToExport()
        {
            var selected = SearchResultsList.SelectedItem as SearchItem;
            if (selected == null)
            {
                MessageBox.Show("请先在搜索结果中选择一个元件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _selectedSearchItem = selected;
            UpdateComponentDetail(selected);

            var component = new ComponentDetail(
                lcscId: selected.LcscId,
                name: selected.Name,
                description: selected.Description ?? string.Empty,
                manufacturer: selected.Manufacturer ?? string.Empty)
            {
                Package = selected.Package ?? string.Empty,
                ManufacturerPart = selected.ManufacturerPart ?? string.Empty,
                Supplier = selected.Supplier ?? string.Empty,
                Value = selected.Value ?? string.Empty,
                Tolerance = selected.Tolerance ?? string.Empty,
                Power = selected.Power ?? string.Empty,
                Voltage = selected.Voltage ?? string.Empty,
                SpecSummary = selected.SpecSummary ?? string.Empty,
                Category = selected.Category ?? string.Empty,
                SubCategory = selected.SubCategory ?? string.Empty,
                ComponentType = selected.ComponentType ?? string.Empty,
                SymbolUuid = selected.SymbolUuid ?? string.Empty,
                FootprintUuid = selected.FootprintUuid ?? string.Empty,
                Model3DUuid = selected.Model3DUuid ?? string.Empty,
            };

            if (ViewModel.ComponentListViewModel.Components.FirstOrDefault(c => c.LcscId == component.LcscId) != null)
            {
                UpdateStatus($"元件『{component.Name}』已在列表中");
                return;
            }

            ViewModel.ComponentListViewModel.Components.Add(component);
            UpdateComponentCount();
            UpdateStatus($"已添加: {component.Name} ({component.LcscId})");
        }

        private void UpdateComponentDetail(SearchItem item)
        {
            ComponentDetailText.Text = $"编号: {item.LcscId}\n" +
                $"名称: {item.Name}\n型号: {item.ManufacturerPart}\n品牌: {item.Manufacturer}\n" +
                $"封装: {item.Package}\n渠道: {item.Supplier}\n数值: {item.Value}\n" +
                $"精度: {item.Tolerance}\n功率: {item.Power}\n电压: {item.Voltage}\n" +
                $"规格: {item.SpecSummary}\n类别: {item.Category}/{item.SubCategory}\n" +
                $"元件类型: {item.ComponentType}\n描述: {item.Description}\n" +
                $"Symbol UUID: {item.SymbolUuid}\nFootprint UUID: {item.FootprintUuid}";
        }

        private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = ComponentsList.SelectedItem as ComponentDetail;
            if (selected == null)
            {
                MessageBox.Show("请先在待导出元件列表中选择一个元件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ViewModel.ComponentListViewModel.Components.Remove(selected);
            UpdateStatus($"已移除: {selected.Name}");
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            var components = ViewModel.ComponentListViewModel.Components;
            if (components.Count == 0) return;
            var result = MessageBox.Show($"确定要清空所有 {components.Count} 个元件吗？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.OK) components.Clear();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "选择导出目录",
                InitialDirectory = string.IsNullOrEmpty(OutputDirectoryBox.Text)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : OutputDirectoryBox.Text
            };
            if (folderDialog.ShowDialog() == true) OutputDirectoryBox.Text = folderDialog.FolderName;
        }

        /// <summary>
        /// 导出按钮：调用 npnp.exe batch 生成 Altium 格式的 SchLib 和 PcbLib 文件
        /// </summary>
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var components = ViewModel.ComponentListViewModel.Components;
            if (components.Count == 0)
            {
                MessageBox.Show("请先从搜索结果中选择并添加至少一个元件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 1) 确定输出目录
            if (string.IsNullOrWhiteSpace(OutputDirectoryBox.Text))
            {
                OutputDirectoryBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "LcscExport");
            }
            var outputDir = OutputDirectoryBox.Text.Trim();
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            // 2) 找到 npnp.exe
            var possiblePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "tools", "npnp.exe"),
                Path.Combine(AppContext.BaseDirectory, "npnp.exe"),
                Path.Combine(Directory.GetCurrentDirectory(), "tools", "npnp.exe"),
            };
            string? npnpExe = possiblePaths.FirstOrDefault(File.Exists);
            if (npnpExe == null)
            {
                MessageBox.Show(
                    "找不到 npnp.exe 工具。\n\n请确认 tools\\npnp.exe 存在于程序目录下。\n下载地址：https://github.com/linkyourbin/npnp/releases",
                    "工具未找到", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3) 写入 LCSC ID 列表
            var idsPath = Path.Combine(outputDir, "lcsc_ids.txt");
            var lcscIds = components
                .Select(c => c.LcscId?.Trim())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();
            await File.WriteAllLinesAsync(idsPath, lcscIds);

            ExportButton.IsEnabled = false;
            ExportProgressBar.Value = 0;
            TotalCountText.Text = lcscIds.Count.ToString();
            CompletedCountText.Text = "0";
            FailedCountText.Text = "0";
            UpdateStatus($"正在调用 npnp 导出 {lcscIds.Count} 个元件...");

            try
            {
                // 4) 根据界面选项构建 npnp 参数
                var argList = new List<string>
                {
                    "batch",
                    "--input",
                    $"\"{idsPath}\"",
                    "--output",
                    $"\"{outputDir}\""
                };

                if (EmbedStepModelBox.IsChecked == true) argList.Add("--full");
                argList.Add("--force");
                argList.Add("--continue-on-error");

                if (MergeOutputBox.IsChecked == true)
                {
                    argList.Add("--merge");
                    var libName = LibraryNameBox.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(libName))
                    {
                        libName = SanitizeFileName(libName);
                        argList.Add("--library-name");
                        argList.Add($"\"{libName}\"");
                    }
                    if (AppendModeBox.IsChecked == true) argList.Add("--append");
                }

                var arguments = string.Join(" ", argList);

                // 5) 调用 npnp batch
                var psi = new ProcessStartInfo
                {
                    FileName = npnpExe,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = outputDir
                };

                var outputLines = new System.Collections.Concurrent.ConcurrentBag<string>();
                var progressRegex = new Regex(@"\[.*\]\s+(\d+)/(\d+)");

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    MessageBox.Show("无法启动 npnp.exe", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 异步捕获输出
                _ = Task.Run(() =>
                {
                    string? line;
                    while ((line = proc.StandardOutput.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            outputLines.Add(line);
                            Dispatcher.Invoke(() =>
                            {
                                UpdateStatus(line);
                                var m = progressRegex.Match(line);
                                if (m.Success && int.TryParse(m.Groups[1].Value, out var cur) && int.TryParse(m.Groups[2].Value, out var tot))
                                {
                                    CompletedCountText.Text = cur.ToString();
                                    ExportProgressBar.Value = cur * 100.0 / Math.Max(1, tot);
                                }
                            });
                        }
                    }
                    while ((line = proc.StandardError.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            outputLines.Add(line);
                            Dispatcher.Invoke(() => UpdateStatus(line));
                        }
                    }
                });

                // 等待（每个元件约 3 秒，最多 10 分钟）
                var timeoutMs = Math.Max(60_000, lcscIds.Count * 8_000);
                var completedInTime = await Task.Run(() => proc.WaitForExit(timeoutMs));

                if (!completedInTime)
                {
                    try { proc.Kill(); } catch { }
                    MessageBox.Show($"导出超时（>{timeoutMs / 1000}s）。", "超时", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int exitCode = proc.ExitCode;
                var allOutput = string.Join(Environment.NewLine, outputLines);

                // 5) 枚举生成的文件
                var schlibDir = Path.Combine(outputDir, "schlib");
                var pcblibDir = Path.Combine(outputDir, "pcblib");
                var schLibs = Directory.Exists(schlibDir) ? Directory.GetFiles(schlibDir, "*.SchLib") : Array.Empty<string>();
                var pcbLibs = Directory.Exists(pcblibDir) ? Directory.GetFiles(pcblibDir, "*.PcbLib") : Array.Empty<string>();

                if (schLibs.Length > 0 || pcbLibs.Length > 0)
                {
                    ExportProgressBar.Value = 100;
                    var report = "🎉 导出完成！\n\n";

                    var modeText = MergeOutputBox.IsChecked == true ? "（合并模式）" : "（每个元件单独文件）";
                    var mergeLabel = MergeOutputBox.IsChecked == true ? "合并库" : "文件";
                    var stepModelText = EmbedStepModelBox.IsChecked == true ? "（含 3D STEP 模型）" : "";

                    if (schLibs.Length > 0)
                    {
                        report += $"📄 原理图库 (.SchLib)：{schLibs.Length} 个{mergeLabel} {modeText}\n";
                        report += $"   文件夹：{schlibDir}\n";
                        foreach (var f in schLibs.Take(10))
                            report += $"     • {Path.GetFileName(f)}  ({new FileInfo(f).Length / 1024.0:F1} KB)\n";
                        if (schLibs.Length > 10) report += $"     • ... 另有 {schLibs.Length - 10} 个文件\n";
                    }
                    if (pcbLibs.Length > 0)
                    {
                        report += $"\n🔧 PCB 封装库 (.PcbLib)：{pcbLibs.Length} 个{mergeLabel} {stepModelText}\n";
                        report += $"   文件夹：{pcblibDir}\n";
                        foreach (var f in pcbLibs.Take(10))
                            report += $"     • {Path.GetFileName(f)}  ({new FileInfo(f).Length / 1024.0:F1} KB)\n";
                        if (pcbLibs.Length > 10) report += $"     • ... 另有 {pcbLibs.Length - 10} 个文件\n";
                    }
                    report += "\n💡 使用方法：\n";
                    report += "   1) 在 Altium Designer 中直接打开 .SchLib 查看原理图符号\n";
                    report += "   2) 在 Altium Designer 中直接打开 .PcbLib 查看封装";
                    if (EmbedStepModelBox.IsChecked == true) report += "（含 3D 模型）";
                    UpdateStatus($"导出完成：{schLibs.Length} 个 SchLib，{pcbLibs.Length} 个 PcbLib {stepModelText}");
                    MessageBox.Show(report, "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    try { Process.Start(new ProcessStartInfo(outputDir) { UseShellExecute = true }); } catch { }
                }
                else
                {
                    UpdateStatus("导出结束，但未生成任何文件。");
                    MessageBox.Show(
                        $"npnp 已执行（退出码: {exitCode}），但未找到 SchLib/PcbLib 文件。\n\n输出：\n{allOutput}\n\n请检查：\n1) 网络是否正常\n2) LCSC ID 是否有效",
                        "没有生成文件", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"导出失败: {ex.Message}");
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExportButton.IsEnabled = true;
            }
        }
    }
}
