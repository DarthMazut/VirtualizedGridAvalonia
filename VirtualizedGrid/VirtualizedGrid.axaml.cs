using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
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
        private Canvas PART_ItemsCanvas = null!;

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
            PART_ItemsCanvas = e.NameScope.Find<Canvas>(nameof(PART_ItemsCanvas)) ??
                throw new NullReferenceException(nameof(PART_ItemsCanvas));

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
                UpdateItemsDimensions();
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
            PART_ItemsCanvas.Width = GetNumberOfRenderedItemsHorizontally() * ItemWidth;
            PART_ItemsCanvas.Height = GetNumberOfRenderedItemsVertically() * ItemHeight;
        }

        private void UpdateColumns()
        {
            int currentColumnsNumber = GetNumberOfRenderedItemsHorizontally();
            int expectedColumnsNumber = ResolveMaxHorizontalVisibleItems();

            if (currentColumnsNumber < expectedColumnsNumber)
            {
                AddColumns(expectedColumnsNumber - currentColumnsNumber);
            }

            if (currentColumnsNumber > expectedColumnsNumber)
            {
                RemoveColumns(currentColumnsNumber - expectedColumnsNumber);
            }
        }

        private void UpdateRows()
        {
            int currentRowsNumber = GetNumberOfRenderedItemsVertically();
            int expectedRowsNumber = ResolveMaxVerticalVisibleItems();

            if (currentRowsNumber < expectedRowsNumber)
            {
                AddRows(expectedRowsNumber - currentRowsNumber);
            }

            if (currentRowsNumber > expectedRowsNumber)
            {
                RemoveRows(currentRowsNumber - expectedRowsNumber);
            }
        }

        /// <summary>
        /// Updates width and height of all rendered items.
        /// </summary>
        private void UpdateItemsDimensions()
        {
            LoopThrough(renderedControls, (item, x, y) =>
            {
                item?.SetValue(HeightProperty, ItemHeight);
                item?.SetValue(WidthProperty, ItemWidth);
                item?.SetValue(Canvas.LeftProperty, x * ItemWidth);
                item?.SetValue(Canvas.TopProperty, y * ItemHeight);
            });
        }

        private void AddColumns(int numberToAdd)
        {
            int currentRowsNumber = GetNumberOfRenderedItemsVertically();
            int currentColumnsNumber = GetNumberOfRenderedItemsHorizontally();
            
            //DEBUG
            if (currentRowsNumber == 0) currentRowsNumber = 1;
            //DEBUG

            for (int i = 0; i < numberToAdd; i++)
            {
                for (int j = 0; j < currentRowsNumber; j++)
                {
                    // Render controls for newly created column
                    CreateItem(currentColumnsNumber + i, j);
                }
            }
        }

        private void AddRows(int numberToAdd)
        {
            int currentRowsNumber = GetNumberOfRenderedItemsVertically();
            int currentColumnsNumber = GetNumberOfRenderedItemsHorizontally();

            for (int i = 0; i < numberToAdd; i++)
            {
                for (int j = 0; j < currentColumnsNumber; j++)
                {
                    // Render controls for newly created row
                    CreateItem(j, currentRowsNumber + i);
                }
            }
        }

        private void RemoveColumns(int numberToRemove)
        {
            int currentRowsNumber = GetNumberOfRenderedItemsVertically();
            int currentColumnsNumber = GetNumberOfRenderedItemsHorizontally();
            int expectedColumnsNumber = currentColumnsNumber - numberToRemove;

            for (int i = 0; i < numberToRemove; i++)
            {
                for (int j = 0; j < currentRowsNumber; j++)
                {
                    PutItem(renderedControls, expectedColumnsNumber + i, j, default);
                }
            }

            List<Control> childrenToRemove = PART_ItemsCanvas.Children.Where(c => c.GetValue(Canvas.LeftProperty) >= expectedColumnsNumber * ItemWidth).ToList();
            PART_ItemsCanvas.Children.RemoveAll(childrenToRemove);
        }

        private void RemoveRows(int numberToRemove)
        {
            int currentColumnsNumber = GetNumberOfRenderedItemsHorizontally();
            int currentRowsNumber = GetNumberOfRenderedItemsVertically();
            int expectedRowsNumber = currentRowsNumber - numberToRemove;

            for (int i = 0; i < numberToRemove; i++)
            {
                for (int j = 0; j < currentColumnsNumber; j++)
                {
                    PutItem(renderedControls, j, expectedRowsNumber + i, default);
                }
            }

            List<Control> childrenToRemove = PART_ItemsCanvas.Children.Where(c => c.GetValue(Canvas.TopProperty) >= expectedRowsNumber * ItemHeight).ToList();
            PART_ItemsCanvas.Children.RemoveAll(childrenToRemove);
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

        private void CreateItem(int x, int y)
        {
            ContentPresenter newItem = new();

            newItem.SetValue(Canvas.LeftProperty, x * ItemWidth);
            newItem.SetValue(Canvas.TopProperty, y * ItemHeight);
            newItem.SetValue(WidthProperty, ItemWidth);
            newItem.SetValue(HeightProperty, ItemHeight);
            newItem.SetValue(ContentPresenter.ContentTemplateProperty, ItemTemplate);

            newItem.PointerWheelChanged += (s, e) =>
            {
                e.Handled = true;
                HandleOffsetWhenScrollOnItem(e.Delta);
            };

            PutItem(renderedControls, x, y, newItem);
            PART_ItemsCanvas.Children.Add(newItem);
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

        private int GetNumberOfRenderedItemsHorizontally()
            => renderedControls.FirstOrDefault()?.Count(i => i is not null) ?? 0;

        private int GetNumberOfRenderedItemsVertically()
            => renderedControls.Count(row => row.FirstOrDefault() is not null);

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


        /// <summary>
        /// Returns number of elements in row for <see cref="Items"/> collection treated as 2D array.
        /// </summary>
        private int GetItemsNumberX() => Items.Count < MaxItemsInRow ? Items.Count : MaxItemsInRow;

        /// <summary>
        /// Returns number of elements in column for <see cref="Items"/> collection treated as 2D array.
        /// </summary>
        private int GetItemsNumberY() => (int)Math.Ceiling((double)Items.Count / MaxItemsInRow);
    }
}
