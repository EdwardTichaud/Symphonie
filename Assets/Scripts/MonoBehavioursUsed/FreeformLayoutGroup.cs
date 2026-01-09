using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LayoutGroup libre qui memorise les positions des enfants sans les contraindre
/// a une grille stricte.
/// </summary>
[AddComponentMenu("Layout/Freeform Layout Group")]
public class FreeformLayoutGroup : LayoutGroup
{
    [Serializable]
    public class LayoutEntry
    {
        public RectTransform rect;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 referenceSize;
        [HideInInspector]
        public bool sizeInitialized;
        [HideInInspector]
        public bool referenceSizeInitialized;
    }

    [Header("Layout")]
    [SerializeField] private bool editMode = false;
    [SerializeField] private bool useScaleForSize = false;
    [SerializeField] private List<LayoutEntry> entries = new();

    [Header("Runtime Slots")]
    [SerializeField] private List<Vector2> slotPositions = new();

    [Header("Grid Helper")]
    [SerializeField] private Vector2 gridSpacing = Vector2.zero;
    [SerializeField] private int gridColumns = 4;

    private readonly List<RectTransform> runtimeEntries = new();

    public bool EditMode => editMode;
    public bool UseScaleForSize => useScaleForSize;
    public IReadOnlyList<LayoutEntry> Entries => entries;
    public IReadOnlyList<Vector2> SlotPositions => slotPositions;
    public int RuntimeEntryCount => runtimeEntries.Count;

