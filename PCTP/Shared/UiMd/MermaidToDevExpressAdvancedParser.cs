using DevExpress.Diagram.Core;
using DevExpress.Diagram.Core.InteractiveLayout;
using DevExpress.Diagram.Core.Layout;
using DevExpress.DirectX.Common;
using DevExpress.Utils;
using DevExpress.XtraDiagram;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PCTP.Shared.UiMd
{
    public static class MermaidToDevExpressAdvancedParser
    {
        // C# 7.3 compatible.
        // Important: DiagramDoubleCollection does NOT use Add().
        // It must be created from an array, e.g.
        // new DiagramDoubleCollection(new double[] { 4.0, 3.0 });

        private static readonly Font NodeFont = new Font("Segoe UI Emoji", 9.0f, FontStyle.Regular);

        private static SizeF MeasureWrappedText(string text, Font font, float maxWidth)
        {
            if (string.IsNullOrEmpty(text))
                return new SizeF(maxWidth, 40f);

            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            {
                return g.MeasureString(text, font, Math.Max(40, (int)maxWidth));
            }
        }



        private sealed class NodeDef
        {
            public string Id;
            public string Text;
            public bool Decision;
            public string GroupId;
        }

        private sealed class EdgeDef
        {
            public string Source;
            public string Target;
            public string Label;
            public string Operator;
        }

        private sealed class GroupDef
        {
            public string Id;
            public string Title;
            public int Depth;
            public GroupDef Parent;
            public readonly List<string> NodeIds = new List<string>();
            public readonly List<GroupDef> Children = new List<GroupDef>();
        }

        private static readonly Regex SubgraphRegex = new Regex(
            @"^\s*subgraph\s+(?:(?<id>[A-Za-z_][A-Za-z0-9_-]*)\s*\[\s*""(?<title>.*?)""\s*\]|(?<plain>.+?))\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex NodeRegex = new Regex(
            @"(?<id>[A-Za-z_][A-Za-z0-9_-]*)(?:" +
            @"\(\(""(?<circle>.*?)""\)\)" +
            @"|\{""(?<decision>.*?)""\}" +
            @"|\[""(?<rect>.*?)""\]" +
            @"|\(""(?<round>.*?)""\)" +
            @"|\((?<roundRaw>.*?)\)" +
            @"|\[(?<rectRaw>.*?)\]" +
            @"|\{(?<decisionRaw>.*?)\})",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex EdgeRegex = new Regex(
            @"(?<src>[A-Za-z_][A-Za-z0-9_-]*)\s*" +
            @"(?<op>-->|-.->|==>|---|-.-)\s*" +
            @"(?:\|(?<label>.*?)\|\s*)?" +
            @"(?<dst>[A-Za-z_][A-Za-z0-9_-]*)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex StyleRegex = new Regex(
            @"^\s*style\s+(?<id>[A-Za-z_][A-Za-z0-9_-]*)\s+(?<props>.+?)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ClassDefRegex = new Regex(
            @"^\s*classDef\s+(?<name>[A-Za-z_][A-Za-z0-9_-]*)\s+(?<props>.+?)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ClassApplyRegex = new Regex(
            @"^\s*class\s+(?<ids>[A-Za-z0-9_,\-\s]+)\s+(?<name>[A-Za-z_][A-Za-z0-9_-]*)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FormNameRegex = new Regex(
            @"\bForm[A-Za-z0-9_]+\b",
            RegexOptions.Compiled);
        public static void ParseAndBuildDiagram(
            DiagramControl diagram,
            string mermaidText,
            bool autoDetectDirection = true)
        {
            if (diagram == null || string.IsNullOrWhiteSpace(mermaidText))
                return;

            diagram.BeginUpdate();

            try
            {
                diagram.Items.Clear();

                List<string> lines = NormalizeLines(mermaidText);

                string mermaidDirectionToken = null;

                foreach (string rawLine in lines)
                {
                    string lower = rawLine.ToLowerInvariant();

                    if (lower.StartsWith("graph ", StringComparison.Ordinal) ||
                        lower.StartsWith("flowchart ", StringComparison.Ordinal))
                    {
                        string[] parts = rawLine.Split(
                            new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Length >= 2)
                            mermaidDirectionToken = parts[1];

                        break;
                    }
                }

                var nodes = new Dictionary<string, NodeDef>(StringComparer.OrdinalIgnoreCase);
                var edges = new List<EdgeDef>();
                var groups = new List<GroupDef>();
                var groupById = new Dictionary<string, GroupDef>(StringComparer.OrdinalIgnoreCase);
                var rootGroups = new List<GroupDef>();
                var groupStack = new Stack<GroupDef>();
                var styles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var classDefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var classApplications = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                ParseMermaid(
                    lines, nodes, edges, groups, groupById,
                    rootGroups, groupStack, styles, classDefs, classApplications);

                // ============================================================
                // 1. Tạo container TRƯỚC, theo thứ tự từ nông -> sâu (Depth tăng dần).
                //    Container con được add vào container cha ngay lúc tạo,
                //    KHÔNG add vào diagram.Items nếu nó có cha.
                // ============================================================

                var containerMap = new Dictionary<GroupDef, DiagramContainer>();

                foreach (GroupDef group in groups.OrderBy(g => g.Depth))
                {
                    var container = new DiagramContainer
                    {
                        Shape = StandardContainers.Classic,
                        Header = group.Title,
                        ShowHeader = true,
                        CanAddItems = true,
                        ItemsCanChangeParent = true,
                        // Kích thước khởi tạo chỉ là placeholder,
                        // layout bên dưới sẽ tự tính lại theo nội dung.
                        AdjustBoundsBehavior = AdjustBoundaryBehavior.AutoAdjust,
                        //Width = 240.0f,
                        //Height = 140.0f
                    };

                    container.HeaderPadding = new Padding(8, 6, 8, 6);
                    container.Padding = new Padding(15, 22, 15, 15); // top=22 để chừa khoảng dưới header

                    container.Appearance.BackColor = Color.FromArgb(248, 249, 250);
                    container.Appearance.BorderColor = Color.FromArgb(190, 198, 208);
                    container.Appearance.BorderSize = 1;

                    container.Appearance.ForeColor = GetContrastingTextColor(container.Appearance.BackColor);
                    container.Appearance.Options.UseForeColor = true;

                    container.Appearance.Options.UseBackColor = true;
                    container.Appearance.Options.UseBorderColor = true;
                    container.Appearance.Options.UseBorderSize = true;

                    // Đổi Font Header của Container sang Segoe UI Emoji để hiện Icon trên tiêu đề Group
                    container.Appearance.Font = new Font("Segoe UI Emoji", 9.0f, FontStyle.Bold);
                    container.Appearance.Options.UseFont = true;

                    if (group.Parent != null &&
                        containerMap.TryGetValue(group.Parent, out DiagramContainer parentContainer))
                    {
                        parentContainer.Items.Add(container);
                    }
                    else
                    {
                        diagram.Items.Add(container);
                    }

                    containerMap[group] = container;
                }

                // ============================================================
                // 2. Tạo shape, add vào ĐÚNG container của nó (nếu có),
                //    hoặc vào diagram.Items nếu không thuộc group nào.
                // ============================================================

                var shapeMap = new Dictionary<string, DiagramShape>(StringComparer.OrdinalIgnoreCase);

                foreach (NodeDef node in nodes.Values)
                {
                    DiagramShape shape = CreateShape(node);

                    if (styles.TryGetValue(node.Id, out string styleText))
                        ApplyStyle(shape, styleText);

                    if (classApplications.TryGetValue(node.Id, out string className) &&
                        classDefs.TryGetValue(className, out string classProps))
                    {
                        ApplyStyle(shape, classProps);
                    }

                    GroupDef ownerGroup = null;

                    if (node.GroupId != null)
                        groupById.TryGetValue(node.GroupId, out ownerGroup);

                    if (ownerGroup != null && containerMap.TryGetValue(ownerGroup, out DiagramContainer owner))
                        owner.Items.Add(shape);
                    else
                        diagram.Items.Add(shape);

                    shapeMap[node.Id] = shape;
                }

                // ============================================================
                // 3. Tạo Connector, lưu vào map để dùng lại ở cả layout nội bộ
                //    lẫn layout cấp gốc. Connector luôn add vào diagram.Items (root)
                //    — DevExpress tự resolve theo tham chiếu Source/Target.
                // ============================================================

                var connectorMap = new Dictionary<EdgeDef, DiagramConnector>();

                foreach (EdgeDef edge in edges)
                {
                    if (!shapeMap.TryGetValue(edge.Source, out DiagramShape source) ||
                        !shapeMap.TryGetValue(edge.Target, out DiagramShape target))
                        continue;

                    DiagramConnector connector = CreateConnector(source, target, edge);
                    connectorMap[edge] = connector;
                    diagram.Items.Add(connector);
                }

                // ============================================================
                // 4. Khoảng cách layer/node.
                // ============================================================

                diagram.OptionsSugiyamaLayout.ColumnSpacing = 80;
                diagram.OptionsSugiyamaLayout.LayerSpacing = 60;

                LayoutDirection direction = DecideLayoutDirection(
                    nodes, edges, mermaidDirectionToken, autoDetectDirection);

                // ============================================================
                // 5. QUAN TRỌNG: DevExpress KHÔNG tự layout item bên trong container.
                //    Phải tự layout đệ quy: group lá trước, group cha sau.
                // ============================================================

                var consumedEdges = new HashSet<EdgeDef>();

                foreach (GroupDef rootGroup in rootGroups)
                {
                    LayoutGroupRecursive(
                        diagram, rootGroup, containerMap, shapeMap,
                        connectorMap, edges, consumedEdges, direction);
                }

                // ============================================================
                // 6. Layout cấp gốc: shape không thuộc group nào + container gốc
                //    (đã có kích thước thật từ bước 5, DevExpress coi nó là 1 node)
                //    + connector CHƯA bị "tiêu thụ" ở bước 5 (connector liên nhóm).
                // ============================================================

                var rootItems = new List<DiagramItem>();

                foreach (NodeDef node in nodes.Values)
                {
                    if (node.GroupId == null && shapeMap.TryGetValue(node.Id, out DiagramShape rootShape))
                        rootItems.Add(rootShape);
                }

                foreach (GroupDef rootGroup in rootGroups)
                {
                    if (containerMap.TryGetValue(rootGroup, out DiagramContainer rootContainer))
                        rootItems.Add(rootContainer);
                }
                // ============================================================
                // 6.5. QUAN TRỌNG: việc Remove/Add shape qua lại giữa container
                //      và diagram.Items ở bước 5 (layout nội bộ từng group) có thể
                //      khiến DevExpress tự động xoá các connector đang gắn vào
                //      shape đó (cascade-delete khi Remove khỏi Items, để tránh
                //      tham chiếu treo). Vì vậy KHÔNG dựa vào connector tạo ở bước 3
                //      còn sống sót — dọn sạch chúng và tạo lại 1 lần cuối, khi mọi
                //      vị trí đã ổn định và không còn shape nào bị di chuyển nữa.
                // ============================================================

                foreach (DiagramConnector oldConnector in connectorMap.Values)
                {
                    if (diagram.Items.Contains(oldConnector))
                        diagram.Items.Remove(oldConnector);
                }

                foreach (EdgeDef edge in edges)
                {
                    if (!shapeMap.TryGetValue(edge.Source, out DiagramShape source) ||
                        !shapeMap.TryGetValue(edge.Target, out DiagramShape target))
                        continue;

                    DiagramConnector connector = CreateConnector(source, target, edge);
                    diagram.Items.Add(connector);
                }

                // Sắp root items theo thứ tự luồng chính (topological), dùng lại
                // ComputeNodeLayers nhưng trên danh sách rootItems đã gộp nhóm.
                var rootLayers = ComputeGroupedLayers(nodes, edges); // hàm mới, xem bên dưới

                List<DiagramItem> orderedRootItems = rootItems
                    .OrderBy(item =>
                    {
                        string key = item is DiagramContainer c
                            ? "GROUP::" + containerMap.First(kv => kv.Value == c).Key.Id
                            : (string)((DiagramShape)item).Tag;

                        return rootLayers.TryGetValue(key, out int l) ? l : 0;
                    })
                    .ToList();

                ArrangeRootItemsWrapped(
                    orderedRootItems,
                    targetRowCount: 1,      // chỉnh số này để rộng/hẹp theo ý bạn
                    horizontalSpacing: 80,
                    verticalSpacing: 60);

                // ============================================================
                // 7. Fit toàn bộ diagram.
                // ============================================================

                if (diagram.Items.Count > 0)
                {
                    // Cài đặt lề (margin) xung quanh sơ đồ (ví dụ 10-20px)
                    diagram.OptionsView.FitToDrawingMargin = new System.Windows.Forms.Padding(15);

                    // Fit toàn bộ các item vừa khít tầm nhìn DiagramControl
                    diagram.FitToItems(diagram.Items);

                    // Bật thanh cuộn nếu sơ đồ rộng hơn Form
                    //diagram.OptionsBehavior.ScrollMode = DevExpress.XtraDiagram.DiagramScrollMode.Pixel;
                }
            }
            finally
            {
                diagram.EndUpdate();
            }
        }
        private static void LayoutGroupRecursive(
    DiagramControl diagram,
    GroupDef group,
    Dictionary<GroupDef, DiagramContainer> containerMap,
    Dictionary<string, DiagramShape> shapeMap,
    Dictionary<EdgeDef, DiagramConnector> connectorMap,
    List<EdgeDef> edges,
    HashSet<EdgeDef> consumedEdges,
    LayoutDirection direction)
        {
            // Layout group con TRƯỚC (lá -> gốc).
            foreach (GroupDef child in group.Children)
            {
                LayoutGroupRecursive(
                    diagram, child, containerMap, shapeMap,
                    connectorMap, edges, consumedEdges, direction);
            }

            if (!containerMap.TryGetValue(group, out DiagramContainer container))
                return;

            var idSet = new HashSet<string>(group.NodeIds, StringComparer.OrdinalIgnoreCase);

            // Shape/container con TRỰC TIẾP của group này (đang nằm trong container.Items).
            var childShapes = new List<DiagramItem>();

            foreach (string nodeId in group.NodeIds)
            {
                if (shapeMap.TryGetValue(nodeId, out DiagramShape shape))
                    childShapes.Add(shape);
            }

            foreach (GroupDef child in group.Children)
            {
                if (containerMap.TryGetValue(child, out DiagramContainer childContainer))
                    childShapes.Add(childContainer);
            }

            if (childShapes.Count == 0)
                return;

            // Connector "nội bộ": cả 2 đầu đều thuộc group này.
            var innerConnectors = new List<DiagramItem>();

            foreach (EdgeDef edge in edges)
            {
                if (consumedEdges.Contains(edge))
                    continue;

                if (idSet.Contains(edge.Source) && idSet.Contains(edge.Target) &&
                    connectorMap.TryGetValue(edge, out DiagramConnector connector))
                {
                    innerConnectors.Add(connector);
                    consumedEdges.Add(edge);
                }
            }

            // ------------------------------------------------------------------
            // BƯỚC QUAN TRỌNG NHẤT: DevExpress chỉ layout được item KHÔNG có
            // parent là container. Phải "nhấc" ra root, layout xong mới đưa lại.
            // ------------------------------------------------------------------

            foreach (DiagramItem item in childShapes)
            {
                container.Items.Remove(item);
                diagram.Items.Add(item);
            }

            var layoutItems = new List<DiagramItem>(childShapes);
            layoutItems.AddRange(innerConnectors);

            diagram.ApplySugiyamaLayout(direction, layoutItems);

            // Đo bounding box theo tọa độ global (item đang ở root).
            float minX = childShapes.Min(x => x.Position.X);
            float minY = childShapes.Min(x => x.Position.Y);
            float maxX = childShapes.Max(x => x.Position.X + x.Width);
            float maxY = childShapes.Max(x => x.Position.Y + x.Height);

            // Đưa item trở lại container: set Position cục bộ TRƯỚC khi Add,
            // vì Add() không tự quy đổi hệ tọa độ.
            foreach (DiagramItem item in childShapes)
            {
                float localX = item.Position.X - minX + container.Padding.Left;
                float localY = item.Position.Y - minY + container.Padding.Top;

                diagram.Items.Remove(item);

                item.Position = new DevExpress.Utils.PointFloat(localX, localY);

                container.Items.Add(item);
            }

            container.Width = Math.Max(
                200f,
                (maxX - minX) + container.Padding.Left + container.Padding.Right);

            container.Height = Math.Max(
                120f,
                (maxY - minY) + container.Padding.Top + container.Padding.Bottom);
        }
        private static void ArrangeRootItemsWrapped(
    List<DiagramItem> orderedItems,
    int targetRowCount, // Số dòng bạn muốn (VD: truyền vào 2 hoặc 3)
    float horizontalSpacing,
    float verticalSpacing)
        {
            if (orderedItems.Count == 0) return;

            // 1. Tính tổng độ rộng của tất cả các Container/Item gốc
            float totalWidth = orderedItems.Sum(i => i.Width) + (orderedItems.Count - 1) * horizontalSpacing;

            // 2. Độ rộng lý thuyết mục tiêu cho mỗi hàng
            float targetRowWidth = totalWidth / Math.Max(1, targetRowCount);

            var rows = new List<List<DiagramItem>>();
            var currentRow = new List<DiagramItem>();
            float currentRowWidth = 0f;

            foreach (DiagramItem item in orderedItems)
            {
                // Khi hàng hiện tại đã vượt targetRowWidth và đã có ít nhất 1 item -> Chuyển sang hàng mới
                if (currentRow.Count > 0 && (currentRowWidth + item.Width) > targetRowWidth)
                {
                    rows.Add(currentRow);
                    currentRow = new List<DiagramItem>();
                    currentRowWidth = 0f;
                }

                currentRow.Add(item);
                currentRowWidth += item.Width + horizontalSpacing;
            }

            if (currentRow.Count > 0)
                rows.Add(currentRow);

            // 3. Tiến hành xếp vị trí theo các hàng đã phân chia
            float y = 0f;

            for (int r = 0; r < rows.Count; r++)
            {
                List<DiagramItem> itemsInRow = rows[r];

                // Hàng lẻ đảo ngược chiều (Rắn bò - Snake Flow)
                if (r % 2 == 1)
                    itemsInRow.Reverse();

                float x = 0f;
                float maxHeightInRow = itemsInRow.Max(i => i.Height);

                foreach (DiagramItem item in itemsInRow)
                {
                    item.Position = new DevExpress.Utils.PointFloat(x, y);
                    x += item.Width + horizontalSpacing;
                }

                y += maxHeightInRow + verticalSpacing;
            }
        }
        //    private static void ArrangeRootItemsWrapped(
        //List<DiagramItem> orderedItems,   // đã sắp theo thứ tự luồng chính
        //int maxColumnsPerRow,
        //float horizontalSpacing,
        //float verticalSpacing)
        //    {
        //        if (orderedItems.Count == 0) return;

        //        var rows = new List<List<DiagramItem>>();
        //        var currentRow = new List<DiagramItem>();

        //        foreach (DiagramItem item in orderedItems)
        //        {
        //            currentRow.Add(item);
        //            if (currentRow.Count >= maxColumnsPerRow)
        //            {
        //                rows.Add(currentRow);
        //                currentRow = new List<DiagramItem>();
        //            }
        //        }
        //        if (currentRow.Count > 0) rows.Add(currentRow);

        //        float y = 0f;

        //        for (int r = 0; r < rows.Count; r++)
        //        {
        //            List<DiagramItem> itemsInRow = rows[r];

        //            // Hàng lẻ đi ngược chiều (kiểu "rắn bò") để luồng đọc mượt hơn
        //            // khi mắt phải nhảy từ cuối hàng trên xuống đầu hàng dưới.
        //            if (r % 2 == 1)
        //                itemsInRow.Reverse();

        //            float x = 0f;
        //            float maxHeightInRow = itemsInRow.Max(i => i.Height);

        //            foreach (DiagramItem item in itemsInRow)
        //            {
        //                item.Position = new DevExpress.Utils.PointFloat(x, y);
        //                x += item.Width + horizontalSpacing;
        //            }

        //            y += maxHeightInRow + verticalSpacing;
        //        }
        //    }
        private static Dictionary<string, int> ComputeGroupedLayers(
    Dictionary<string, NodeDef> nodes,
    List<EdgeDef> edges)
        {
            string Collapse(NodeDef n) => n.GroupId == null ? n.Id : "GROUP::" + n.GroupId;

            var ids = new HashSet<string>(nodes.Values.Select(Collapse));
            var adjacency = ids.ToDictionary(id => id, id => new List<string>(), StringComparer.OrdinalIgnoreCase);
            var inDegree = ids.ToDictionary(id => id, id => 0, StringComparer.OrdinalIgnoreCase);

            foreach (EdgeDef e in edges)
            {
                if (!nodes.TryGetValue(e.Source, out NodeDef sn) || !nodes.TryGetValue(e.Target, out NodeDef tn))
                    continue;

                string a = Collapse(sn);
                string b = Collapse(tn);
                if (a == b) continue; // cạnh nội bộ trong cùng group -> bỏ qua

                adjacency[a].Add(b);
                inDegree[b]++;
            }

            var layer = ids.ToDictionary(id => id, id => 0, StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            int guard = ids.Count * ids.Count + 10;

            while (queue.Count > 0 && guard-- > 0)
            {
                string cur = queue.Dequeue();
                foreach (string next in adjacency[cur])
                {
                    if (layer[next] < layer[cur] + 1) layer[next] = layer[cur] + 1;
                    if (--inDegree[next] == 0) queue.Enqueue(next);
                }
            }

            return layer;
        }
        // ================================================================
        // Tính layer (tầng) của từng node bằng longest-path layering,
        // dùng để ước lượng "depth" (số tầng) và "breadth" (số node/tầng rộng nhất).
        // ================================================================
        private static Dictionary<string, int> ComputeNodeLayers(
            Dictionary<string, NodeDef> nodes,
            List<EdgeDef> edges)
        {
            var layer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var remainingInDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string id in nodes.Keys)
            {
                adjacency[id] = new List<string>();
                remainingInDegree[id] = 0;
                layer[id] = 0;
            }

            foreach (EdgeDef edge in edges)
            {
                if (!nodes.ContainsKey(edge.Source) || !nodes.ContainsKey(edge.Target))
                    continue;

                adjacency[edge.Source].Add(edge.Target);
                remainingInDegree[edge.Target]++;
            }

            var queue = new Queue<string>(
                remainingInDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

            // Bảo vệ vòng lặp vô hạn nếu mermaid lỡ có chu trình (cycle).
            int guard = nodes.Count * nodes.Count + 10;

            while (queue.Count > 0 && guard-- > 0)
            {
                string current = queue.Dequeue();

                foreach (string next in adjacency[current])
                {
                    if (layer[next] < layer[current] + 1)
                        layer[next] = layer[current] + 1;

                    remainingInDegree[next]--;

                    if (remainingInDegree[next] == 0)
                        queue.Enqueue(next);
                }
            }

            return layer;
        }

        private static LayoutDirection? MapMermaidDirection(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            switch (token.Trim().ToUpperInvariant())
            {
                case "TD":
                case "TB":
                    return LayoutDirection.TopToBottom;
                case "BT":
                    return LayoutDirection.BottomToTop;
                case "LR":
                    return LayoutDirection.LeftToRight;
                case "RL":
                    return LayoutDirection.RightToLeft;
                default:
                    return null;
            }
        }

        // depth > breadth * threshold => sơ đồ "dài mà hẹp" => nên xoay ngang.
        private const double AutoDirectionThreshold = 1.4;

        private static LayoutDirection DecideLayoutDirection(
            Dictionary<string, NodeDef> nodes,
            List<EdgeDef> edges,
            string mermaidDirectionToken,
            bool autoDetectDirection)
        {
            if (!autoDetectDirection)
                return MapMermaidDirection(mermaidDirectionToken) ?? LayoutDirection.TopToBottom;

            Dictionary<string, int> layers = ComputeNodeLayers(nodes, edges);

            if (layers.Count == 0)
                return LayoutDirection.TopToBottom;

            int depth = layers.Values.Max() + 1;

            int breadth = layers.Values
                .GroupBy(v => v)
                .Max(g => g.Count());

            return (depth > breadth * AutoDirectionThreshold)
                ? LayoutDirection.LeftToRight
                : LayoutDirection.TopToBottom;
        }

        private static List<string> NormalizeLines(string mermaidText)
        {
            string normalized = mermaidText
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            string[] rawLines = normalized.Split('\n');
            var result = new List<string>();

            foreach (string rawLine in rawLines)
            {
                string line = rawLine.Trim();

                if (line.Length == 0)
                    continue;

                if (line.StartsWith("%%", StringComparison.Ordinal))
                    continue;

                result.Add(line);
            }

            return result;
        }

        private static void ParseMermaid(
            List<string> lines,
            Dictionary<string, NodeDef> nodes,
            List<EdgeDef> edges,
            List<GroupDef> groups,
            Dictionary<string, GroupDef> groupById,
            List<GroupDef> rootGroups,
            Stack<GroupDef> groupStack,
            Dictionary<string, string> styles,
            Dictionary<string, string> classDefs,
            Dictionary<string, string> classApplications)
        {
            foreach (string line in lines)
            {
                string lower = line.ToLowerInvariant();

                if (lower == "graph td" ||
                    lower == "graph tb" ||
                    lower == "graph bt" ||
                    lower == "graph lr" ||
                    lower == "graph rl" ||
                    lower.StartsWith("graph ", StringComparison.Ordinal) ||
                    lower.StartsWith("flowchart ", StringComparison.Ordinal))
                {
                    continue;
                }

                if (lower == "end")
                {
                    if (groupStack.Count > 0)
                        groupStack.Pop();

                    continue;
                }

                // ------------------------------------------------------------
                // subgraph
                // ------------------------------------------------------------
                if (lower.StartsWith("subgraph ", StringComparison.Ordinal))
                {
                    GroupDef group =
                        ParseSubgraph(line, groupStack.Count);

                    if (group != null)
                    {
                        GroupDef parent =
                            groupStack.Count > 0
                                ? groupStack.Peek()
                                : null;

                        group.Parent = parent;

                        if (parent != null)
                            parent.Children.Add(group);
                        else
                            rootGroups.Add(group);

                        groups.Add(group);

                        string uniqueId = group.Id;
                        int suffix = 2;

                        while (groupById.ContainsKey(uniqueId))
                        {
                            uniqueId = group.Id + "_" + suffix;
                            suffix++;
                        }

                        group.Id = uniqueId;
                        groupById[uniqueId] = group;

                        groupStack.Push(group);
                    }

                    continue;
                }

                // ------------------------------------------------------------
                // style
                // ------------------------------------------------------------
                Match styleMatch = StyleRegex.Match(line);

                if (styleMatch.Success)
                {
                    styles[styleMatch.Groups["id"].Value] =
                        styleMatch.Groups["props"].Value;

                    continue;
                }

                // ------------------------------------------------------------
                // classDef
                // ------------------------------------------------------------
                Match classDefMatch = ClassDefRegex.Match(line);

                if (classDefMatch.Success)
                {
                    classDefs[classDefMatch.Groups["name"].Value] =
                        classDefMatch.Groups["props"].Value;

                    continue;
                }

                // ------------------------------------------------------------
                // class A,B className
                // ------------------------------------------------------------
                Match classApplyMatch = ClassApplyRegex.Match(line);

                if (classApplyMatch.Success)
                {
                    string className =
                        classApplyMatch.Groups["name"].Value;

                    string[] ids =
                        classApplyMatch.Groups["ids"].Value.Split(
                            new[] { ',' },
                            StringSplitOptions.RemoveEmptyEntries);

                    foreach (string rawId in ids)
                    {
                        string id = rawId.Trim();

                        if (id.Length > 0)
                            classApplications[id] = className;
                    }

                    continue;
                }

                // ------------------------------------------------------------
                // Node declarations
                // ------------------------------------------------------------
                foreach (Match nodeMatch in NodeRegex.Matches(line))
                {
                    string id = nodeMatch.Groups["id"].Value;

                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    string text = GetNodeText(nodeMatch);

                    if (text == null)
                        continue;

                    if (!nodes.ContainsKey(id))
                    {
                        var node = new NodeDef
                        {
                            Id = id,
                            Text = CleanMermaidText(text),
                            Decision =
                                nodeMatch.Groups["decision"].Success ||
                                nodeMatch.Groups["decisionRaw"].Success,
                            GroupId =
                                groupStack.Count > 0
                                    ? groupStack.Peek().Id
                                    : null
                        };

                        nodes.Add(id, node);

                        if (groupStack.Count > 0)
                            groupStack.Peek().NodeIds.Add(id);
                    }
                }

                // ------------------------------------------------------------
                // Edges
                // ------------------------------------------------------------
                foreach (Match edgeMatch in EdgeRegex.Matches(line))
                {
                    string source =
                        edgeMatch.Groups["src"].Value;

                    string target =
                        edgeMatch.Groups["dst"].Value;

                    if (string.IsNullOrWhiteSpace(source) ||
                        string.IsNullOrWhiteSpace(target))
                        continue;

                    edges.Add(new EdgeDef
                    {
                        Source = source,
                        Target = target,
                        Label =
                            CleanMermaidText(
                                edgeMatch.Groups["label"].Value),
                        Operator =
                            edgeMatch.Groups["op"].Value
                    });
                }
            }
        }

        private static GroupDef ParseSubgraph(
            string line,
            int depth)
        {
            Match match = SubgraphRegex.Match(line);

            if (!match.Success)
                return null;

            string id;
            string title;

            if (match.Groups["id"].Success)
            {
                id = match.Groups["id"].Value;

                title =
                    match.Groups["title"].Success
                        ? match.Groups["title"].Value
                        : id;
            }
            else
            {
                string plain =
                    match.Groups["plain"].Value.Trim();

                if (plain.Length >= 2 &&
                    plain[0] == '"' &&
                    plain[plain.Length - 1] == '"')
                {
                    title =
                        plain.Substring(
                            1,
                            plain.Length - 2);

                    id =
                        "subgraph_" +
                        Math.Abs(title.GetHashCode());
                }
                else
                {
                    title = plain;
                    id = plain;
                }
            }

            return new GroupDef
            {
                Id = id,
                Title = CleanMermaidText(title),
                Depth = depth
            };
        }

        private static string GetNodeText(Match match)
        {
            string[] names =
            {
            "circle",
            "decision",
            "rect",
            "round",
            "roundRaw",
            "rectRaw",
            "decisionRaw"
        };

            foreach (string name in names)
            {
                Group group = match.Groups[name];

                if (group.Success)
                    return group.Value;
            }

            return null;
        }

        private static float CalculateNodeWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 180.0f;

            // Đo độ dài dòng thực tế
            string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            int maxLength = lines.Length > 0 ? lines.Max(x => x.Length) : 0;

            // Tăng khung rộng hơn để chứa đủ text tiếng Việt và Icon/Emoji
            if (maxLength >= 60) return 320.0f;
            if (maxLength >= 40) return 260.0f;
            if (maxLength >= 25) return 220.0f;

            return 180.0f;
        }

        private static float CalculateNodeHeight(string text, float width)
        {
            if (string.IsNullOrEmpty(text))
                return 65.0f;

            float usableWidth = Math.Max(40f, width - 40f);
            SizeF measured = MeasureWrappedText(text, NodeFont, usableWidth);

            // Ước lượng số dòng tối thiểu dựa trên số ký tự xuống dòng thủ công
            // (từ <br/> trong mermaid) — đảm bảo không bao giờ thấp hơn mức này.
            int explicitLineCount = text.Split('\n').Length;
            float minHeightForLines = (explicitLineCount * NodeFont.Height * 1.4f) + 48.0f;

            float height = Math.Max((measured.Height * 1.4f) + 48.0f, minHeightForLines);

            if (height < 65.0f) height = 65.0f;

            return height;
        }

        private static DiagramShape CreateShape(NodeDef node)
        {
            ShapeDescription shapeType =
                node.Decision ? BasicFlowchartShapes.Decision : BasicShapes.Rectangle;

            float width = CalculateNodeWidth(node.Text);
            float height = CalculateNodeHeight(node.Text, width);

            var shape = new DiagramShape
            {
                Shape = shapeType,
                Content = node.Text,
                Tag = node.Id,
                Width = width,
                Height = height
            };

            shape.Appearance.Font = new Font("Segoe UI Emoji", 9.0f, FontStyle.Regular);
            shape.Appearance.Options.UseFont = true;
            shape.Appearance.TextOptions.WordWrap = WordWrap.Wrap;
            shape.Appearance.TextOptions.HAlignment = HorzAlignment.Center;
            shape.Appearance.TextOptions.VAlignment = VertAlignment.Center;
            shape.Appearance.Options.UseTextOptions = true;

            // Node "form thao tác" = node không phải quyết định (Decision) và
            // trong text có chứa tên 1 class Form (ví dụ "FormNhapPhieuTraHangKhach").
            // Đây là những node sau này sẽ gắn sự kiện click để mở Form tương ứng.
            bool isFormNode = !node.Decision && FormNameRegex.IsMatch(node.Text);

            if (isFormNode)
            {
                shape.Appearance.BackColor = Color.FromArgb(232, 245, 255); // xanh rất nhạt
                shape.Appearance.BorderColor = Color.FromArgb(21, 101, 192); // xanh dương đậm
                shape.Appearance.BorderSize = 2;
            }
            else
            {
                shape.Appearance.BackColor = Color.White;
                shape.Appearance.BorderColor = Color.FromArgb(90, 90, 90);
                shape.Appearance.BorderSize = 1;
            }

            shape.Appearance.ForeColor = GetContrastingTextColor(shape.Appearance.BackColor);
            shape.Appearance.Options.UseForeColor = true;
            shape.Appearance.Options.UseBackColor = true;
            shape.Appearance.Options.UseBorderColor = true;
            shape.Appearance.Options.UseBorderSize = true;

            return shape;
        }
        /// <summary>
        /// Trích tên class Form (vd "FormNhapPhieuTraHangKhach") từ nội dung 1 shape
        /// trên diagram, nếu node đó là "form thao tác" thật sự. Trả về null nếu
        /// không phải (node quyết định, trạng thái kết thúc, ghi chú...).
        /// </summary>
        public static string TryGetFormName(DiagramShape shape)
        {
            if (shape == null || string.IsNullOrEmpty(shape.Content))
                return null;

            Match match = FormNameRegex.Match(shape.Content);
            return match.Success ? match.Value : null;
        }
        private static DiagramConnector CreateConnector(
            DiagramShape source,
            DiagramShape target,
            EdgeDef edge)
        {
            ConnectorType type =
                ConnectorType.RightAngle;

            if (edge.Operator == "-.->" ||
                edge.Operator == "-.-")
            {
                type = ConnectorType.Curved;
            }
            else if (edge.Operator == "---")
            {
                type = ConnectorType.Straight;
            }

            var connector =
                new DiagramConnector(
                    type,
                    source,
                    target);

            connector.Content = edge.Label;

            connector.EndArrow =
                ArrowDescriptions.Filled90;

            connector.Appearance.Font = new Font("Segoe UI Emoji", 8.5f, FontStyle.Regular);
            connector.Appearance.Options.UseFont = true;

            connector.Appearance.ForeColor = Color.FromArgb(60, 60, 60);
            connector.Appearance.Options.UseForeColor = true;

            connector.Appearance.BackColor = Color.White;
            connector.Appearance.Options.UseBackColor = true;

            // IMPORTANT:
            // Do not use:
            // connector.Appearance.BorderDashPattern.Add(...)
            //
            // DiagramDoubleCollection has no Add() in this API.
            if (edge.Operator == "-.->" ||
                edge.Operator == "-.-")
            {
                connector.Appearance.BorderDashPattern =
                    new DiagramDoubleCollection(
                        new double[] { 4.0, 3.0 });

                connector.Appearance.Options.UseBorderDashPattern =
                    true;
            }

            if (edge.Operator == "==>")
            {
                connector.Appearance.BorderSize = 2;
                connector.Appearance.Options.UseBorderSize = true;
            }

            return connector;
        }

        private static Color GetContrastingTextColor(Color background)
        {
            double luminance =
                (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;

            return luminance > 0.55
                ? Color.FromArgb(33, 33, 33)   // nền sáng -> chữ tối
                : Color.White;                  // nền tối -> chữ trắng
        }



        private static string CleanMermaidText(
            string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("<br/>", "\n")
                .Replace("<br />", "\n")
                .Replace("<br>", "\n")
                .Replace("&nbsp;", " ")
                .Trim();
        }

        private static void ApplyStyle(DiagramItem item, string propsRaw)
        {
            if (item == null || string.IsNullOrWhiteSpace(propsRaw))
                return;

            string[] parts = propsRaw.Split(',');
            bool fillChanged = false;
            bool explicitForeColor = false;

            foreach (string rawPart in parts)
            {
                string[] kv = rawPart.Split(new[] { ':' }, 2);
                if (kv.Length != 2) continue;

                string key = kv[0].Trim().ToLowerInvariant();
                string value = kv[1].Trim();

                switch (key)
                {
                    case "fill":
                        if (TryParseMermaidColor(value, out Color fill))
                        {
                            item.Appearance.BackColor = fill;
                            item.Appearance.Options.UseBackColor = true;
                            fillChanged = true;
                        }
                        break;

                    case "stroke":
                        if (TryParseMermaidColor(value, out Color stroke))
                        {
                            item.Appearance.BorderColor = stroke;
                            item.Appearance.Options.UseBorderColor = true;
                        }
                        break;

                    case "color":
                        if (TryParseMermaidColor(value, out Color fore))
                        {
                            item.Appearance.ForeColor = fore;
                            item.Appearance.Options.UseForeColor = true;
                            explicitForeColor = true;
                        }
                        break;

                    case "stroke-width":
                        if (TryParsePixelInt(value, out int borderSize))
                        {
                            item.Appearance.BorderSize = Math.Max(1, borderSize);
                            item.Appearance.Options.UseBorderSize = true;
                        }
                        break;
                }
            }

            // Không có "color:" tường minh nhưng fill đổi -> tự chọn chữ tương phản
            if (fillChanged && !explicitForeColor)
            {
                item.Appearance.ForeColor = GetContrastingTextColor(item.Appearance.BackColor);
                item.Appearance.Options.UseForeColor = true;
            }
        }

        private static bool TryParsePixelInt(
            string text,
            out int value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string clean =
                text
                    .Trim()
                    .ToLowerInvariant()
                    .Replace("px", string.Empty)
                    .Trim();

            return int.TryParse(
                clean,
                out value);
        }

        private static bool TryParseMermaidColor(
            string text,
            out Color color)
        {
            color = Color.Empty;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string value = text.Trim();

            if (value.StartsWith(
                "#",
                StringComparison.Ordinal))
            {
                string hex =
                    value.Substring(1);

                try
                {
                    if (hex.Length == 3)
                    {
                        int r =
                            Convert.ToInt32(
                                new string(hex[0], 2),
                                16);

                        int g =
                            Convert.ToInt32(
                                new string(hex[1], 2),
                                16);

                        int b =
                            Convert.ToInt32(
                                new string(hex[2], 2),
                                16);

                        color =
                            Color.FromArgb(
                                r,
                                g,
                                b);

                        return true;
                    }

                    if (hex.Length == 6)
                    {
                        int r =
                            Convert.ToInt32(
                                hex.Substring(0, 2),
                                16);

                        int g =
                            Convert.ToInt32(
                                hex.Substring(2, 2),
                                16);

                        int b =
                            Convert.ToInt32(
                                hex.Substring(4, 2),
                                16);

                        color =
                            Color.FromArgb(
                                r,
                                g,
                                b);

                        return true;
                    }
                }
                catch (FormatException)
                {
                    return false;
                }
            }

            switch (value.ToLowerInvariant())
            {
                case "white":
                    color = Color.White;
                    return true;

                case "black":
                    color = Color.Black;
                    return true;

                case "red":
                    color = Color.Red;
                    return true;

                case "green":
                    color = Color.Green;
                    return true;

                case "blue":
                    color = Color.Blue;
                    return true;

                case "yellow":
                    color = Color.Yellow;
                    return true;

                case "orange":
                    color = Color.Orange;
                    return true;

                case "gray":
                case "grey":
                    color = Color.Gray;
                    return true;

                case "transparent":
                    color = Color.Transparent;
                    return true;
            }

            return false;
        }
    }
}
