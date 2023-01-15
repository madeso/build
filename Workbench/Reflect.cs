using System.Reflection;

namespace Workbench;

public static class Reflect
{
    public record Resolvers<Data>(Func<MemberInfo, Data> Member, Func<PropertyInfo, Data> Property);

    public static IEnumerable<KeyValuePair<Data, Member>> PublicStaticValuesOf<Container, Member, Data>(Resolvers<Data> resolvers)
    {
        Type containerType = typeof(Container);
        Type memberType = typeof(Member);
        FieldInfo[] infos = containerType.GetFields(BindingFlags.Public | System.Reflection.BindingFlags.Static);
        foreach (FieldInfo member in infos)
        {
            if (member.FieldType == memberType)
            {
                var val = member.GetValue(null);
                if (val == null) { continue; }
                yield return new KeyValuePair<Data, Member>(resolvers.Member(member), (Member)val);
            }
        }
        var pinfos = containerType.GetProperties(BindingFlags.Public | BindingFlags.Static);
        foreach (PropertyInfo property in pinfos)
        {
            if (property.DeclaringType == memberType)
            {
                var val = property.GetValue(null, null);
                if (val == null) { continue; }
                yield return new KeyValuePair<Data, Member>(resolvers.Member(property), (Member)val);
            }
        }
    }

    public static Resolvers<string> NameResolver { get; } = new Resolvers<string>(mem => mem.Name, prop => prop.Name);
    public static Resolvers<Att> Atributes<Att>()
        where Att : Attribute
    {
        static Att GetAttribute(IEnumerable<Attribute> attributes)
        {
            foreach (var attribute in attributes)
            {
                if (attribute is Att my)
                {
                    return my;
                }
            }

            // how should missing attributes be handled?
            throw new NotImplementedException();
        }

        return new Resolvers<Att>
            (
                mem => GetAttribute(mem.GetCustomAttributes()),
                prop => GetAttribute(prop.GetCustomAttributes())
            );
    }

    public static IEnumerable<KeyValuePair<Data, Container>> PublicStaticValuesOf<Container, Data>(Resolvers<Data> resolvers)
    {
        return PublicStaticValuesOf<Container, Container, Data>(resolvers);
    }
}
