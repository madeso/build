using System.Reflection;

namespace Workbench.Utils;

public static class Reflect
{
    public record Resolvers<TData>(Func<MemberInfo, TData> Member, Func<PropertyInfo, TData> Property);


    public static IEnumerable<KeyValuePair<TData, TMember>>
        PublicStaticValuesOf<TContainer, TMember, TData>(Resolvers<TData> resolvers)
    {
        var container_type = typeof(TContainer);
        var member_type = typeof(TMember);

        foreach (var member in container_type
                     .GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (member.FieldType != member_type)
            {
                continue;
            }

            var val = member.GetValue(null);
            if (val == null) { continue; }
            yield return new KeyValuePair<TData, TMember>(resolvers.Member(member), (TMember)val);
        }

        foreach (var property in container_type
                     .GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (property.DeclaringType != member_type)
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
                mem => get_attribute(mem.GetCustomAttributes()),
                prop => get_attribute(prop.GetCustomAttributes())
            );

        static TAttribute get_attribute(IEnumerable<Attribute> attributes)
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
