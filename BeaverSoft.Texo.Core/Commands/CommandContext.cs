using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using BeaverSoft.Texo.Core.Path;

namespace BeaverSoft.Texo.Core.Commands
{
    public partial class CommandContext
    {
        private readonly ImmutableList<string> queryPath;
        private readonly ImmutableDictionary<string, OptionContext> options;
        private readonly ImmutableDictionary<string, ParameterContext> parameters;

        private CommandContext()
        {
            queryPath = ImmutableList<string>.Empty;
            options = ImmutableDictionary<string, OptionContext>.Empty;
            parameters = ImmutableDictionary<string, ParameterContext>.Empty;
        }

        private CommandContext(CommandContext toClone)
        {
            Key = toClone.Key;
            IsValid = toClone.IsValid;
            queryPath = toClone.queryPath;
            options = toClone.options;
            parameters = toClone.parameters;
        }

        private CommandContext(Builder builder)
        {
            Key = builder.Key;
            IsValid = builder.IsValid;
            queryPath = builder.QueryPath.ToImmutable();
            options = builder.Options.ToImmutable();
            parameters = builder.Parameters.ToImmutable();
        }

        public string Key { get; }

        public bool IsValid { get; }

        public ImmutableList<string> QueryPath => queryPath;

        public ImmutableDictionary<string, OptionContext> Options => options;

        public ImmutableDictionary<string, ParameterContext> Parameters => parameters;

        // TODO: [P2] Should be setup on context build
        public CancellationToken CancellationToken { get; internal set; }

        public static CommandContext Empty { get; } = new CommandContext();

        public string FirstQuery => QueryPath.FirstOrDefault() ?? string.Empty;

        public bool HasParameter(string parameterKey)
        {
            return parameters.ContainsKey(parameterKey);
        }

        public bool HasPath()
        {
            return HasParameter(ParameterKeys.PATH);
        }

        public string GetParameterValue(string parameterKey)
        {
            if (!parameters.TryGetValue(parameterKey, out ParameterContext parameter))
            {
                return string.Empty;
            }

            return parameter.GetValue();
        }

        public IImmutableList<string> GetParameterValues(string parameterKey)
        {
            if (!parameters.TryGetValue(parameterKey, out ParameterContext parameter))
            {
                return ImmutableList<string>.Empty;
            }

            return parameter.GetValues();
        }

        public TexoPath GetParameterPath()
        {
            string stringPath = GetParameterValue(ParameterKeys.PATH);

            if (string.IsNullOrEmpty(stringPath))
            {
                return null;
            }

            return new TexoPath(stringPath);
        }

        public IEnumerable<TexoPath> GetParameterPaths()
        {
            foreach (string stringPath in GetParameterValues(ParameterKeys.PATH))
            {
                if (string.IsNullOrEmpty(stringPath))
                {
                    continue;
                }

                yield return new TexoPath(stringPath);
            }
        }

        public bool HasOption(string optionKey)
        {
            return options.ContainsKey(optionKey);
        }

        public OptionContext GetOption(string optionKey)
        {
            options.TryGetValue(optionKey, out OptionContext result);
            return result;
        }

        public string GetParameterFromOption(string optionKey)
        {
            if (!options.TryGetValue(optionKey, out OptionContext option))
            {
                return string.Empty;
            }

            return option.GetParameterValue();
        }

        public Builder ToBuilder()
        {
            return new Builder(this);
        }
    }
}
