using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DevExpress.Diagram.Core;
using DevExpress.XtraDiagram;

namespace PCTP.Shell.Help
{
    /// <summary>
    /// Renders the supported WMS Mermaid flowchart subset directly with DevExpress DiagramControl.
    /// This keeps help diagrams offline and avoids a browser/JavaScript dependency.
    /// </summary>
    internal static class WmsMermaidDiagramRenderer
    {
        private sealed class NodeDefinition
        {
            internal string Id;
            internal string Label;
            internal ShapeDescription Shape;
        }

        private sealed class EdgeDefinition
        {
            internal string From;
            internal string To;
            internal string Label;
        }

        private static readonly Regex EdgeRegex = new Regex(
            @"(?<from>[A-Za-z_][A-Za-z0-9_-]*)\s*(?<arrow>-->|==>|-\.->|---)\s*(?:\|(?<label>[^|]+)\|\s*)?(?<to>[A-Za-z_][A-Za-z0-9_-]*)",
            RegexOptions.Compiled);

        private static readonly Regex NodeRegex = new Regex(
            @"(?<id>[A-Za-z_][A-Za-z0-9_-]*)\s*(?<shape>\[[^\]]*\]|\([^\)]*\)|\{[^\}]*\})",
            RegexOptions.Compiled);

        internal static bool Render(string mermaid, DiagramControl diagram, out string error)
        {
            error = null;

            if (diagram == null)
            {
                error = "DiagramControl chưa được khởi tạo.";
                return false;
            }

            diagram.BeginUpdate();
            try
            {
                diagram.Items.Clear();

                List<NodeDefinition> nodes;
                List<EdgeDefinition> edges;
                Parse(mermaid, out nodes, out edges);

                if (nodes.Count == 0)
                {
                    error = "Không tìm thấy node Mermaid để dựng sơ đồ.";
                    return false;
                }

                Dictionary<string, DiagramShape> shapes = new Dictionary<string, DiagramShape>(StringComparer.OrdinalIgnoreCase);

                foreach (NodeDefinition node in nodes)
                {
                    DiagramShape shape = new DiagramShape();
                    shape.Shape = node.Shape;
                    shape.Content = node.Label;
                    shape.Size = new System.Drawing.SizeF(190F, 58F);
                    shape.CanEdit = false;
                    shape.CanMove = false;
                    shape.CanResize = false;
                    shape.CanDelete = false;
                    shape.CanCopy = false;
                    shapes[node.Id] = shape;
                    diagram.Items.Add(shape);
                }

                foreach (EdgeDefinition edge in edges)
                {
                    DiagramShape from;
                    DiagramShape to;
                    if (!shapes.TryGetValue(edge.From, out from) || !shapes.TryGetValue(edge.To, out to))
                        continue;

                    DiagramConnector connector = new DiagramConnector(from, to);
                    connector.EndArrow = ArrowDescriptions.Filled90;
                    connector.Content = edge.Label ?? string.Empty;
                    connector.CanEdit = false;
                    connector.CanMove = false;
                    connector.CanDelete = false;
                    diagram.Items.Add(connector);
                }

                if (diagram.Items.Count > 0)
                {
                    try
                    {
                        diagram.ApplySugiyamaLayout(Direction.Down, diagram.Items);
                    }
                    catch
                    {
                        diagram.ApplyTreeLayout(Direction.Down, diagram.Items);
                    }

                    diagram.AlignPage(null, null);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                diagram.EndUpdate();
            }
        }

        private static void Parse(string mermaid, out List<NodeDefinition> nodes, out List<EdgeDefinition> edges)
        {
            nodes = new List<NodeDefinition>();
            edges = new List<EdgeDefinition>();

            if (string.IsNullOrWhiteSpace(mermaid))
                return;

            Dictionary<string, NodeDefinition> map = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);
            string[] lines = mermaid.Replace("\r", string.Empty).Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("%%", StringComparison.Ordinal))
                    continue;
                if (line.StartsWith("flowchart ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("graph ", StringComparison.OrdinalIgnoreCase))
                    continue;

                MatchCollection nodeMatches = NodeRegex.Matches(line);
                foreach (Match match in nodeMatches)
                {
                    string id = match.Groups["id"].Value;
                    string token = match.Groups["shape"].Value;
                    string label = ExtractLabel(token);
                    ShapeDescription shape = ResolveShape(token);

                    NodeDefinition existing;
                    if (!map.TryGetValue(id, out existing))
                    {
                        existing = new NodeDefinition
                        {
                            Id = id,
                            Label = label.Length == 0 ? id : label,
                            Shape = shape
                        };
                        map.Add(id, existing);
                    }
                    else if (!string.IsNullOrWhiteSpace(label))
                    {
                        existing.Label = label;
                        existing.Shape = shape;
                    }
                }

                MatchCollection edgeMatches = EdgeRegex.Matches(line);
                foreach (Match match in edgeMatches)
                {
                    string from = match.Groups["from"].Value;
                    string to = match.Groups["to"].Value;
                    string label = match.Groups["label"].Success ? CleanLabel(match.Groups["label"].Value) : string.Empty;

                    EnsureNode(map, from);
                    EnsureNode(map, to);
                    edges.Add(new EdgeDefinition { From = from, To = to, Label = label });
                }
            }

            nodes.AddRange(map.Values);
        }

        private static void EnsureNode(Dictionary<string, NodeDefinition> map, string id)
        {
            if (map.ContainsKey(id))
                return;

            map.Add(id, new NodeDefinition
            {
                Id = id,
                Label = id,
                Shape = BasicFlowchartShapes.Process
            });
        }

        private static string ExtractLabel(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length < 2)
                return string.Empty;

            string value = token.Substring(1, token.Length - 2);
            return CleanLabel(value);
        }

        private static string CleanLabel(string value)
        {
            return (value ?? string.Empty)
                .Replace("<br/>", " ")
                .Replace("<br>", " ")
                .Replace("<br />", " ")
                .Trim();
        }

        private static ShapeDescription ResolveShape(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BasicFlowchartShapes.Process;

            if (token[0] == '{')
                return BasicFlowchartShapes.Decision;

            if (token[0] == '(')
                return BasicFlowchartShapes.StartEnd;

            return BasicFlowchartShapes.Process;
        }
    }
}
