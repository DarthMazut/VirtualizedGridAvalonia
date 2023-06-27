using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualizedGrid
{
    /// <summary>
    /// Provides wrapper that allows to interpret single dimension list as two dimension array.
    /// </summary>
    /// <typeparam name="T">Type of elements in the wrapping list.</typeparam>
    internal class List2D<T> : IList<T>
    {
        private readonly IList<T> _internalList;
        
        private int _constraint;

        public List2D()
            : this(new List<T>(), 1) { }

        public List2D(int constraintValue)
            : this(new List<T>(), constraintValue) { }

        public List2D(IList<T> wrappedList, int constraint)
        {
            _internalList = wrappedList;
            Constraint = constraint;
        }

        /// <summary>
        /// Returns list that is wrapped by this instance.
        /// </summary>
        public IList<T> CoreList => _internalList;

        public int Width => Math.Min(_internalList.Count, Constraint);

        public int Height => (int)Math.Ceiling((double)Count / Width);

        public int Constraint 
        {
            get => _constraint;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Constraint must be greather than 0", nameof(Constraint));
                }

                _constraint = value;
            }
        }

        public T this[int index] 
        { 
            get => _internalList[index];
            set => _internalList[index] = value;
        }

        public T this[int x, int y]
        {
            get => _internalList[CoordsToIndex(x, y)];
            set => _internalList[CoordsToIndex(x, y)] = value;
        }

        public int Count => _internalList.Count;

        public bool IsReadOnly => _internalList.IsReadOnly;

        public void Add(T item) => _internalList.Add(item);

        public void AddColumns(int numberOfColumnsToAdd, Func<Point, int, T> elementBuilder)
        {
            FillGapWithBuilder(elementBuilder);

            for (int row = Height; row > 0; row--)
            {
                for (int i = 0; i < numberOfColumnsToAdd; i++)
                {
                    int index = row * Constraint - 1 + i;
                    Point coords = IndexToCoords(index);
                    _internalList.Insert(index, elementBuilder.Invoke(coords, index));
                }
            }

            Constraint += numberOfColumnsToAdd;
        }

        public T[] RemoveColumns(int numberOfColumnsToRemove)
        {
            List<T> removedItems = new();

            for (int row = Height; row > 0; row--)
            {
                for (int i = 0; i < numberOfColumnsToRemove; i++)
                {
                    int indexToRemove = row * Constraint - 1 + i;
                    if (indexToRemove < Count)
                    {
                        removedItems.Add(_internalList[indexToRemove]);
                        _internalList.RemoveAt(indexToRemove);
                    }
                }
            }

            Constraint -= numberOfColumnsToRemove;
            return removedItems.ToArray();
        }


        public void AddRows(int numberOfRowsToAdd, Func<Point, int, T> elementBuilder)
        {
            FillGapWithBuilder(elementBuilder);

            int numberOfItemsToAdd = numberOfRowsToAdd * Constraint;
            for (int i = 0; i < numberOfItemsToAdd; i++)
            {
                AddItemWithBuilder(elementBuilder);
            }
        }

        public T[] RemoveRows(int numberOfRowsToRemove)
        {
            int rowsToStay = Height - numberOfRowsToRemove;
            int numberOfItemsToRemove = Count - rowsToStay * Constraint;
            T[] removedItems = new T[numberOfItemsToRemove];

            for (int i = 0; i < numberOfItemsToRemove; i++)
            {
                int indexToRemove = Count - 1;
                removedItems[i] = _internalList[indexToRemove];
                _internalList.RemoveAt(indexToRemove);
            }

            return removedItems;
        }

        /// <summary>
        /// Returns deep copy of wrapped list.
        /// </summary>
        public IList<T> AsSingleDimension() => _internalList.ToList();

        public void Clear() => _internalList.Clear();

        public bool Contains(T item) => _internalList.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => _internalList.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() => _internalList.GetEnumerator();

        public int IndexOf(T item) => _internalList.IndexOf(item);

        public void Insert(int index, T item) => _internalList.Insert(index, item);

        public bool Remove(T item) => _internalList.Remove(item);

        public void RemoveAt(int index) => _internalList.RemoveAt(index);

        public void RemoveAt(int x, int y) => RemoveAt(CoordsToIndex(x, y));

        public bool IsRectangular => Width * Height == Count;

        IEnumerator IEnumerable.GetEnumerator() => _internalList.GetEnumerator();

        private void FillGapWithBuilder(Func<Point, int, T> elementBuilder)
        {
            int missingItems = Constraint - Count % Constraint;
            for (int i = 0; i < missingItems; i++)
            {
                AddItemWithBuilder(elementBuilder);
            }
        }

        private void AddItemWithBuilder(Func<Point, int, T> elementBuilder)
        {
            int index = Count;
            Point coords = IndexToCoords(index);
            _internalList.Add(elementBuilder.Invoke(coords, index));
        }

        private int CoordsToIndex(int x, int y) => y * Width + x;

        private Point IndexToCoords(int index)
        {
            return new Point(index % Constraint, index / Constraint);
        }
    }

    /// <summary>
    /// Provides wrapper that allows to interpret single dimension list as two dimension array.
    /// </summary>
    internal class List2D : IList
    {
        private readonly IList _internalList;

        private int _constraint;

        public List2D()
            : this(new ArrayList(), 1) { }

        public List2D(int constraintValue)
            : this(new ArrayList(), constraintValue) { }

        public List2D(IList wrappedList, int constraint)
        {
            _internalList = wrappedList;
            Constraint = constraint;
        }

        /// <summary>
        /// Returns list that is wrapped by this instance.
        /// </summary>
        public IList CoreList => _internalList;

        public int Width => Math.Min(_internalList.Count, Constraint);

        public int Height => (int)Math.Ceiling((double)Count / Width);

        public int Constraint
        {
            get => _constraint;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Constraint must be greather than 0", nameof(Constraint));
                }

                _constraint = value;
            }
        }

        public object? this[int index]
        {
            get => _internalList[index];
            set => _internalList[index] = value;
        }

        public object? this[int x, int y]
        {
            get => _internalList[CoordsToIndex(x, y)];
            set => _internalList[CoordsToIndex(x, y)] = value;
        }

        public int Count => _internalList.Count;

        public bool IsReadOnly => _internalList.IsReadOnly;

        public int Add(object? item) => _internalList.Add(item);

        public void AddColumns(int numberOfColumnsToAdd, Func<Point, int, object> elementBuilder)
        {
            FillGapWithBuilder(elementBuilder);

            for (int row = Height; row > 0; row--)
            {
                for (int i = 0; i < numberOfColumnsToAdd; i++)
                {
                    int index = row * Constraint - 1 + i;
                    Point coords = IndexToCoords(index);
                    _internalList.Insert(index, elementBuilder.Invoke(coords, index));
                }
            }

            Constraint += numberOfColumnsToAdd;
        }

        public object?[] RemoveColumns(int numberOfColumnsToRemove)
        {
            List<object?> removedItems = new();

            for (int row = Height; row > 0; row--)
            {
                for (int i = 0; i < numberOfColumnsToRemove; i++)
                {
                    int indexToRemove = row * Constraint - 1 + i;
                    if (indexToRemove < Count)
                    {
                        removedItems.Add(_internalList[indexToRemove]);
                        _internalList.RemoveAt(indexToRemove);
                    }
                }
            }

            Constraint -= numberOfColumnsToRemove;
            return removedItems.ToArray();
        }


        public void AddRows(int numberOfRowsToAdd, Func<Point, int, object> elementBuilder)
        {
            FillGapWithBuilder(elementBuilder);

            int numberOfItemsToAdd = numberOfRowsToAdd * Constraint;
            for (int i = 0; i < numberOfItemsToAdd; i++)
            {
                AddItemWithBuilder(elementBuilder);
            }
        }

        public object?[] RemoveRows(int numberOfRowsToRemove)
        {
            int rowsToStay = Height - numberOfRowsToRemove;
            int numberOfItemsToRemove = Count - rowsToStay * Constraint;
            object?[] removedItems = new object[numberOfItemsToRemove];

            for (int i = 0; i < numberOfItemsToRemove; i++)
            {
                int indexToRemove = Count - 1;
                removedItems[i] = _internalList[indexToRemove];
                _internalList.RemoveAt(indexToRemove);
            }

            return removedItems;
        }

        /// <summary>
        /// Returns deep copy of wrapped list.
        /// </summary>
        public IList AsSingleDimension()
        {
            object[] list = new object[_internalList.Count];
            _internalList.CopyTo(list, 0);
            return list.ToList();
        }

        public void Clear() => _internalList.Clear();

        public bool Contains(object? item) => _internalList.Contains(item);

        public void CopyTo(Array array, int arrayIndex) => _internalList.CopyTo(array, arrayIndex);

        public IEnumerator GetEnumerator() => _internalList.GetEnumerator();

        public int IndexOf(object? item) => _internalList.IndexOf(item);

        public void Insert(int index, object? item) => _internalList.Insert(index, item);

        public void Remove(object? item) => _internalList.Remove(item);

        public void RemoveAt(int index) => _internalList.RemoveAt(index);

        public void RemoveAt(int x, int y) => RemoveAt(CoordsToIndex(x, y));

        public bool IsRectangular => Width * Height == Count;

        public bool IsFixedSize => _internalList.IsFixedSize;

        public bool IsSynchronized => _internalList.IsSynchronized;

        public object SyncRoot => _internalList.SyncRoot;

        IEnumerator IEnumerable.GetEnumerator() => _internalList.GetEnumerator();

        private void FillGapWithBuilder(Func<Point, int, object> elementBuilder)
        {
            int missingItems = Constraint - Count % Constraint;
            for (int i = 0; i < missingItems; i++)
            {
                AddItemWithBuilder(elementBuilder);
            }
        }

        private void AddItemWithBuilder(Func<Point, int, object> elementBuilder)
        {
            int index = Count;
            Point coords = IndexToCoords(index);
            _internalList.Add(elementBuilder.Invoke(coords, index));
        }

        private int CoordsToIndex(int x, int y) => y * Width + x;

        private Point IndexToCoords(int index)
        {
            return new Point(index % Constraint, index / Constraint);
        }
    }
}
