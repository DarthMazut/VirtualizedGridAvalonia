using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace VirtualizedGridDemo.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private static readonly Random _rnd = new();

    [ObservableProperty]
    private ObservableCollection<GridItemVM> _items = new();

    [ObservableProperty]
    private int _maxColumns = 1000;

    public MainViewModel()
    {
        for (int y = 0; y < 1000; y++)
        {
            for (int x = 0; x < 1000; x++)
            {
                GridItemVM newItem = new(x, y);
                _items.Add(newItem);
            }
        }
    }
}
