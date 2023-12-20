using EntityComponentSystem;
using Rendering.Components;

namespace UIComponents.Components
{
    public class LineComponent : Component
    {
        [State]
        public virtual List<LineSegmentComponent> LineSegments { get; set; }

        public override void OnInit()
        {
            LineSegments = new();
            EnsureDependency<UITransform>();
        }

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

    }
}
