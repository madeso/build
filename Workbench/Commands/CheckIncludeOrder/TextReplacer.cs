namespace Workbench.Commands.CheckIncludeOrder
{
    /// multi replace calls on a single text 
    public class TextReplacer
    {
        private record SingleReplacement(string From, string To);

        private readonly List<SingleReplacement> replacements = new();

        // add a replacement command 
        public void Add(string from, string to)
        {
            replacements.Add(new SingleReplacement(from, to));
        }

        public string Replace(string in_text)
            => replacements
                .Aggregate(in_text,
                    (current, replacement) => current.Replace(replacement.From, replacement.To));
    }
}