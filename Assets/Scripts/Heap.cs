using UnityEngine;
using System.Collections;
using System;

public class Heap<Node> where Node : IHeapItem<Node> {

    Node[] items;
    int currentItemCount;

    public Heap(int maxHeapSize) {
        items = new Node[maxHeapSize];
    }

    public void Add(Node item) {
        item.HeapIndex = currentItemCount;
        items[currentItemCount] = item;
        SortUp(item);
        currentItemCount++;
    }

    public Node RemoveFirst() {
        Node firstItem = items[0];
        currentItemCount--;
        items[0] = items[currentItemCount];
        items[0].HeapIndex = 0;
        SortDown(items[0]);
        return firstItem;
    }

    public void UpdateItem(Node item) {
        SortUp(item);
    }

    public int Count {
        get {
            return currentItemCount;
        }
    }

    public bool Contains(Node item) {
        return Equals(items[item.HeapIndex], item);
    }

    void SortDown(Node item) {
        while (true) {
            int childIndexLeft = item.HeapIndex * 2 + 1;
            int childIndexRight = item.HeapIndex * 2 + 2;
            int swapIndex = 0;

            if (childIndexLeft < currentItemCount) {
                swapIndex = childIndexLeft;

                if (childIndexRight < currentItemCount) {
                    if (items[childIndexLeft].CompareTo(items[childIndexRight]) < 0) {
                        swapIndex = childIndexRight;
                    }
                }

                if (item.CompareTo(items[swapIndex]) < 0) {
                    Swap(item, items[swapIndex]);
                } else {
                    return;
                }

            } else {
                return;
            }

        }
    }

    void SortUp(Node item) {
        int parentIndex = (item.HeapIndex - 1) / 2;

        while (true) {
            Node parentItem = items[parentIndex];
            if (item.CompareTo(parentItem) > 0) {
                Swap(item, parentItem);
            } else {
                break;
            }

            parentIndex = (item.HeapIndex - 1) / 2;
        }
    }

    void Swap(Node itemA, Node itemB) {
        items[itemA.HeapIndex] = itemB;
        items[itemB.HeapIndex] = itemA;
        int itemAIndex = itemA.HeapIndex;
        itemA.HeapIndex = itemB.HeapIndex;
        itemB.HeapIndex = itemAIndex;
    }
}

public interface IHeapItem<T> : IComparable<T> {
    int HeapIndex {
        get;
        set;
    }
}
