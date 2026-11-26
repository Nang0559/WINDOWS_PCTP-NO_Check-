using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PCTP.Framework.UiLayout
{
    public static class UiMenuNodeType
    {
        public const string Group = "GROUP";
        public const string Item = "ITEM";
    }

    /// <summary>One node of a menu profile. An ITEM carries only the Name of an existing shell item (ItemKey).</summary>
    public sealed class UiMenuNode
    {
        public int NodeId { get; set; }
        public int? ParentNodeId { get; set; }
        public string NodeType { get; set; }
        public string ItemKey { get; set; }
        public string Caption { get; set; }
        public int SequenceNo { get; set; }
    }

    public sealed class UiMenuProfileInfo
    {
        public string ProfileCode { get; set; }
        public string DisplayName { get; set; }
        public int ConfigVersion { get; set; }
        public int NodeCount { get; set; }
        public int RoleCount { get; set; }
        public int ProfileOrder { get; set; }
        public override string ToString() { return ProfileCode + " - " + DisplayName; }
    }

    public sealed class UiMenuProfile
    {
        public string ProfileCode { get; set; }
        public string DisplayName { get; set; }
        public int ConfigVersion { get; set; }
        /// <summary>Priority when a user's roles sit in several profiles: the smallest number wins (ties: ProfileCode).</summary>
        public int ProfileOrder { get; set; }
        public List<UiMenuNode> Nodes { get; set; }
        public List<string> RoleCodes { get; set; }

        public UiMenuProfile() { Nodes = new List<UiMenuNode>(); RoleCodes = new List<string>(); ProfileOrder = UiMenuValidator.DefaultOrder; }
    }

    public sealed class UiMenuRole
    {
        public string RoleCode { get; set; }
        public string RoleName { get; set; }
    }

    /// <summary>A shell item the designer may place: the Name of the real menu element plus its default text and group.</summary>
    public sealed class UiMenuItemInfo
    {
        public string ItemKey { get; set; }
        public string Caption { get; set; }
        public string GroupCaption { get; set; }
    }

    /// <summary>Name of the shell item that opens an approved UI screen. One definition for the shell and the designer.</summary>
    public static class UiMenuScreenKey
    {
        public const string Prefix = "menuUiScreen_";
        public const string GroupCaption = "Màn hình UI";
        public static string For(string screenCode) { return Prefix + screenCode; }
    }

    /// <summary>Same rules as usp_FVN_UiMenuProfile_Save, so the designer reports problems before SQL does.</summary>
    public static class UiMenuValidator
    {
        public const int MaxNodes = 300;
        public const int MaxDepth = 3;
        public const int MinOrder = 1;
        public const int MaxOrder = 9999;
        public const int DefaultOrder = 100;
        private static readonly Regex KeyPattern = new Regex("^[A-Za-z0-9_]{1,100}$", RegexOptions.CultureInvariant);
        private static readonly Regex ProfilePattern = new Regex("^[A-Z0-9_]{1,40}$", RegexOptions.CultureInvariant);

        public static bool IsValidProfileCode(string code) { return code != null && ProfilePattern.IsMatch(code); }

        public static bool IsValidOrder(int order) { return order >= MinOrder && order <= MaxOrder; }

        public static IList<string> Validate(string profileCode, IList<UiMenuNode> nodes, IList<string> roleCodes)
        {
            var errors = new List<string>();
            nodes = nodes ?? new List<UiMenuNode>();
            roleCodes = roleCodes ?? new List<string>();
            if (!IsValidProfileCode(profileCode)) errors.Add("Mã profile không hợp lệ (A-Z, 0-9, _; tối đa 40 ký tự).");
            if (nodes.Count > MaxNodes) errors.Add("Tối đa " + MaxNodes + " mục menu.");
            if (string.Equals(profileCode, "DEFAULT", StringComparison.Ordinal) && roleCodes.Count > 0)
                errors.Add("Profile DEFAULT là menu dự phòng, không gán vai trò.");

            var byId = new Dictionary<int, UiMenuNode>();
            foreach (var n in nodes)
            {
                if (n == null || n.NodeId < 1 || byId.ContainsKey(n.NodeId)) { errors.Add("NodeId thiếu hoặc trùng."); return errors; }
                byId[n.NodeId] = n;
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in nodes)
            {
                bool group = string.Equals(n.NodeType, UiMenuNodeType.Group, StringComparison.OrdinalIgnoreCase);
                bool item = string.Equals(n.NodeType, UiMenuNodeType.Item, StringComparison.OrdinalIgnoreCase);
                string caption = (n.Caption ?? string.Empty).Trim();
                if (!group && !item) { errors.Add("Loại nút không hợp lệ (NodeId " + n.NodeId + ")."); continue; }
                if (caption.Length > 160) errors.Add("Tiêu đề dài quá 160 ký tự (NodeId " + n.NodeId + ").");
                if (group)
                {
                    if (caption.Length == 0) errors.Add("Nhóm phải có tiêu đề (NodeId " + n.NodeId + ").");
                    if (!string.IsNullOrEmpty(n.ItemKey)) errors.Add("Nhóm không được mang ItemKey (NodeId " + n.NodeId + ").");
                }
                else
                {
                    if (string.IsNullOrEmpty(n.ItemKey) || !KeyPattern.IsMatch(n.ItemKey))
                        errors.Add("ItemKey không hợp lệ (NodeId " + n.NodeId + ").");
                    else if (!keys.Add(n.ItemKey))
                        errors.Add("Mục \"" + n.ItemKey + "\" xuất hiện nhiều lần; mỗi mục chỉ đặt một lần.");
                }
                if (n.ParentNodeId.HasValue)
                {
                    UiMenuNode parent;
                    if (!byId.TryGetValue(n.ParentNodeId.Value, out parent) || parent.NodeId == n.NodeId)
                        errors.Add("Nút cha không tồn tại (NodeId " + n.NodeId + ").");
                    else if (string.Equals(parent.NodeType, UiMenuNodeType.Item, StringComparison.OrdinalIgnoreCase))
                        errors.Add("Một mục menu không thể chứa mục con (NodeId " + n.NodeId + ").");
                }
            }
            if (errors.Count > 0) return errors;

            foreach (var n in nodes)
            {
                int depth = 1;
                var cursor = n;
                while (cursor.ParentNodeId.HasValue)
                {
                    cursor = byId[cursor.ParentNodeId.Value];
                    depth++;
                    if (depth > nodes.Count + 1) { errors.Add("Cây menu có vòng lặp."); return errors; }
                }
                if (depth > MaxDepth) { errors.Add("Cây menu sâu quá " + MaxDepth + " cấp."); return errors; }
            }
            return errors;
        }
    }

    public sealed class UiMenuPlanNode
    {
        public bool IsGroup { get; set; }
        public string Caption { get; set; }
        public string ItemKey { get; set; }
        public List<UiMenuPlanNode> Children { get; set; }
        public UiMenuPlanNode() { Children = new List<UiMenuPlanNode>(); }
    }

    /// <summary>
    /// Turns stored nodes into what the shell may build: unknown or missing items are dropped, a key is used once, empty
    /// groups disappear, malformed or cyclic data cannot loop. Pure function: it never touches the UI or the database.
    /// </summary>
    public static class UiMenuResolver
    {
        public static IList<UiMenuPlanNode> Resolve(IEnumerable<UiMenuNode> nodes, ISet<string> presentKeys)
        {
            var list = (nodes ?? Enumerable.Empty<UiMenuNode>()).Where(n => n != null).ToList();
            if (list.Count == 0 || presentKeys == null) return new List<UiMenuPlanNode>();
            var lookup = list.ToLookup(n => n.ParentNodeId);
            return Build(null, lookup, presentKeys, new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<int>(), 1);
        }

        private static List<UiMenuPlanNode> Build(int? parent, ILookup<int?, UiMenuNode> lookup, ISet<string> present,
            HashSet<string> used, HashSet<int> visiting, int depth)
        {
            var result = new List<UiMenuPlanNode>();
            if (depth > UiMenuValidator.MaxDepth) return result;
            foreach (var n in lookup[parent].OrderBy(x => x.SequenceNo).ThenBy(x => x.NodeId))
            {
                if (!visiting.Add(n.NodeId)) continue;
                string caption = string.IsNullOrWhiteSpace(n.Caption) ? null : n.Caption.Trim();
                if (string.Equals(n.NodeType, UiMenuNodeType.Group, StringComparison.OrdinalIgnoreCase))
                {
                    var kids = Build(n.NodeId, lookup, present, used, visiting, depth + 1);
                    if (kids.Count > 0 && caption != null)
                        result.Add(new UiMenuPlanNode { IsGroup = true, Caption = caption, Children = kids });
                }
                else if (string.Equals(n.NodeType, UiMenuNodeType.Item, StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrEmpty(n.ItemKey) && present.Contains(n.ItemKey) && used.Add(n.ItemKey))
                {
                    result.Add(new UiMenuPlanNode { IsGroup = false, ItemKey = n.ItemKey, Caption = caption });
                }
                visiting.Remove(n.NodeId);
            }
            return result;
        }
    }
}
