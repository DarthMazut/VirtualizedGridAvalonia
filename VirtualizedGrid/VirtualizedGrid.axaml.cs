using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
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

        public static readonly StyledProperty<int> MaxItemsCacheProperty =
            AvaloniaProperty.Register<VirtualizedGrid, int>(nameof(MaxItemsCache), defaultValue: 100000);

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

        public int MaxItemsCache
        {
            get => GetValue(MaxItemsCacheProperty);
            set => SetValue(MaxItemsCacheProperty, value);
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
                UpdateRowsHeight();
                UpdateColumnsWidth();
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

        /// <summary>
        /// Updates rendered items, theirs data context and scroll bars to reflect current state.
        /// </summary>
        private void UpdateState()
        {
            if (_isTemplateApplied)
            {
                UpdateGrid();
                UpdateScrollViewerBoundaries();
                UpdatViewportDataContext();
            }
        }

        /// <summary>
        /// Ensures that number of rendered controls is matching the current state.
        /// </summary>
        private void UpdateGrid()
        {
            UpdateColumns();
            UpdateRows();
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
            LoopThrough(renderedControls, (control, x, y) =>
            {
                if (control is not null)
                {
                    int itemCoordX = ResolveHorizontalItemsOffset() + x;
                    int itemCoordY = ResolveVerticalItemOffset() + y;

                    // At the very end of scrolling we're exceeding by one (because of Smooth scrolling)
                    // Here we ignore this one additional item
                    if (itemCoordX >= GetItemsNumberX() || itemCoordY >= GetItemsNumberY())
                    {
                        return;
                    }

                    control.DataContext = GetItem(itemCoordX, itemCoordY);
                }
            });
        }

        private void UpdateColumns()
        {
            int currentColumnsNumber = PART_ItemsGrid.ColumnDefinitions.Count;
            int expectedColumnsNumber = ResolveMaxHorizontalVisibleItems();

            if (currentColumnsNumber < expectedColumnsNumber)
            {
                AddColumns(expectedColumnsNumber - currentColumnsNumber);
            }

            if (currentColumnsNumber > expectedColumnsNumber)
            {
                RemoveColumns();
            }
        }

        private void UpdateRows()
        {
            int currentRowsNumber = PART_ItemsGrid.RowDefinitions.Count;
            int expectedRowsNumber = ResolveMaxVerticalVisibleItems();

            if (currentRowsNumber < expectedRowsNumber)
            {
                AddRows(expectedRowsNumber - currentRowsNumber);
            }

            if (currentRowsNumber > expectedRowsNumber)
            {
                RemoveRows();
            }
        }

        /// <summary>
        /// Updates width of all available columns to reflect <see cref="ItemWidth"/> value.
        /// </summary>
        private void UpdateColumnsWidth()
        {
            if(_isTemplateApplied)
            {
                foreach (ColumnDefinition columnDefinition in PART_ItemsGrid.ColumnDefinitions)
                {
                    columnDefinition.Width = new GridLength(ItemWidth, GridUnitType.Pixel);
                }
            }
        }

        /// <summary>
        /// Updates htight of all available rows to reflect <see cref="ItemHeight"/> value.
        /// </summary>
        private void UpdateRowsHeight()
        {
            if (_isTemplateApplied)
            {
                foreach (RowDefinition rowDefinition in PART_ItemsGrid.RowDefinitions)
                {
                    rowDefinition.Height = new GridLength(ItemHeight, GridUnitType.Pixel);
                }
            }
        }

        private void AddColumns(int numberToAdd)
        {
            int currentRowsNumber = PART_ItemsGrid.RowDefinitions.Count;
            int currentColumnsNumber = PART_ItemsGrid.ColumnDefinitions.Count;

            for (int i = 0; i < numberToAdd; i++)
            {
                PART_ItemsGrid.ColumnDefinitions.Add(new ColumnDefinition(ItemWidth, GridUnitType.Pixel));
                for (int j = 0; j < currentRowsNumber; j++)
                {
                    // Render controls for newly created column
                    CreateItem(currentColumnsNumber + i, j);
                }
            }
        }

        private void AddRows(int numberToAdd)
        {
            int currentRowsNumber = PART_ItemsGrid.RowDefinitions.Count;
            int currentColumnsNumber = PART_ItemsGrid.ColumnDefinitions.Count;

            for (int i = 0; i < numberToAdd; i++)
            {
                PART_ItemsGrid.RowDefinitions.Add(new RowDefinition(ItemHeight, GridUnitType.Pixel));
                for (int j = 0; j < currentColumnsNumber; j++)
                {
                    // Render controls for newly created row
                    CreateItem(j, currentRowsNumber + i);
                }
            }
        }

        // JE�ELI expected < viewPort TO ignoruj cache
        // JE�ELI current > expected + cache TO usu� kolumn�

        // ColumnsToRemove = (CurrentItems - ExpectedItems - Cache)/H
        // ColumnsToRemove = (CurrentColumns * H - expectedColumns * H - Cache) / H

        private void RemoveColumns()
        {
            int currentItemsNumber = PART_ItemsGrid.Children.Count;
            int currentColumnsNumber = PART_ItemsGrid.ColumnDefinitions.Count;
            int currentRowsNumber = PART_ItemsGrid.RowDefinitions.Count;
            int expectedColumnsNumber = ResolveMaxHorizontalVisibleItems();
            int cache = MaxItemsCache;

            if (expectedColumnsNumber < GetMaxItemsInCurrentViewportX())
            {
                cache = 0;
            }

            int columnsToRemove = (int)Math.Ceiling((double)(currentItemsNumber - expectedColumnsNumber * currentRowsNumber - cache) / currentRowsNumber);
            if (columnsToRemove <= 0)
            {
                return;
            }

            int finalColumnsNumber = currentColumnsNumber - columnsToRemove;

            for (int i = 0; i < columnsToRemove; i++)
            {
                for (int j = 0; j < currentRowsNumber; j++)
                {
                    PutItem(renderedControls, finalColumnsNumber + i, j, default);
                }
            }

            List<Control> childrenToRemove = PART_ItemsGrid.Children.Where(c => c.GetValue(Grid.ColumnProperty) >= finalColumnsNumber).ToList();
            PART_ItemsGrid.Children.RemoveAll(childrenToRemove);

            PART_ItemsGrid.ColumnDefinitions.RemoveRange(finalColumnsNumber, columnsToRemove);
        }

        // JE�ELI expected < viewPort TO ignoruj cache
        // JE�ELI current > expected + cache TO usu� kolumn�

        // RowsToRemove = (CurrentItems - ExpectedItems - Cache)/W
        // RowsToRemove = (CurrentRows * W - expectedRows * W - Cache) / W

        private void RemoveRows()
        {
            int currentItemsNumber = PART_ItemsGrid.Children.Count;
            int currentColumnsNumber = PART_ItemsGrid.ColumnDefinitions.Count;
            int currentRowsNumber = PART_ItemsGrid.RowDefinitions.Count;
            int expectedRowsNumber = ResolveMaxVerticalVisibleItems();
            int expectedColumnsNumber = ResolveMaxHorizontalVisibleItems();
            int cache = MaxItemsCache - (currentColumnsNumber - expectedColumnsNumber) * currentRowsNumber;

            if (expectedRowsNumber < GetMaxItemsInCurrentViewportY())
            {
                cache = 0;
            }

            int rowsToRemove = (int)Math.Ceiling((double)(currentItemsNumber - expectedRowsNumber * currentColumnsNumber - cache) / currentColumnsNumber);
            if (rowsToRemove <= 0)
            {
                return;
            }

            int finalRowsNumber = currentRowsNumber - rowsToRemove;

            for (int i = 0; i < rowsToRemove; i++)
            {
                for (int j = 0; j < currentColumnsNumber; j++)
                {
                    PutItem(renderedControls, j, finalRowsNumber + i, default);
                }
            }

            List<Control> childrenToRemove = PART_ItemsGrid.Children.Where(c => c.GetValue(Grid.RowProperty) >= finalRowsNumber).ToList();
            PART_ItemsGrid.Children.RemoveAll(childrenToRemove);

            PART_ItemsGrid.RowDefinitions.RemoveRange(finalRowsNumber, rowsToRemove);
        }

        private void CreateItem(int x, int y)
        {
            ContentPresenter newItem = new();

            newItem.SetValue(Grid.ColumnProperty, x);
            newItem.SetValue(Grid.RowProperty, y);
            newItem.SetValue(ContentPresenter.ContentTemplateProperty, ItemTemplate);

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

        /// <summary>
        /// Gets item with specific coords from <see cref="Items"/> collection.
        /// </summary>
        private object? GetItem(int x, int y)
        {
            int index = y * GetItemsNumberX() + x;
            if (index >= Items.Count)
            {
                return null;
            }

            return Items[index];
        }

        /// <summary>
        /// How many elements can fit in current viewport horizontaly?
        /// </summary>
        private int GetMaxItemsInCurrentViewportX()
            => (int)Math.Ceiling(PART_ClipScrollViewer.Bounds.Width / ItemWidth);

        /// <summary>
        /// How many elements can fit in current viewport verticaly?
        /// </summary>
        private int GetMaxItemsInCurrentViewportY()
            => (int)Math.Ceiling(PART_ClipScrollViewer.Bounds.Height / ItemHeight);

        /// <summary>
        /// Returns number of elements in row for <see cref="Items"/> collection treated as 2D array.
        /// </summary>
        private int GetItemsNumberX() => Items.Count < MaxItemsInRow ? Items.Count : MaxItemsInRow;

        /// <summary>
        /// Returns number of elements in column for <see cref="Items"/> collection treated as 2D array.
        /// </summary>
        private int GetItemsNumberY() => (int)Math.Ceiling((double)Items.Count / MaxItemsInRow);

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
            int itemsCount = GetMaxItemsInCurrentViewportX();

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
            int itemsCount = GetMaxItemsInCurrentViewportY();

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
    }
}
