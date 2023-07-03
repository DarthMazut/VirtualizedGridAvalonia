using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VirtualizedGridDemo.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private static readonly Random _rnd = new();

    [ObservableProperty]
    private ObservableCollection<GridItemVM> _items = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingItems))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private int _itemsHorizontally = 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingItems))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private int _itemsVertically = 1000;

    [ObservableProperty]
    private int _maxItemsInRow = 1000;

    public int PendingItems => ItemsHorizontally * ItemsVertically;

    public double ManagedMemoryUsed => (double)GC.GetTotalMemory(true) / (1024 * 1024);

    public double TotalMemoryUsed => (double)Process.GetCurrentProcess().PrivateMemorySize64 / (1024 * 1024);

    public MainViewModel()
    {
        Apply();
        ScheduleMemoryStateUpdate();
    }

    [RelayCommand(CanExecute = nameof(CanResetOrApply))]
    private void Reset()
    {
        ItemsHorizontally = MaxItemsInRow;
        ItemsVertically = Items.Count / MaxItemsInRow;
    }

    [RelayCommand(CanExecute = nameof(CanResetOrApply))]
    private void Apply()
    {
        List<GridItemVM> newItems = new();
        for (int y = 0; y < ItemsVertically; y++)
        {
            for (int x = 0; x < ItemsHorizontally; x++)
            {
                GridItemVM newItem = new(x, y);
                newItems.Add(newItem);
            }
        }

        Items = new ObservableCollection<GridItemVM>(newItems);
        MaxItemsInRow = ItemsHorizontally;
        ApplyCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
    }

    private bool CanResetOrApply() => PendingItems != Items.Count;

    private void ScheduleMemoryStateUpdate()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(500);
                OnPropertyChanged(nameof(ManagedMemoryUsed));
                OnPropertyChanged(nameof(TotalMemoryUsed));
            }
        });
    }
}
