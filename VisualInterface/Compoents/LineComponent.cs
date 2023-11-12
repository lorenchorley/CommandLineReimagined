using EntityComponentSystem;

namespace Console.Components
{
    public class LineComponent : Component
    {
        [State] public List<LineSegmentComponent> LineSegments = new();

        public void AddLineSegment<T>(T segment) where T : LineSegmentComponent
        {
            if (segment.Entity == Entity)
            {
                throw new InvalidOperationException("Cannot add a line segment to the entity of the line");
            }

            segment.Entity.Parent = Entity;

            LineSegments.Add(segment);
        }

        public void Clear()
        {
            foreach (var segment in LineSegments)
            {
                ((Component)segment).Entity.Destroy();
            }

            LineSegments.Clear();
        }

        public string ToText()
        {
            return LineSegments.Select(l => l.ToText())
                                .Join("");
        }

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                for (int i = 0; i < LineSegments.Count; i++)
                {
                    var lineSegment = LineSegments[i];
                    yield return ($"LineSegment[{i}]", lineSegment.ToText());
                }
            }
        }

    }
}
