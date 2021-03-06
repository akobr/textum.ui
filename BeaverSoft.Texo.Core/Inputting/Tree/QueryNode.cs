using System;
using System.Collections.Generic;
using BeaverSoft.Texo.Core.Configuration;

namespace BeaverSoft.Texo.Core.Inputting.Tree
{
    public class QueryNode : ParameteriseNode
    {
        public QueryNode(Query query)
        {
            Query = query;

            Queries = new Dictionary<string, QueryNode>(StringComparer.OrdinalIgnoreCase);
            Options = new Dictionary<string, OptionNode>(StringComparer.OrdinalIgnoreCase);
            OptionList = new Dictionary<char, OptionNode>();
        }

        public Query Query { get; }

        public override NodeTypeEnum Type => NodeTypeEnum.Query;

        public QueryNode DefaultQuery { get; private set; }

        public Dictionary<string, QueryNode> Queries { get; }

        public Dictionary<string, OptionNode> Options { get; }

        public Dictionary<char, OptionNode> OptionList { get; }

        public void SetDefaultQuery(QueryNode defaultQuery)
        {
            DefaultQuery = defaultQuery;
        }

        public override string ToString()
        {
            return $"Query: {Query.Key}";
        }
    }

}