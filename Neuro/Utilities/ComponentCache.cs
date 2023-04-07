﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Neuro.Utilities;

/// <summary>
/// Caches all components of type T in the scene (including disabled ones)
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class ComponentCache<T> : IList<T>, IList, IReadOnlyList<T> where T : Component
{
    public static ComponentCache<T> Cached { get; } = new();

    public static ComponentCache<T> FindObjects()
    {
        Cached._list.Clear();
        Cached._list.AddRange(GameObject.FindObjectsOfType<T>(true));
        return Cached;
    }

    private void CleanupList()
    {
        for (int i = _list.Count - 1; i >= 0; i--)
        {
            if (!_list[i])
            {
                _list.RemoveAt(i);
            }
        }
    }

    #region Interface implementation

    private readonly List<T> _list = new();

    private ComponentCache()
    {
    }

    public int Count
    {
        get
        {
            CleanupList();
            return _list.Count;
        }
    }

    public T this[int index]
    {
        get
        {
            CleanupList();
            return _list[index];
        }
    }

    public bool Contains(T item)
    {
        CleanupList();
        return _list.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        CleanupList();
        _list.CopyTo(array, arrayIndex);
    }

    public IEnumerator<T> GetEnumerator()
    {
        CleanupList();
        return _list.GetEnumerator();
    }

    public int IndexOf(T item)
    {
        CleanupList();
        return _list.IndexOf(item);
    }

    bool ICollection<T>.IsReadOnly => true;

    T IList<T>.this[int index]
    {
        get => _list[index];
        set => throw new NotSupportedException("Operation not supported on read-only collection");
    }

    void ICollection<T>.Add(T item)
    {
        throw new NotSupportedException("Operation not supported on read-only collection");
    }

    void ICollection<T>.Clear()
    {
        throw new NotSupportedException("Operation not supported on read-only collection");
    }

    void IList<T>.Insert(int index, T value)
    {
        throw new NotSupportedException("Operation not supported on read-only collection");
    }

    bool ICollection<T>.Remove(T value)
    {
        throw new NotSupportedException("Operation not supported on read-only collection");
    }

    void IList<T>.RemoveAt(int index)
    {
        throw new NotSupportedException("Operation not supported on read-only collection");
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        CleanupList();
        return ((IEnumerable) _list).GetEnumerator();
    }

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => _list is ICollection coll ? coll.SyncRoot : this;

    void ICollection.CopyTo(Array array, int arrayIndex)
    {
        CleanupList();
        ((ICollection) _list).CopyTo(array, arrayIndex);
    }

    bool IList.IsFixedSize => true;

    bool IList.IsReadOnly => true;

    object IList.this[int index]
    {
        get
        {
            CleanupList();
            return ((IList) _list)[index];
        }
        set => throw new NotSupportedException("Operation not supported on read-only collection");
    }

    int IList.Add(object item)
    {
        throw new NotSupportedException("Operation not supported on read-only collection");
    }

    void IList.Clear()
    {
        throw new NotSupportedException("Operation not supported on read-only collection");
    }

    bool IList.Contains(object item)
    {
        CleanupList();
        return ((IList) _list).Contains(item);
    }

    int IList.IndexOf(object item)
    {
        CleanupList();
        return ((IList) _list).IndexOf(item);
    }

    void IList.Insert(int index, object item)
    {
        throw new NotSupportedException("Operation not supported on read-only collection");
    }

    void IList.Remove(object item)
    {
        throw new NotSupportedException("Operation not supported on read-only collection");
    }

    void IList.RemoveAt(int index)
    {
        throw new NotSupportedException("Operation not supported on read-only collection");
    }

    #endregion
}
