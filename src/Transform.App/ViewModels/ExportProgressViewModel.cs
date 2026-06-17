using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npnp.Core.Services;

namespace Transform.App.ViewModels;

public partial class ExportProgressViewModel : ObservableObject
{
    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _currentComponent = string.Empty;

    [ObservableProperty]
    private ExportStatus _status = ExportStatus.Idle;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private int _totalCount;

    public IProgress<ExportProgress> ProgressReporter { get; }

    public ExportProgressViewModel()
    {
        ProgressReporter = new Progress<ExportProgress>(UpdateProgress);
    }

    private void UpdateProgress(ExportProgress progress)
    {
        TotalCount = progress.Total;
        CompletedCount = progress.Completed;
        FailedCount = progress.Failed;
        CurrentComponent = progress.CurrentComponent;
        Status = progress.Status;

        if (progress.Total > 0)
        {
            Progress = (int)((progress.Completed + progress.Failed) * 100.0 / progress.Total);
        }
    }

    public void Reset()
    {
        Progress = 0;
        CurrentComponent = string.Empty;
        Status = ExportStatus.Idle;
        CompletedCount = 0;
        FailedCount = 0;
        TotalCount = 0;
    }

    public void Reset(int total)
    {
        Progress = 0;
        CurrentComponent = string.Empty;
        Status = ExportStatus.Running;
        CompletedCount = 0;
        FailedCount = 0;
        TotalCount = total;
    }

    [RelayCommand]
    private void Cancel()
    {
        Status = ExportStatus.Canceled;
    }
}