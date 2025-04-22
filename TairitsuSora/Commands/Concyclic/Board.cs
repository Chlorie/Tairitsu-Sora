namespace TairitsuSora.Commands.Concyclic;

public record struct CircleInfo(IGeneralizedCircle Circle, HashSet<Point> Points);

public class Board
{
    public bool this[Point point] => _placed.Contains(point);

    public IReadOnlySet<Point> Points => _placed;

    public Point? LastPlayed { get; private set; }

    public void Place(Point point)
    {
        LastPlayed = point;
        _placed.Add(point);
    }

    public List<CircleInfo> FindConcyclicQuadruples()
    {
        if (LastPlayed is not { } target) return [];
        Dictionary<IGeneralizedCircle, HashSet<Point>> circles = [];
        List<Point> points = _placed.ToList();
        int n = points.Count;
        for (int i = 0; i < n - 1; i++)
        {
            Point p = points[i];
            if (p == target) continue;
            for (int j = i + 1; j < n; j++)
            {
                Point q = points[j];
                if (q == target) continue;
                var circle = GeneralizedCircle.FromPoints(p, q, target);
                if (circles.TryGetValue(circle, out var pointsOnCircle))
                {
                    pointsOnCircle.Add(p);
                    pointsOnCircle.Add(q);
                }
                else
                    circles.Add(circle, [p, q]);
            }
        }
        return circles.Where(kv => kv.Value.Count >= 3)
            .Select(kv => new CircleInfo(kv.Key, kv.Value))
            .ToList();
    }

    private readonly HashSet<Point> _placed = [];
}
