using Newtonsoft.Json;
using System;
using System.Drawing;

namespace CommandLineReimagined.Serialisation;

public class RectangleFConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(RectangleF);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var rectangle = (RectangleF)value;
        writer.WriteStartArray();
        writer.WriteValue(rectangle.X);
        writer.WriteValue(rectangle.Y);
        writer.WriteValue(rectangle.Width);
        writer.WriteValue(rectangle.Height);
        writer.WriteEndArray();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
