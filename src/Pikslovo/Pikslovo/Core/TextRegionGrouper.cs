namespace Pikslovo.Core;

public sealed class TextRegionGrouper
{
    public const float DefaultGroupingPower = 0.25f;

    private readonly float _groupingPower;

    public TextRegionGrouper(float groupingPower = DefaultGroupingPower)
    {
        _groupingPower = Math.Clamp(groupingPower, DefaultGroupingPower, 1f);
    }

    public IReadOnlyList<TextRegion> Group(IReadOnlyList<TextRegion> regions)
    {
        if (regions.Count <= 1)
        {
            return regions;
        }

        var medianHeight = GetMedianHeight(regions);
        var lines = Merge(regions, medianHeight, IsSameLine, IsBeyondLine, " ");
        var paragraphs = Merge(lines, medianHeight, IsSameParagraph, IsBeyondParagraph, "\n");

        return paragraphs
            .OrderBy(region => region.Bounds.Top)
            .ThenBy(region => region.Bounds.Left)
            .ToArray();
    }

    private static int GetMedianHeight(IReadOnlyList<TextRegion> regions)
    {
        var heights = regions
            .Select(region => region.Bounds.Height)
            .Order()
            .ToArray();

        return heights.Length == 0 ? 20 : heights[heights.Length / 2];
    }

    private static bool IsSameLine(TextRegion first, TextRegion second, int medianHeight)
    {
        var firstBounds = first.Bounds;
        var secondBounds = second.Bounds;
        var firstCenter = (firstBounds.Top + firstBounds.Bottom) / 2d;
        var secondCenter = (secondBounds.Top + secondBounds.Bottom) / 2d;
        if (Math.Abs(firstCenter - secondCenter) > 0.5d * medianHeight)
        {
            return false;
        }

        var heightRatio = (double)Math.Max(firstBounds.Height, secondBounds.Height) /
            Math.Min(firstBounds.Height, secondBounds.Height);
        if (heightRatio > 1.5d)
        {
            return false;
        }

        var horizontalGap = Math.Max(firstBounds.Left, secondBounds.Left) -
            Math.Min(firstBounds.Right, secondBounds.Right);
        return horizontalGap <= medianHeight;
    }

    private static bool IsBeyondLine(TextRegion first, TextRegion second, int medianHeight) =>
        second.Bounds.Top - first.Bounds.Bottom > medianHeight;

    private bool IsSameParagraph(TextRegion first, TextRegion second, int medianHeight)
    {
        var firstBounds = first.Bounds;
        var secondBounds = second.Bounds;
        var upperBounds = firstBounds.Top <= secondBounds.Top ? firstBounds : secondBounds;
        var lowerBounds = firstBounds.Top <= secondBounds.Top ? secondBounds : firstBounds;
        var verticalGap = Math.Max(0, lowerBounds.Top - upperBounds.Bottom);
        if (verticalGap > 1.5d * medianHeight * _groupingPower)
        {
            return false;
        }

        var overlapLeft = Math.Max(firstBounds.Left, secondBounds.Left);
        var overlapRight = Math.Min(firstBounds.Right, secondBounds.Right);
        var overlapWidth = Math.Max(0, overlapRight - overlapLeft);
        var minimumWidth = Math.Min(firstBounds.Width, secondBounds.Width);
        if (minimumWidth == 0 || overlapWidth / (double)minimumWidth < 0.3d)
        {
            return false;
        }

        var firstLineHeight = firstBounds.Height / Math.Max(1, (int)Math.Round(firstBounds.Height / (double)medianHeight));
        var secondLineHeight = secondBounds.Height / Math.Max(1, (int)Math.Round(secondBounds.Height / (double)medianHeight));
        var heightRatio = (double)Math.Max(firstLineHeight, secondLineHeight) /
            Math.Min(firstLineHeight, secondLineHeight);
        if (heightRatio > 1.4d)
        {
            return false;
        }

        var leftEdgeDistance = Math.Abs(firstBounds.Left - secondBounds.Left);
        var rightEdgeDistance = Math.Abs(firstBounds.Right - secondBounds.Right);
        var firstCenter = (firstBounds.Left + firstBounds.Right) / 2d;
        var secondCenter = (secondBounds.Left + secondBounds.Right) / 2d;
        var centerDistance = Math.Abs(firstCenter - secondCenter);

        return leftEdgeDistance <= 2d * medianHeight ||
            rightEdgeDistance <= 2d * medianHeight ||
            centerDistance <= medianHeight;
    }

    private bool IsBeyondParagraph(TextRegion first, TextRegion second, int medianHeight) =>
        second.Bounds.Top - first.Bounds.Bottom > 1.5d * medianHeight * _groupingPower;

    private static IReadOnlyList<TextRegion> Merge(
        IReadOnlyList<TextRegion> regions,
        int medianHeight,
        Func<TextRegion, TextRegion, int, bool> canMerge,
        Func<TextRegion, TextRegion, int, bool> isBeyondSearchRange,
        string separator)
    {
        var groups = new DisjointSet(regions.Count);
        var orderedIndexes = Enumerable.Range(0, regions.Count)
            .OrderBy(index => regions[index].Bounds.Top)
            .ToArray();
        for (var firstPosition = 0; firstPosition < orderedIndexes.Length; firstPosition++)
        {
            var first = orderedIndexes[firstPosition];
            for (var secondPosition = firstPosition + 1; secondPosition < orderedIndexes.Length; secondPosition++)
            {
                var second = orderedIndexes[secondPosition];
                if (isBeyondSearchRange(regions[first], regions[second], medianHeight))
                {
                    break;
                }

                if (canMerge(regions[first], regions[second], medianHeight))
                {
                    groups.Union(first, second);
                }
            }
        }

        return Enumerable.Range(0, regions.Count)
            .GroupBy(groups.Find)
            .Select(group => MergeGroup(group.Select(index => regions[index]), separator))
            .ToArray();
    }

    private static TextRegion MergeGroup(IEnumerable<TextRegion> regions, string separator)
    {
        var ordered = regions
            .OrderBy(region => region.Bounds.Top)
            .ThenBy(region => region.Bounds.Left)
            .ToArray();
        var bounds = ordered.Select(region => region.Bounds).ToArray();

        return new TextRegion(
            string.Join(separator, ordered.Select(region => region.Text)),
            new PixelRect(
                bounds.Min(bound => bound.Left),
                bounds.Min(bound => bound.Top),
                bounds.Max(bound => bound.Right),
                bounds.Max(bound => bound.Bottom)));
    }

    private sealed class DisjointSet
    {
        private readonly int[] _parents;
        private readonly int[] _ranks;

        public DisjointSet(int count)
        {
            _parents = Enumerable.Range(0, count).ToArray();
            _ranks = new int[count];
        }

        public int Find(int index)
        {
            if (_parents[index] != index)
            {
                _parents[index] = Find(_parents[index]);
            }

            return _parents[index];
        }

        public void Union(int first, int second)
        {
            var firstRoot = Find(first);
            var secondRoot = Find(second);
            if (firstRoot == secondRoot)
            {
                return;
            }

            if (_ranks[firstRoot] < _ranks[secondRoot])
            {
                _parents[firstRoot] = secondRoot;
            }
            else
            {
                _parents[secondRoot] = firstRoot;
                if (_ranks[firstRoot] == _ranks[secondRoot])
                {
                    _ranks[firstRoot]++;
                }
            }
        }
    }
}
