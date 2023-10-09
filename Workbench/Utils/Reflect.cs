using System.Reflection;

namespace Workbench.Utils;

public static class Reflect
{
    public record Resolvers<TData>(Func<MemberInfo, TData> Member, Func<PropertyInfo, TData> Property);


    public static IEnumerable<KeyValuePair<TData, TMember>>
        PublicStaticValuesOf<TContainer, TMember, TData>(Resolvers<TData> resolvers)
    {
        var containerType = typeof(TContainer);
        var memberType = typeof(TMember);

        foreach (var member in containerType
                     .GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (member.FieldType != memberType)
            {
                continue;
            }

            var val = member.GetValue(null);
            if (val == null) { continue; }
            yield return new KeyValuePair<TData, TMember>(resolvers.Member(member), (TMember)val);
        }

        foreach (var property in containerType
                     .GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (property.DeclaringType != memberType)
            {
                continue;
            }

            var val = property.GetValue(null, null);
            if (val == null) { continue; }
            yield return new KeyValuePair<TData, TMember>(resolvers.Member(property), (TMember)val);
        }
    }


    public static Resolvers<string> NameResolver { get; } = new(mem => mem.Name, prop => prop.Name);


    public static Resolvers<TAttribute> Attributes<TAttribute>()
        where TAttribute : Attribute
    {
        return new Resolvers<TAttribute>
            (
                mem => GetAttribute(mem.GetCustomAttributes()),
                prop => GetAttribute(prop.GetCustomAttributes())
            );

        static TAttribute GetAttribute(IEnumerable<Attribute> attributes)
        {
            foreach (var attribute in attributes)
            {
                if (attribute is TAttribute my)
                {
                    return my;
                }
            }

            // how should missing attributes be handled?
            throw new NotImplementedException();
        }
    }


    public static IEnumerable<KeyValuePair<TData, TContainer>>
        PublicStaticValuesOf<TContainer, TData>(Resolvers<TData> resolvers)
    {
        return PublicStaticValuesOf<TContainer, TContainer, TData>(resolvers);
    }
}
