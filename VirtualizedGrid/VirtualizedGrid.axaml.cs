using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media.TextFormatting.Unicode;
using Avalonia.Threading;
using System.Collections;
using System.Collections.Specialized;

namespace VirtualizedGrid
{
    public class VirtualizedGrid : TemplatedControl
    {
        private bool _isTemplateApplied;

        private ScrollViewer PART_ScrollViewer = null!;
        private ScrollViewer PART_ClipScrollViewer = null!;
        private Border PART_InnerCanvas = null!;
        private Grid PART_ItemsGrid = null!;

        private List<List<StyledElement?>> renderedControls = new();

        public static readonly StyledProperty<double> ItemHeightProperty =
            AvaloniaProperty.Register<VirtualizedGrid, double>(nameof(ItemHeight), 64);

        public static readonly StyledProperty<double> ItemWidthProperty =
            AvaloniaProperty.Register<VirtualizedGrid, double>(nameof(ItemWidth), 64);

        public static readonly StyledProperty<IList> ItemsProperty =
            AvaloniaProperty.Register<VirtualizedGrid, IList>(nameof(Items));

        public static readonly StyledProperty<int> MaxItemsInRowProperty =
            AvaloniaProperty.Register<VirtualizedGrid, int>(nameof(MaxItemsInRow));

        public static readonly StyledProperty<IDataTemplate> ItemTemplateProperty =
            AvaloniaProperty.Register<VirtualizedGrid, IDataTemplate>(nameof(ItemTemplate));

        public static readonly StyledProperty<bool> DisableSmoothScrollingProperty =
            AvaloniaProperty.Register<VirtualizedGrid, bool>(nameof(DisableSmoothScrolling));

