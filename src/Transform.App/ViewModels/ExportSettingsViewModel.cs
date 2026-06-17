using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;

namespace Transform.App.ViewModels;

public partial class ExportSettingsViewModel : ObservableObject
{
    public ExportSettingsViewModel()
    {
        ExportTypes = new List<string> { "Altium Designer", "KiCad", "Eagle", "OrCAD" };
        SelectedExportType = "Altium Designer";
        LibraryName = "NpnpLibrary";
    }

    public List<string> ExportTypes { get; }

    [ObservableProperty]
    private string _selectedExportType = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _libraryName = string.Empty;

    [ObservableProperty]
    private bool _embedStepModel = true;

    [ObservableProperty]
    private bool _mergeOutput = false;

    [ObservableProperty]
    private bool _appendMode = false;

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        // WPF 内置的文件夹选择对话框（兼容 Win11）
        var folderDialog = new OpenFolderDialog
        {
            Title = "选择输出目录",
            InitialDirectory = string.IsNullOrEmpty(OutputDirectory)
                ? System.IO.Directory.GetCurrentDirectory()
                : OutputDirectory
        };

        if (folderDialog.ShowDialog() == true)
        {
            OutputDirectory = folderDialog.FolderName;
        }
    }
}