using BeaverSoft.Texo.Core.Configuration;

namespace Commands.CodeBaseSearch
{
    public static class CodeBaseSearchBuilder
    {
        public static Query BuildCommand()
        {
            var command = Query.CreateBuilder();
            command.Key = CodeBaseSearchConstants.COMMAND_NAME;
            command.DefaultQueryKey = CodeBaseSearchConstants.QUERY_SEARCH;
            command.Representations.AddRange(
                new[] { CodeBaseSearchConstants.COMMAND_NAME, "code-search", "search", "s" });
            command.Documentation.Title = "Code-base search";
            command.Documentation.Description = "Cobe-base search with context and rule definitions.";

            var search = Query.CreateBuilder();
            search.Key = CodeBaseSearchConstants.QUERY_SEARCH;
            search.Representations.AddRange(
                new[] { CodeBaseSearchConstants.QUERY_SEARCH });
            search.Documentation.Title = "code-base-search";
            search.Documentation.Description = "Process a search in code-base.";

            var categories = Query.CreateBuilder();
            categories.Key = CodeBaseSearchConstants.QUERY_CATEGORIES;
            categories.Representations.AddRange(
                new[] { CodeBaseSearchConstants.QUERY_CATEGORIES, "category", "cat", "c" });
            categories.Documentation.Title = "code-base-categories";
            categories.Documentation.Description = "Shows a list of all registered categories.";

            command.Queries.AddRange(
                new[]
                {
                    search.ToImmutable(),
                    categories.ToImmutable(),
                });

            return command.ToImmutable();
        }
    }
}
