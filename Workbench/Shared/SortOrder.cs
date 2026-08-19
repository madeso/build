namespace Workbench.Shared;

public enum OrderDirection
{
    Ascending, Descending
}

public static class OrderDirectionUtil
{
    public static IEnumerable<T> InDirection<T>(this IEnumerable<T> items, OrderDirection direction)
    {
        return direction switch
        {
            OrderDirection.Ascending => items,
            OrderDirection.Descending => items.Reverse(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}