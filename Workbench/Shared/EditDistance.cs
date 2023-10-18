namespace Workbench.Shared;

public static class EditDistance
{
    public static IEnumerable<string> ClosestMatches(
        int count, string input, int max_diff, IEnumerable<string> candidates)
    {
        return candidates
            .Select(name => new { Name = name, Distance = Calculate(input, name) })
            .Where(entry => entry.Distance <= max_diff)
            .OrderBy(entry => entry.Distance)
            .Take(count)
            .Select(entry => entry.Name);
    }

    // edit distance implementation from: https://gist.github.com/Davidblkx/e12ab0bb2aff7fd8072632b396538560

    /// <summary>
    ///     Calculate the difference between 2 strings using the Levenshtein distance algorithm
    /// </summary>
    /// <param name="source1">First string</param>
    /// <param name="source2">Second string</param>
    /// <returns></returns>
    private static int Calculate(string source1, string source2) //O(n*m)
    {
        var source1_length = source1.Length;
        var source2_length = source2.Length;

        var matrix = new int[source1_length + 1, source2_length + 1];

        // First calculation, if one entry is empty return full length
        if (source1_length == 0)
            return source2_length;

        if (source2_length == 0)
            return source1_length;

        // Initialization of matrix with row size source1Length and columns size source2Length
        for (var i = 0; i <= source1_length; matrix[i, 0] = i++) { }
        for (var j = 0; j <= source2_length; matrix[0, j] = j++) { }

        // Calculate rows and columns distances
        for (var i = 1; i <= source1_length; i++)
        {
            for (var j = 1; j <= source2_length; j++)
            {
                var cost = source2[j - 1] == source1[i - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }
        // return result
        return matrix[source1_length, source2_length];
    }
}
