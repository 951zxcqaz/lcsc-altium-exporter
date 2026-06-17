#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npnp.Core.Models;

namespace Transform.App.ViewModels;

public partial class ComponentListViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ComponentDetail> _components = new();

    [ObservableProperty]
    private ComponentDetail? _selectedComponent;

    [RelayCommand]
    private void AddComponent(ComponentDetail component)
    {
        if (!Components.Any(c => c.LcscId == component.LcscId))
        {
            Components.Add(component);
        }
    }

    [RelayCommand]
    private void RemoveComponent(ComponentDetail component)
    {
        Components.Remove(component);
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedComponent != null)
        {
            Components.Remove(SelectedComponent);
            SelectedComponent = null;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        Components.Clear();
        SelectedComponent = null;
    }

    public IEnumerable<string> GetLcscIds()
    {
        return Components.Select(c => c.LcscId);
    }
}