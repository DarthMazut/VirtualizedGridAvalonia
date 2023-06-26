using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualizedGrid
{
    internal class AreaList<T> : IList<T>
    {
        private readonly IList<T> _internalList;
        
        private int _widthConstraint;
        private int _heightConstraint;

        public AreaList(int widthConstraint)
            : this(new List<T>(), widthConstraint, DimensionConstraint.Horizontal) { }

        public AreaList(int constraintValue, DimensionConstraint dimensionConstraint)
            : this(new List<T>(), constraintValue, dimensionConstraint) { }

        public AreaList(IList<T> wrappedList, int widthConstraint)
            : this(wrappedList, widthConstraint, DimensionConstraint.Horizontal) { }

        public AreaList(IList<T> wrappedList, int constraintValue, DimensionConstraint dimensionConstraint)
        {
            _internalList = wrappedList;
            if (dimensionConstraint == DimensionConstraint.Horizontal)
            {
                WidthConstraint = constraintValue;
            }

            if (dimensionConstraint == DimensionConstraint.Vertical)
            {
                throw new NotImplementedException();
            }
        }

        public IList<T> CoreList => _internalList;

        public int Width => Math.Min(_internalList.Count, WidthConstraint);

        public int Height => (int)Math.Ceiling((double)Count / Width);

        // Zero means no constraint

        public int WidthConstraint 
        { 
            get => _widthConstraint; 
            set
            {
                _widthConstraint = value;
                HeightConstraint = 0;
            }
        }

        public int HeightConstraint 
        { 
            get => 0; 
            set
            {
                throw new NotImplementedException();
                //_heightConstraint = value;
                //WidthConstraint = 0;
            }
        }

        public DimensionConstraint DimensionConstraint => DimensionConstraint.Horizontal;

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

        public void AddColumn(Func<int, int, int, T> elementBuilder) => AddColumns(1, elementBuilder);

        public void AddColumns(int numberOfColumnsToAdd, Func<int, int, int, T> elementBuilder)
        {

        }

        public void AddRow(Func<int, int, int, T> elementBuilder) => AddRows(1, elementBuilder);

        public void AddRows(int numberOfRowsToAdd, Func<int, int, int, T> elementBuilder)
        {
            if (!IsRectangular)
            {
                int missingItems = Count % Width;
                for (int i = 0; i < missingItems; i++)
                {
                    int index = Count;
                    (int x, int y) coords = IndexToCoords(index);
                    _internalList.Add(elementBuilder.Invoke(coords.x, coords.y, index));
                }
            }


        }

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

        private int CoordsToIndex(int x, int y) => y * Width + x;

        private (int x, int y) IndexToCoords(int index) => (index % Width, index / Width);
    }

    internal enum DimensionConstraint
    {
        Horizontal,
        Vertical
    }
}
