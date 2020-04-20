using System;
using System.Linq;

namespace CapFrameX.Extensions.NetStandard
{
    public static class EnumExtensions
    {
        public static TAttribute GetAttribute<TAttribute>(this Enum value)
            where TAttribute : Attribute
        {
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            var attributes = type.GetField(name).GetCustomAttributes(false);
            return attributes.OfType<TAttribute>().SingleOrDefault();
        }
    }
}