        public double ItemHeight
        {
            get => GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public double ItemWidth
        {
            get => GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        public IList Items
        {
            get => GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public int MaxItemsInRow
        {
            get => GetValue(MaxItemsInRowProperty);
            set => SetValue(MaxItemsInRowProperty, value);
        }

        public IDataTemplate ItemTemplate
        {
            get => GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public bool DisableSmoothScrolling
        {
            get => GetValue(DisableSmoothScrollingProperty);
            set => SetValue(DisableSmoothScrollingProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            PART_ScrollViewer = e.NameScope.Find<ScrollViewer>(nameof(PART_ScrollViewer)) ??
                throw new NullReferenceException(nameof(PART_ScrollViewer));
            PART_ClipScrollViewer = e.NameScope.Find<ScrollViewer>(nameof(PART_ClipScrollViewer)) ??
                throw new NullReferenceException(nameof(PART_ClipScrollViewer));
            PART_InnerCanvas = e.NameScope.Find<Border>(nameof(PART_InnerCanvas)) ??
                throw new NullReferenceException(nameof(PART_InnerCanvas));
            PART_ItemsGrid = e.NameScope.Find<Grid>(nameof(PART_ItemsGrid)) ??
                throw new NullReferenceException(nameof(PART_ItemsGrid));

            PART_ScrollViewer.ScrollChanged += ScrollChanged;
            LayoutUpdated += (s, e) => UpdateState();

            _isTemplateApplied = true;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property.Name == nameof(Items))
            {
                UpdateState();

                if (Items is not null)
                {
                    if (Items is INotifyCollectionChanged observableCollection)
                    {
                        observableCollection.CollectionChanged += (s, e) =>
                        {
                            UpdateState();
                        };
                    }
                }
            }

            if (change.Property.Name == nameof(MaxItemsInRow) ||
                change.Property.Name == nameof(ItemHeight) ||
                change.Property.Name == nameof(ItemWidth))
            {
                UpdateState();
            }
        }

        private void ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (!DisableSmoothScrolling)
            {
                double xOffset = PART_ScrollViewer.Offset.X % ItemWidth;
                double yOffset = PART_ScrollViewer.Offset.Y % ItemHeight;

                PART_ClipScrollViewer.Offset = PART_ClipScrollViewer.Offset
                    .WithX(xOffset)
                    .WithY(yOffset);
            }

            UpdatViewportDataContext();
        }

        DateTimeOffset _updateTime = DateTimeOffset.MinValue;
        Task? _updateTask = null;

        /// <summary>
        /// Updates rendered items, theirs data context and scroll bars to reflect current state.
        /// </summary>
        private void UpdateState()
        {
            if (_isTemplateApplied)
            {
                ScheduleOrPostponeUpdate();
            }
        }

        private void ScheduleOrPostponeUpdate()
        {
            if (_updateTask is null)
            {
                // Schedule
                _updateTime = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(500);
                _updateTask = Task.Run(async () => 
                {
                    bool stop = false;
                    while(!stop)
                    {
                        await Task.Delay(50);
                        if (DateTimeOffset.UtcNow > _updateTime)
                        {
                            await Dispatcher.UIThread.Invoke(async () => 
                            {
                                await UpdateGrid();
                                UpdateScrollViewerBoundaries();
                                UpdatViewportDataContext();
                                _updateTask = null;
                                stop = true;
                            }, DispatcherPriority.Background);
                        }
                    }
                });
            }
            else
            {
                // Postpone
                _updateTime = DateTimeOffset.UtcNow.AddMilliseconds(500);
            }
        }

        /// <summary>
        /// Ensures that number of rendered controls is matching the current state.
        /// </summary>
        private async Task UpdateGrid()
        {
            await UpdateColumns();
            await UpdateRows();
        }

        private void UpdateScrollViewerBoundaries()
        {
            PART_InnerCanvas.Width = GetItemsNumberX() * ItemWidth;
            PART_InnerCanvas.Height = GetItemsNumberY() * ItemHeight;
        }

        /// <summary>
        /// Assings proper DataContext to rendered <see cref="IControl"/>s objects.
        /// </summary>
        private void UpdatViewportDataContext()
        {
            //LoopThrough(renderedControls, (control, x, y) =>
            //{
            //    if (control is not null)
            //    {
            //        int itemCoordX = ResolveHorizontalItemsOffset() + x;
            //        int itemCoordY = ResolveVerticalItemOffset() + y;

            //        // At the very end of scrolling we're exceeding by one (because of Smooth scrolling)
            //        // Here we ignore this one additional item
            //        if (itemCoordX >= GetItemsNumberX() || itemCoordY >= GetItemsNumberY())
            //        {
            //            return;
            //        }

            //        control.DataContext = GetItem(itemCoordX, itemCoordY);
            //    }
            //});
        }

        /// <summary>
        /// Calculates how many items from the left edge is current viewport (area with rendered <see cref="IControl"/>s). 
        /// </summary>
        private int ResolveHorizontalItemsOffset()
        {
            return (int)(PART_ScrollViewer.Offset.X / ItemWidth);
        }

        /// <summary>
        /// Calculates how many items from the top edge is current viewport (area with rendered <see cref="IControl"/>s). 
        /// </summary>
        private int ResolveVerticalItemOffset()
        {
            return (int)(PART_ScrollViewer.Offset.Y / ItemHeight);
        }

        /// <summary>
        /// Calculates how many <see cref="IControl"/>s should be rendered horizontaly.
        /// </summary>
        private int ResolveMaxHorizontalVisibleItems()
        {
            // How many items can fit in current viewport?
            int itemsCount = (int)Math.Ceiling(PART_ClipScrollViewer.Bounds.Width / ItemWidth);

            // Exceed viewport by one to enable smooth scrolling
            if (!DisableSmoothScrolling)
            {
                itemsCount++;
            }

            // If we can render more than we'd like to actually display we'll render only what is required
            if (itemsCount > GetItemsNumberX())
            {
                itemsCount = GetItemsNumberX();
            }

            return itemsCount;
        }

        /// <summary>
        /// Calculates how many <see cref="IControl"/>s should be rendered verticaly.
        /// </summary
        private int ResolveMaxVerticalVisibleItems()
        {
            // How many items can fit in current viewport?
            int itemsCount = (int)Math.Ceiling(PART_ClipScrollViewer.Bounds.Height / ItemHeight);

            // Exceed viewport by one to enable smooth scrolling
            if (!DisableSmoothScrolling)
            {
                itemsCount++;
            }

            // If we can render more than we'd like to actually display we'll render only what is required
            if (itemsCount > GetItemsNumberY())
            {
                itemsCount = GetItemsNumberY();
            }

            return itemsCount;
        }

        private async Task UpdateColumns()
        {
            int currentColumnsNumber = PART_ItemsGrid.ColumnDefinitions.Count;
            int expectedColumnsNumber = ResolveMaxHorizontalVisibleItems();

            UpdateColumnsWidth();

            if (currentColumnsNumber < expectedColumnsNumber)
            {
                await AddColumns(expectedColumnsNumber - currentColumnsNumber);
            }

            if (currentColumnsNumber > expectedColumnsNumber)
            {
                RemoveColumns(currentColumnsNumber - expectedColumnsNumber);
            }
        }

        private async Task UpdateRows()
        {
            int currentRowsNumber = PART_ItemsGrid.RowDefinitions.Count;
            int expectedRowsNumber = ResolveMaxVerticalVisibleItems();

            UpdateRowsHeight();

            if (currentRowsNumber < expectedRowsNumber)
            {
                await AddRows(expectedRowsNumber - currentRowsNumber);
            }

            if (currentRowsNumber > expectedRowsNumber)
            {
                RemoveRows(currentRowsNumber - expectedRowsNumber);
            }
        }

        /// <summary>
        /// Updates width of all available columns to reflect <see cref="ItemWidth"/> value.
        /// </summary>
        private void UpdateColumnsWidth()
        {
            foreach (ColumnDefinition columnDefinition in PART_ItemsGrid.ColumnDefinitions)
            {
                columnDefinition.Width = new GridLength(ItemWidth, GridUnitType.Pixel);
            }
        }

        /// <summary>
        /// Updates htight of all available rows to reflect <see cref="ItemHeight"/> value.
        /// </summary>
        private void UpdateRowsHeight()
        {
            foreach (RowDefinition rowDefinition in PART_ItemsGrid.RowDefinitions)
            {
                rowDefinition.Height = new GridLength(ItemHeight, GridUnitType.Pixel);
            }
        }

        private async Task AddColumns(int numberToAdd)
        {
            int currentRowsNumber = PART_ItemsGrid.RowDefinitions.Count;
            int currentColumnsNumber = PART_ItemsGrid.ColumnDefinitions.Count;

            for (int i = 0; i < numberToAdd; i++)
            {
                PART_ItemsGrid.ColumnDefinitions.Add(new ColumnDefinition(ItemWidth, GridUnitType.Pixel));
                //if (i % 5 == 0) await Task.Yield();
                for (int j = 0; j < currentRowsNumber; j++)
                {
                    // Render controls for newly created column
                    CreateItem(currentColumnsNumber + i, j);
                }
            }
        }

        private async Task AddRows(int numberToAdd)
        {
            int currentRowsNumber = PART_ItemsGrid.RowDefinitions.Count;
            int currentColumnsNumber = PART_ItemsGrid.ColumnDefinitions.Count;

            for (int i = 0; i < numberToAdd; i++)
            {
                PART_ItemsGrid.RowDefinitions.Add(new RowDefinition(ItemHeight, GridUnitType.Pixel));
                //if (i % 5 == 0)
                await Task.Yield();
                for (int j = 0; j < currentColumnsNumber; j++)
                {
                    // Render controls for newly created row
                    CreateItem(j, currentRowsNumber + i);
                }
            }
        }

        private void RemoveColumns(int numberToRemove)
        {
            int currentRowsNumber = PART_ItemsGrid.RowDefinitions.Count;
            int currentColumnsNumber = PART_ItemsGrid.ColumnDefinitions.Count;
            int expectedColumnsNumber = currentColumnsNumber - numberToRemove;

            for (int i = 0; i < numberToRemove; i++)
            {
                for (int j = 0; j < currentRowsNumber; j++)
                {
                    PutItem(renderedControls, expectedColumnsNumber + i, j, default);
                }
            }

            List<Control> childrenToRemove = PART_ItemsGrid.Children.Where(c => c.GetValue(Grid.ColumnProperty) >= expectedColumnsNumber).ToList();
            PART_ItemsGrid.Children.RemoveAll(childrenToRemove);

            PART_ItemsGrid.ColumnDefinitions.RemoveRange(expectedColumnsNumber, numberToRemove);
        }

        private void RemoveRows(int numberToRemove)
        {
            int currentColumnsNumber = PART_ItemsGrid.ColumnDefinitions.Count;
            int currentRowsNumber = PART_ItemsGrid.RowDefinitions.Count;
            int expectedRowsNumber = currentRowsNumber - numberToRemove;

            for (int i = 0; i < numberToRemove; i++)
            {
                for (int j = 0; j < currentColumnsNumber; j++)
                {
                    PutItem(renderedControls, j, expectedRowsNumber + i, default);
                }
            }

            List<Control> childrenToRemove = PART_ItemsGrid.Children.Where(c => c.GetValue(Grid.RowProperty) >= expectedRowsNumber).ToList();
            PART_ItemsGrid.Children.RemoveAll(childrenToRemove);

            PART_ItemsGrid.RowDefinitions.RemoveRange(expectedRowsNumber, numberToRemove);
        }

        private Random _rnd = new();

        private void CreateItem(int x, int y)
        {
            ContentPresenter newItem = new();

            newItem.SetValue(Grid.ColumnProperty, x);
            newItem.SetValue(Grid.RowProperty, y);
            newItem.SetValue(ContentPresenter.ContentTemplateProperty, ItemTemplate);
            //if (_rnd.Next(100) < 90)
            //{
            //    newItem.SetValue(IsVisibleProperty, false);
            //}

            newItem.PointerWheelChanged += (s, e) =>
            {
                e.Handled = true;
                HandleOffsetWhenScrollOnItem(e.Delta);
            };

            PutItem(renderedControls, x, y, newItem);
            PART_ItemsGrid.Children.Add(newItem);
        }

        /// <summary>
        /// Updates the offset of main ScrollViewer using input data from clip ScrollViewer.
        /// </summary>
        /// <param name="delta"></param>
        private void HandleOffsetWhenScrollOnItem(Vector delta)
        {
            // TODO
            PART_ScrollViewer.Offset -= delta * 25;
        }

        private static void LoopThrough<T>(List<List<T>> nestedList, Action<T, int, int> action)
        {
            for (int y = 0; y < nestedList.Count; y++)
            {
                for (int x = 0; x < nestedList[y].Count; x++)
                {
                    action.Invoke(nestedList[y][x], x, y);
                }
            }
        }

        private static void LoopThroughAsTwoDiemension<T>(IList<T> list, int width, Action<T, int, int> action)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int y = i / width;
                int x = i % width;
                T item = list[i];

                action.Invoke(item, x, y);
            }
        }

        private static void PutItem<T>(List<List<T?>> nestedList, int x, int y, T item)
        {
            if (nestedList.Count > y)
            {
                if (nestedList[y].Count > x)
                {
                    nestedList[y][x] = item;
                }
                else
                {
                    nestedList[y].AddRange(Enumerable.Repeat(default(T), x - nestedList[y].Count + 1));
                    nestedList[y][x] = item;
                }
            }
            else
            {
                nestedList.AddRange(Enumerable.Repeat(new List<T?>(), y - nestedList.Count + 1));
                nestedList[y].AddRange(Enumerable.Repeat(default(T), x - nestedList[y].Count + 1));
                nestedList[y][x] = item;
            }
        }

        private object? GetItem(int x, int y)
        {
            int index = y * GetItemsNumberX() + x;
            if (index >= Items.Count)
            {
                return null;
            }

            return Items[index];
        }

        private int GetItemsNumberX() => Items.Count < MaxItemsInRow ? Items.Count : MaxItemsInRow;

        private int GetItemsNumberY() => (int)Math.Ceiling((double)Items.Count / MaxItemsInRow);
    }
}
