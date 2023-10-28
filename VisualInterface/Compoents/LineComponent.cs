using EntityComponentSystem;

namespace Console.Components
{
    public class LineComponent : Component
    {
        private List<ILineSegment> _lineSegments = new();

        public IEnumerable<ILineSegment> GetOrderedLineSegments() => _lineSegments;

        public void AddLineSegment<T>(T segment) where T : Component, ILineSegment
        {
            _lineSegments.Add(segment);
        }

        public void Clear()
        {
            foreach (var segment in _lineSegments)
            {
                ((Component)segment).Entity.Destroy();
            }

            _lineSegments.Clear();
        }

        public string ToText()
        {
            return _lineSegments.Select(l => l.ToText())
                                .Join("");
        }

        public override IEnumerable<(string, string)> SerialisableDebugProperties
        {
            get
            {
                for (int i = 0; i < _lineSegments.Count; i++)
                {
                    var lineSegment = _lineSegments[i];
                    yield return ($"LineSegment[{i}]", lineSegment.ToText());
                }
            }
        }

    }
}