    public override void CalculateLayoutInputHorizontal()
    {
        Vector2 size = CalculateEntriesBounds(editMode);
        SetLayoutInputForAxis(size.x + padding.horizontal, size.x + padding.horizontal, -1, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        Vector2 size = CalculateEntriesBounds(editMode);
        SetLayoutInputForAxis(size.y + padding.vertical, size.y + padding.vertical, -1, 1);
    }

    public override void SetLayoutHorizontal()
    {
        if (editMode)
        {
            ApplyEntrySizes();
            ApplyRuntimeSizes();
            return;
        }

        ApplyEntries();
    }

    public override void SetLayoutVertical()
    {
        if (editMode)
        {
            ApplyEntrySizes();
            ApplyRuntimeSizes();
            return;
        }

        ApplyEntries();
    }

    public void AddEntry(RectTransform rect)
    {
        if (rect == null)
            return;

        if (ContainsEntry(rect))
            return;

        entries.Add(new LayoutEntry
        {
            rect = rect,
            anchoredPosition = rect.anchoredPosition,
            sizeDelta = useScaleForSize ? GetScaledRectSize(rect) : rect.sizeDelta,
            sizeInitialized = true,
            referenceSize = rect.rect.size,
            referenceSizeInitialized = true
        });

        SetDirty();
    }

    public void RemoveMissingEntries()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] == null || entries[i].rect == null)
                entries.RemoveAt(i);
        }

        SetDirty();
    }

    public void CaptureLayout()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.rect == null)
                continue;

            entry.anchoredPosition = entry.rect.anchoredPosition;
            entry.sizeDelta = useScaleForSize ? GetScaledRectSize(entry.rect) : entry.rect.sizeDelta;
            entry.sizeInitialized = true;
            entry.referenceSize = entry.rect.rect.size;
            entry.referenceSizeInitialized = true;
        }

        SyncSlotsFromEntries();
        SetDirty();
    }

    public void ApplyLayout()
    {
        ApplyEntries();
    }

    public void ApplySizes()
    {
        ApplyEntrySizes();
        ApplyRuntimeSizes();
        SetDirty();
    }

    public void CaptureReferenceSizes()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.rect == null)
                continue;

            entry.referenceSize = entry.rect.rect.size;
            entry.referenceSizeInitialized = true;
            entry.sizeDelta = GetScaledRectSize(entry.rect);
            entry.sizeInitialized = true;
        }

        SetDirty();
    }

    public void BakeScaleToSize()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.rect == null)
                continue;

            Vector2 scaled = GetScaledRectSize(entry.rect);
            entry.sizeDelta = scaled;
            entry.sizeInitialized = true;
            entry.rect.sizeDelta = scaled;
            Vector3 scale = entry.rect.localScale;
            entry.rect.localScale = new Vector3(1f, 1f, scale.z);
        }

        SetDirty();
    }

    public void AutoArrangeGrid()
    {
        if (entries.Count == 0)
            return;

        RectTransform rect = transform as RectTransform;
        if (rect == null)
            return;

        int columns = Mathf.Max(1, gridColumns);
        int rows = Mathf.CeilToInt(entries.Count / (float)columns);
        Vector2 size = rect.rect.size;

        float[] columnWidths = new float[columns];
        float[] rowHeights = new float[rows];

        for (int i = 0; i < entries.Count; i++)
        {
            int column = i % columns;
            int row = i / columns;
            Vector2 entrySize = GetEntrySize(entries[i], editMode);
            columnWidths[column] = Mathf.Max(columnWidths[column], entrySize.x);
            rowHeights[row] = Mathf.Max(rowHeights[row], entrySize.y);
        }

        float requiredWidth = padding.horizontal + Sum(columnWidths) + Mathf.Max(0, columns - 1) * gridSpacing.x;
        float requiredHeight = padding.vertical + Sum(rowHeights) + Mathf.Max(0, rows - 1) * gridSpacing.y;

        float startOffsetX = GetStartOffset(0, requiredWidth - padding.horizontal);
        float startOffsetY = GetStartOffset(1, requiredHeight - padding.vertical);

        float leftEdge = -size.x * rect.pivot.x;
        float topEdge = size.y * (1f - rect.pivot.y);

        float startX = leftEdge + startOffsetX;
        float startY = topEdge - startOffsetY;

        float[] columnStartX = new float[columns];
        float[] rowStartY = new float[rows];

        float currentX = startX;
        for (int column = 0; column < columns; column++)
        {
            columnStartX[column] = currentX;
            currentX += columnWidths[column] + gridSpacing.x;
        }

        float currentY = startY;
        for (int row = 0; row < rows; row++)
        {
            rowStartY[row] = currentY;
            currentY -= rowHeights[row] + gridSpacing.y;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null)
                continue;

            int column = i % columns;
            int row = i / columns;
            Vector2 pivot = entry.rect != null ? entry.rect.pivot : new Vector2(0.5f, 0.5f);
            Vector2 entrySize = GetEntrySize(entry, editMode);

            float x = columnStartX[column] + entrySize.x * pivot.x;
            float y = rowStartY[row] - entrySize.y * (1f - pivot.y);
            entry.anchoredPosition = new Vector2(x, y);
        }

        if (!editMode)
            ApplyEntries();

        SyncSlotsFromEntries();
        SetDirty();
    }

    public bool RegisterRuntimeChild(RectTransform rect)
    {
        if (rect == null)
            return false;

        if (runtimeEntries.Contains(rect))
            return false;

        runtimeEntries.Add(rect);
        bool applied = ApplyRuntimeEntry(runtimeEntries.Count - 1, rect);
        SetDirty();
        return applied;
    }

    public void ClearRuntimeEntries()
    {
        runtimeEntries.Clear();
        SetDirty();
    }

    private bool ContainsEntry(RectTransform rect)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry != null && entry.rect == rect)
                return true;
        }

        return false;
    }

    private void ApplyEntries()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.rect == null)
                continue;

            if (!entry.rect.IsChildOf(transform))
                continue;

            entry.rect.anchoredPosition = entry.anchoredPosition;
            ApplyEntrySize(entry);
        }

        ApplyRuntimeEntries();
    }

    private void ApplyEntrySizes()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.rect == null)
                continue;

            if (!entry.rect.IsChildOf(transform))
                continue;

            ApplyEntrySize(entry);
        }
    }

    private void ApplyEntrySize(LayoutEntry entry)
    {
        if (entry == null || entry.rect == null)
            return;

        if (!entry.sizeInitialized)
        {
            entry.sizeDelta = useScaleForSize ? GetScaledRectSize(entry.rect) : entry.rect.sizeDelta;
            entry.sizeInitialized = true;
        }

        EnsureReferenceSize(entry, entry.rect);

        if (useScaleForSize)
        {
            Vector2 refSize = entry.referenceSizeInitialized ? entry.referenceSize : entry.rect.rect.size;
            Vector3 scale = entry.rect.localScale;
            scale.x = GetScaleComponent(entry.sizeDelta.x, refSize.x);
            scale.y = GetScaleComponent(entry.sizeDelta.y, refSize.y);
            entry.rect.sizeDelta = refSize;
            entry.rect.localScale = scale;
        }
        else if (entry.sizeInitialized)
        {
            entry.rect.sizeDelta = entry.sizeDelta;
        }
    }

    private void ApplyRuntimeEntries()
    {
        if (runtimeEntries.Count == 0)
            return;

        for (int i = 0; i < runtimeEntries.Count; i++)
        {
            RectTransform rect = runtimeEntries[i];
            if (rect == null || !rect.IsChildOf(transform))
                continue;

            if (TryGetSlotPosition(i, out Vector2 position))
                rect.anchoredPosition = position;

            ApplyRuntimeSize(i, rect);
        }
    }

    private void ApplyRuntimeSizes()
    {
        if (runtimeEntries.Count == 0)
            return;

        for (int i = 0; i < runtimeEntries.Count; i++)
        {
            RectTransform rect = runtimeEntries[i];
            if (rect == null || !rect.IsChildOf(transform))
                continue;

            ApplyRuntimeSize(i, rect);
        }
    }

    private bool ApplyRuntimeEntry(int index, RectTransform rect)
    {
        if (rect == null)
            return false;

        if (TryGetSlotPosition(index, out Vector2 position))
        {
            rect.anchoredPosition = position;
            ApplyRuntimeSize(index, rect);
            return true;
        }

        return false;
    }

    private Vector2 CalculateEntriesBounds(bool useLivePositions)
    {
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        bool hasBounds = false;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null)
                continue;

            Vector2 size = GetEntrySize(entry, useLivePositions);
            Vector2 pivot = entry.rect != null ? entry.rect.pivot : new Vector2(0.5f, 0.5f);
            Vector2 position = useLivePositions && entry.rect != null ? entry.rect.anchoredPosition : entry.anchoredPosition;

            AccumulateBounds(position, size, pivot, ref minX, ref minY, ref maxX, ref maxY);
            hasBounds = true;
        }

        for (int i = 0; i < runtimeEntries.Count; i++)
        {
            RectTransform rect = runtimeEntries[i];
            if (rect == null)
                continue;

            Vector2 size = useLivePositions
                ? GetEffectiveRectSize(rect)
                : (TryGetSlotSize(i, out Vector2 slotSize) ? slotSize : GetEffectiveRectSize(rect));
            Vector2 pivot = rect.pivot;
            Vector2 position = useLivePositions
                ? rect.anchoredPosition
                : (TryGetSlotPosition(i, out Vector2 slot) ? slot : rect.anchoredPosition);

            AccumulateBounds(position, size, pivot, ref minX, ref minY, ref maxX, ref maxY);
            hasBounds = true;
        }

        if (!hasBounds)
            return Vector2.zero;

        return new Vector2(Mathf.Max(0f, maxX - minX), Mathf.Max(0f, maxY - minY));
    }

    private Vector2 GetEntrySize(LayoutEntry entry, bool useLiveSize)
    {
        if (entry == null)
            return Vector2.zero;

        if (useLiveSize && entry.rect != null)
            return GetEffectiveRectSize(entry.rect);

        if (entry.sizeInitialized)
            return entry.sizeDelta;

        if (entry.rect != null)
            return GetEffectiveRectSize(entry.rect);

        return Vector2.zero;
    }

    private static float Sum(float[] values)
    {
        float total = 0f;
        for (int i = 0; i < values.Length; i++)
            total += values[i];
        return total;
    }

    private void SyncSlotsFromEntries()
    {
        slotPositions.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null)
                continue;

            if (entry.rect != null && !entry.sizeInitialized)
            {
                entry.sizeDelta = useScaleForSize ? GetScaledRectSize(entry.rect) : entry.rect.sizeDelta;
                entry.sizeInitialized = true;
            }
            if (entry.rect != null && !entry.referenceSizeInitialized)
            {
                entry.referenceSize = entry.rect.rect.size;
                entry.referenceSizeInitialized = true;
            }
            slotPositions.Add(entry.anchoredPosition);
        }
    }

    private bool TryGetSlotPosition(int index, out Vector2 position)
    {
        if (index < 0)
        {
            position = Vector2.zero;
            return false;
        }

        if (slotPositions.Count > 0)
        {
            if (index < slotPositions.Count)
            {
                position = slotPositions[index];
                return true;
            }

            position = Vector2.zero;
            return false;
        }

        if (index < entries.Count)
        {
            position = entries[index].anchoredPosition;
            return true;
        }

        position = Vector2.zero;
        return false;
    }

    private bool TryGetSlotSize(int index, out Vector2 size)
    {
        if (index < 0)
        {
            size = Vector2.zero;
            return false;
        }

        if (index < entries.Count && entries[index] != null && entries[index].sizeInitialized)
        {
            size = entries[index].sizeDelta;
            return true;
        }

        size = Vector2.zero;
        return false;
    }

    private void ApplyRuntimeSize(int index, RectTransform rect)
    {
        if (rect == null)
            return;

        if (!TryGetSlotSize(index, out Vector2 desiredSize))
            return;

        if (useScaleForSize)
        {
            Vector2 refSize = GetReferenceSizeForIndex(index, rect);
            Vector3 scale = rect.localScale;
            scale.x = GetScaleComponent(desiredSize.x, refSize.x);
            scale.y = GetScaleComponent(desiredSize.y, refSize.y);
            rect.sizeDelta = refSize;
            rect.localScale = scale;
        }
        else
        {
            rect.sizeDelta = desiredSize;
        }
    }

    private Vector2 GetReferenceSizeForIndex(int index, RectTransform rect)
    {
        if (index >= 0 && index < entries.Count)
        {
            var entry = entries[index];
            if (entry != null)
            {
                if (!entry.referenceSizeInitialized)
                    EnsureReferenceSize(entry, rect);
                if (entry.referenceSizeInitialized)
                    return entry.referenceSize;
            }
        }

        return rect != null ? rect.rect.size : Vector2.zero;
    }

    private void EnsureReferenceSize(LayoutEntry entry, RectTransform rect)
    {
        if (entry == null || entry.referenceSizeInitialized)
            return;

        Vector2 size = rect != null ? rect.rect.size : entry.sizeDelta;
        entry.referenceSize = size;
        entry.referenceSizeInitialized = size != Vector2.zero;
    }

    private Vector2 GetEffectiveRectSize(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        if (!useScaleForSize)
            return rect.rect.size;

        return GetScaledRectSize(rect);
    }

    private Vector2 GetScaledRectSize(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        Vector3 scale = rect.localScale;
        return new Vector2(rect.rect.size.x * scale.x, rect.rect.size.y * scale.y);
    }

    private float GetScaleComponent(float desired, float reference)
    {
        if (Mathf.Approximately(reference, 0f))
            return 1f;

        float scale = desired / reference;
        if (float.IsNaN(scale) || float.IsInfinity(scale))
            return 1f;

        return scale;
    }

    private static void AccumulateBounds(
        Vector2 position,
        Vector2 size,
        Vector2 pivot,
        ref float minX,
        ref float minY,
        ref float maxX,
        ref float maxY)
    {
        float left = position.x - (size.x * pivot.x);
        float right = position.x + (size.x * (1f - pivot.x));
        float bottom = position.y - (size.y * pivot.y);
        float top = position.y + (size.y * (1f - pivot.y));

        minX = Mathf.Min(minX, left);
        minY = Mathf.Min(minY, bottom);
        maxX = Mathf.Max(maxX, right);
        maxY = Mathf.Max(maxY, top);
    }
}
