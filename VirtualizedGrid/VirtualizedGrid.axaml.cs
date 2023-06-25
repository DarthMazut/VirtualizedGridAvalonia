using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace VirtualizedGrid
{
    public class VirtualizedGrid : TemplatedControl
    {
        private bool _isTemplateApplied;
        private DateTimeOffset _lastUpdateTimestamp;

        private ScrollViewer PART_ScrollViewer = null!;
        private ScrollViewer PART_ClipScrollViewer = null!;
        private Border PART_InnerCanvas = null!;
        private ItemsControl PART_ItemsControl = null!;

        private List<StyledElement?> _renderedControls = new();

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
            PART_ItemsControl = e.NameScope.Find<ItemsControl>(nameof(PART_ItemsControl)) ??
                throw new NullReferenceException(nameof(PART_ItemsControl));

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

        /// <summary>
        /// Updates rendered items, theirs data context and scroll bars to reflect current state.
        /// </summary>
        private void UpdateState()
        {
            if (_isTemplateApplied)
            {
                UpdateItemsControl();
                UpdateScrollViewerBoundaries();
                UpdatViewportDataContext();
            }
        }

        /// <summary>
        /// Update state of ItemsControl.
        /// </summary>
        private void UpdateItemsControl()
        {
            UpdateItemsControlItems();
            UpdateItemsControlProperties();
        }

        /// <summary>
        /// Ensures that number of rendered controls is matching the current state.
        /// </summary>
        private void UpdateItemsControlItems()
        {
            SimpleGrid panel = (PART_ItemsControl?.ItemsPanelRoot as SimpleGrid)!;

            int numberOfColumnsToRender = ResolveMaxHorizontalVisibleItems();
            int numberOfRowsToRender = ResolveMaxVerticalVisibleItems();
            int currentColumnNumber = panel.MaxElementsInRow;
            int currentRowNumber = (int)Math.Ceiling((double)panel.Children.Count / panel.MaxElementsInRow);

            if (currentRowNumber < 0) return;

            if (currentColumnNumber < numberOfColumnsToRender)
            {
                int numberOfColumnsToAdd = numberOfColumnsToRender - currentColumnNumber;
                for (int row = currentRowNumber; row > 0; row--)
                {
                    for (int i = 0; i < numberOfColumnsToAdd; i++)
                    {
                        //Width * rowNumber - 1
                        StyledElement newItem = CreateItem();
                        PART_ItemsControl.Items.Insert(currentColumnNumber * row - 1 + i, newItem);
                        _renderedControls.Insert(currentColumnNumber * row - 1 + i, newItem);
                    }
                }
            }

            if (currentColumnNumber > numberOfColumnsToRender)
            {
                int numberOfColumnsToRemove = currentColumnNumber - numberOfColumnsToRender;
                for (int row = currentRowNumber; row > 0; row--)
                {
                    for (int i = 0; i < numberOfColumnsToRemove; i++)
                    {
                        //Width * rowNumber - 1
                        PART_ItemsControl.Items.RemoveAt(currentColumnNumber * row - 1 - i);
                        _renderedControls.RemoveAt(currentColumnNumber * row - 1 - i);
                    }
                }
            }

            if (currentRowNumber < numberOfRowsToRender)
            {
                int numberOfRowsToAdd = numberOfRowsToRender - currentRowNumber;
                for (int row = 0; row < numberOfRowsToAdd; row++)
                {
                    for (int i = 0; i < numberOfColumnsToRender; i++)
                    {
                        StyledElement newItem = CreateItem();
                        PART_ItemsControl.Items.Add(newItem);
                        _renderedControls.Add(newItem);
                    }
                }
            }

            if (currentRowNumber > numberOfRowsToRender)
            {
                int numberOfRowsToRemove = currentRowNumber - numberOfRowsToRender;
                for (int row = 0; row < numberOfRowsToRemove; row++)
                {
                    for (int i = 0; i < numberOfColumnsToRender; i++)
                    {
                        PART_ItemsControl.Items.RemoveAt(PART_ItemsControl.ItemCount - 1);
                        _renderedControls.RemoveAt(_renderedControls.Count - 1);
                    }
                }
            }
        }

        /// <summary>
        /// Updates dimension properties of panel which contains rendered items.
        /// </summary>
        private void UpdateItemsControlProperties()
        {
            SimpleGrid? simpleGridPanel = PART_ItemsControl?.ItemsPanelRoot as SimpleGrid;
            if (simpleGridPanel != null)
            {
                if (simpleGridPanel.MaxElementsInRow != ResolveMaxHorizontalVisibleItems())
                {
                    simpleGridPanel.MaxElementsInRow = ResolveMaxHorizontalVisibleItems();
                }

                bool shouldUpdateControls = false;

                if (simpleGridPanel.ItemHeight != ItemHeight)
                {
                    simpleGridPanel.ItemHeight = ItemHeight;
                    shouldUpdateControls = true;
                }

                if (simpleGridPanel.ItemWidth != ItemWidth)
                {
                    simpleGridPanel.ItemWidth = ItemWidth;
                    shouldUpdateControls = true;
                }

                if (shouldUpdateControls)
                {
                    foreach (StyledElement? control in _renderedControls)
                    {
                        control?.SetValue(HeightProperty, ItemHeight);
                        control?.SetValue(WidthProperty, ItemWidth);
                    }
                }
            }
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
            //if (DateTimeOffset.UtcNow - _lastUpdateTimestamp < TimeSpan.FromMilliseconds(1000))
            //{
            //    return;
            //}

            int width = ResolveMaxHorizontalVisibleItems();
            for (int i = 0; i < _renderedControls.Count; i++)
            {
                int y = i / width;
                int x = i % width;
                StyledElement? control = _renderedControls[i];

                if (control is not null)
                {
                    int itemCoordX = ResolveHorizontalItemsOffset() + x;
                    int itemCoordY = ResolveVerticalItemOffset() + y;

                    // At the very end of scrolling we're exceeding by one (because of Smooth scrolling)
                    // Here we ignore this one additional item
                    if (itemCoordX >= GetItemsNumberX() || itemCoordY >= GetItemsNumberY())
                    {
                        continue;
                    }

                    control.DataContext = GetItem(itemCoordX, itemCoordY);
                }
            }

            //_lastUpdateTimestamp = DateTimeOffset.UtcNow;
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

        private StyledElement CreateItem()
        {
            ContentPresenter newItem = new();
            newItem.SetValue(ContentPresenter.ContentTemplateProperty, ItemTemplate);
            newItem.SetValue(ContentPresenter.HeightProperty, ItemHeight);
            newItem.SetValue(ContentPresenter.WidthProperty, ItemWidth);

            newItem.PointerWheelChanged += (s, e) =>
            {
                e.Handled = true;
                HandleOffsetWhenScrollOnItem(e.Delta);
            };

            return newItem;
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
