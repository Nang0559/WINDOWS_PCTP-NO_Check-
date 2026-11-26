using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraTreeList;
using DevExpress.XtraTreeList.Nodes;

namespace PCTP.Framework.UiLayout
{
    /// <summary>
    /// "Tùy chọn các mục trình đơn" (Bravo): the left tree lists every shell item the app really has, the right tree is the
    /// menu of the chosen profile. Drag an item from the left into a group on the right (or use "Thêm"), drag nodes inside the
    /// right tree to reorder, rename groups/items, assign the profile to roles, then Lưu. The profile stores only item keys,
    /// so it can arrange and rename but never add a screen, a command or a permission.
    /// </summary>
    public sealed class UiMenuDesignerForm : XtraForm
    {
        private const string ItemFormat = "PCTP.MenuItem";
        private const string NodeFormat = "PCTP.MenuNode";
        private const int MaxNodeLevel = 2;   // zero-based: 3 levels

        private readonly UiMenuDesignerService _service;
        private readonly Func<IList<UiMenuItemInfo>> _itemSource;
        private IList<UiMenuItemInfo> _catalogue = new List<UiMenuItemInfo>();
        private IList<UiMenuRole> _allRoles = new List<UiMenuRole>();

        private readonly TreeList _left = new TreeList { Dock = DockStyle.Fill };
        private readonly TreeList _right = new TreeList { Dock = DockStyle.Fill, AllowDrop = true };
        private readonly ComboBoxEdit _profile = new ComboBoxEdit();
        private readonly CheckedComboBoxEdit _roles = new CheckedComboBoxEdit();
        private readonly SimpleButton _newProfile = new SimpleButton { Text = "Tạo profile…" };
        private readonly SpinEdit _order = new SpinEdit();
        private readonly SimpleButton _delete = new SimpleButton { Text = "Xóa profile" };
        private readonly SimpleButton _add = new SimpleButton { Text = "Thêm  »" };
        private readonly SimpleButton _newGroup = new SimpleButton { Text = "Nhóm mới" };
        private readonly SimpleButton _rename = new SimpleButton { Text = "Đổi tên" };
        private readonly SimpleButton _remove = new SimpleButton { Text = "Xóa" };
        private readonly SimpleButton _up = new SimpleButton { Text = "Lên" };
        private readonly SimpleButton _down = new SimpleButton { Text = "Xuống" };
        private readonly SimpleButton _clear = new SimpleButton { Text = "Về menu chuẩn" };
        private readonly SimpleButton _save = new SimpleButton { Text = "Lưu" };
        private readonly SimpleButton _close = new SimpleButton { Text = "Đóng" };
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 40, AutoSizeMode = LabelAutoSizeMode.None };

        private readonly HashSet<string> _placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private UiMenuProfile _current;
        private bool _dirty;

        /// <summary>True once a profile was saved or deleted in this dialog: the shell then rebuilds its menu.</summary>
        public bool Saved { get; private set; }
        private bool _loading;
        private int _selectedProfileIndex = -1;
        private TreeListNode _internalDrag;
        private TreeListNode _pressedRight;
        private UiMenuItemInfo _pressedLeft;
        private Point _pressPoint;

        public UiMenuDesignerForm(UiMenuDesignerService service, Func<IList<UiMenuItemInfo>> itemSource)
        {
            if (service == null) throw new ArgumentNullException("service");
            if (itemSource == null) throw new ArgumentNullException("itemSource");
            _service = service;
            _itemSource = itemSource;
            Text = "Tùy chọn các mục trình đơn";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1200, 760);
            MinimumSize = new Size(960, 560);
            BuildUi();
            Load += (s, e) => Initialize();
            FormClosing += (s, e) =>
            {
                if (e.Cancel || !_dirty) return;
                if (XtraMessageBox.Show(this, "Menu chưa lưu. Đóng và bỏ thay đổi?", Text,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) e.Cancel = true;
            };
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            SetupTree(_left, "Mục có sẵn");
            SetupTree(_right, "Menu của profile");
            _left.OptionsFind.AlwaysVisible = true;
            _left.NodeCellStyle += (s, e) =>
            {
                var item = e.Node.Tag as UiMenuItemInfo;
                if (item != null && _placed.Contains(item.ItemKey)) e.Appearance.ForeColor = Color.Gray;
            };
            _left.DoubleClick += (s, e) => AddSelectedLeft();
            _left.MouseDown += (s, e) =>
            {
                _pressedLeft = null;
                if (e.Button != MouseButtons.Left) return;
                var hit = _left.CalcHitInfo(e.Location);
                _pressedLeft = hit.Node == null ? null : hit.Node.Tag as UiMenuItemInfo;
                _pressPoint = e.Location;
            };
            _left.MouseMove += (s, e) =>
            {
                if (_pressedLeft == null || e.Button != MouseButtons.Left || !_service.CanEdit || !MovedFar(e.Location)) return;
                var item = _pressedLeft;
                _pressedLeft = null;
                _left.DoDragDrop(new DataObject(ItemFormat, item.ItemKey), DragDropEffects.Copy);
            };
            _left.MouseUp += (s, e) => _pressedLeft = null;

            _right.MouseDown += (s, e) =>
            {
                _pressedRight = null;
                if (e.Button != MouseButtons.Left) return;
                _pressedRight = _right.CalcHitInfo(e.Location).Node;
                _pressPoint = e.Location;
            };
            _right.MouseMove += (s, e) =>
            {
                if (_pressedRight == null || e.Button != MouseButtons.Left || !_service.CanEdit || !MovedFar(e.Location)) return;
                _internalDrag = _pressedRight;
                _pressedRight = null;
                _right.DoDragDrop(new DataObject(NodeFormat, "1"), DragDropEffects.Move);
                _internalDrag = null;
            };
            _right.MouseUp += (s, e) => _pressedRight = null;
            _right.DragEnter += (s, e) => e.Effect = DropEffect(e);
            _right.DragOver += (s, e) => e.Effect = DropEffect(e);
            _right.DragDrop += (s, e) => HandleDrop(e);

            var split = new SplitContainerControl { Dock = DockStyle.Fill, SplitterPosition = 520 };
            split.Panel1.Controls.Add(_left);
            split.Panel2.Controls.Add(_right);

            var top = new PanelControl { Dock = DockStyle.Top, Height = 84 };
            top.Controls.Add(new LabelControl
            {
                Text = "Kéo mục ở danh sách trái sang cây menu bên phải (hoặc chọn rồi bấm Thêm). Kéo trong cây phải để đổi vị trí.",
                Left = 10, Top = 8
            });
            top.Controls.Add(new LabelControl { Text = "Profile", Left = 10, Top = 40 });
            _profile.Left = 70; _profile.Top = 36; _profile.Width = 250;
            _profile.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
            _profile.SelectedIndexChanged += (s, e) => OnProfileChosen();
            top.Controls.Add(_profile);
            _newProfile.Left = 328; _newProfile.Top = 35; _newProfile.Width = 100;
            _newProfile.Click += (s, e) => CreateProfile();
            top.Controls.Add(_newProfile);
            top.Controls.Add(new LabelControl { Text = "Áp dụng cho vai trò", Left = 445, Top = 40 });
            _roles.Left = 560; _roles.Top = 36; _roles.Width = 250;
            _roles.EditValueChanged += (s, e) => { if (!_loading) MarkDirty(); };
            top.Controls.Add(_roles);
            top.Controls.Add(new LabelControl { Text = "Ưu tiên", Left = 825, Top = 40 });
            _order.Left = 880; _order.Top = 36; _order.Width = 70;
            _order.Properties.MinValue = UiMenuValidator.MinOrder;
            _order.Properties.MaxValue = UiMenuValidator.MaxOrder;
            _order.Properties.IsFloatValue = false;
            _order.Properties.Mask.EditMask = "n0";
            _order.Properties.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.Numeric;
            _order.ToolTip = "Người dùng thuộc nhiều vai trò ở nhiều profile sẽ nhận profile có số nhỏ nhất (bằng nhau: theo mã profile).";
            _order.EditValueChanged += (s, e) => { if (!_loading) MarkDirty(); };
            top.Controls.Add(_order);

            var buttons = new PanelControl { Dock = DockStyle.Right, Width = 132 };
            int y = 10;
            foreach (var b in new[] { _add, _newGroup, _rename, _remove, _up, _down, _clear, _delete })
            {
                b.Left = 10; b.Top = y; b.Width = 112; buttons.Controls.Add(b); y += 34;
            }
            _save.Left = 10; _save.Top = y + 20; _save.Width = 112; buttons.Controls.Add(_save);
            _close.Left = 10; _close.Top = y + 54; _close.Width = 112; buttons.Controls.Add(_close);
            _add.Click += (s, e) => AddSelectedLeft();
            _newGroup.Click += (s, e) => NewGroup();
            _rename.Click += (s, e) => RenameFocused();
            _remove.Click += (s, e) => RemoveFocused();
            _up.Click += (s, e) => MoveFocused(-1);
            _down.Click += (s, e) => MoveFocused(1);
            _clear.Click += (s, e) => ClearMenu();
            _delete.Click += (s, e) => DeleteProfile();
            _save.Click += (s, e) => Save();
            _close.Click += (s, e) => Close();

            Controls.Add(split);
            Controls.Add(buttons);
            Controls.Add(top);
            Controls.Add(_status);
        }

        private static void SetupTree(TreeList tree, string caption)
        {
            tree.OptionsView.ShowIndicator = false;
            tree.OptionsView.ShowColumns = false;
            tree.OptionsBehavior.Editable = false;
            var column = tree.Columns.Add();
            column.FieldName = "Caption";
            column.Caption = caption;
            column.Visible = true;
        }

        private bool MovedFar(Point p)
        {
            return Math.Abs(p.X - _pressPoint.X) > SystemInformation.DragSize.Width / 2
                || Math.Abs(p.Y - _pressPoint.Y) > SystemInformation.DragSize.Height / 2;
        }

        // ------------------------------------------------------------------ load

        private void Initialize()
        {
            try
            {
                string catalogueWarning;
                _catalogue = BuildCatalogue(out catalogueWarning);
                _allRoles = _service.ListRoles();
                FillLeft();
                ReloadProfiles(null);
                bool can = _service.CanEdit;
                foreach (var b in new[] { _add, _newGroup, _rename, _remove, _up, _down, _clear, _save, _newProfile, _delete }) b.Enabled = can;
                ApplyProfileKindState();
                if (catalogueWarning != null) SetStatus(catalogueWarning, true);
                else SetStatus(can ? "Chọn profile rồi sắp xếp menu." : "Bạn chỉ có quyền xem (cần EDIT trên ADMIN.DOCUMENT_DESIGNER để sửa).", !can);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        /// <summary>
        /// Real shell items plus every approved UI screen. The screens come from SQL, so the list does not depend on which
        /// screens the person designing may open; the shell still only creates the items each signed-in user may use.
        /// </summary>
        private IList<UiMenuItemInfo> BuildCatalogue(out string warning)
        {
            warning = null;
            var list = new List<UiMenuItemInfo>(_itemSource() ?? new List<UiMenuItemInfo>());
            var keys = new HashSet<string>(list.Select(i => i.ItemKey), StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var screen in _service.ListScreenItems())
                    if (keys.Add(screen.ItemKey)) list.Add(screen);
            }
            catch (Exception ex)
            {
                warning = "Không đọc được đủ danh sách màn hình UI (áp dụng migration 060?): " + ex.Message;
            }
            return list;
        }

        private void FillLeft()
        {
            _left.BeginUnboundLoad();
            _left.ClearNodes();
            foreach (var group in _catalogue.GroupBy(i => string.IsNullOrWhiteSpace(i.GroupCaption) ? "(Khác)" : i.GroupCaption))
            {
                TreeListNode groupNode = _left.AppendNode(new object[] { group.Key }, (TreeListNode)null);
                foreach (var item in group.OrderBy(i => i.Caption, StringComparer.CurrentCultureIgnoreCase))
                {
                    TreeListNode node = _left.AppendNode(new object[] { item.Caption + "  (" + item.ItemKey + ")" }, groupNode);
                    node.Tag = item;
                }
            }
            _left.EndUnboundLoad();
            _left.ExpandAll();
        }

        private void ReloadProfiles(string select)
        {
            _loading = true;
            try
            {
                var list = _service.ListProfiles();
                _profile.Properties.Items.Clear();
                foreach (var p in list) _profile.Properties.Items.Add(p);
                int index = 0;
                for (int i = 0; i < list.Count; i++)
                    if (string.Equals(list[i].ProfileCode, select ?? "DEFAULT", StringComparison.Ordinal)) index = i;
                _selectedProfileIndex = list.Count == 0 ? -1 : index;
                _profile.SelectedIndex = _selectedProfileIndex;
                if (_selectedProfileIndex >= 0) BindProfile(_service.Open(list[index].ProfileCode));
            }
            finally { _loading = false; }
        }

        private void OnProfileChosen()
        {
            if (_loading) return;
            var info = _profile.SelectedItem as UiMenuProfileInfo;
            if (info == null) return;
            if (_dirty && XtraMessageBox.Show(this, "Menu chưa lưu. Bỏ thay đổi và đổi profile?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                _loading = true; _profile.SelectedIndex = _selectedProfileIndex; _loading = false;
                return;
            }
            try
            {
                _loading = true;
                _selectedProfileIndex = _profile.SelectedIndex;
                BindProfile(_service.Open(info.ProfileCode));
            }
            catch (Exception ex) { SetStatus(ex.Message, true); }
            finally { _loading = false; }
        }

        private void BindProfile(UiMenuProfile profile)
        {
            _current = profile;
            _right.BeginUnboundLoad();
            _right.ClearNodes();
            var byParent = profile.Nodes.ToLookup(n => n.ParentNodeId);
            var seen = new HashSet<int>();
            Action<int?, TreeListNode> add = null;
            add = (parentId, parentNode) =>
            {
                foreach (var n in byParent[parentId].OrderBy(x => x.SequenceNo).ThenBy(x => x.NodeId))
                {
                    if (!seen.Add(n.NodeId)) continue;
                    var model = new UiMenuNode { NodeType = n.NodeType, ItemKey = n.ItemKey, Caption = n.Caption };
                    TreeListNode node = _right.AppendNode(new object[] { NodeText(model) }, parentNode);
                    node.Tag = model;
                    add(n.NodeId, node);
                }
            };
            add(null, null);
            _right.EndUnboundLoad();
            _right.ExpandAll();

            _roles.Properties.Items.Clear();
            foreach (var role in _allRoles)
                _roles.Properties.Items.Add(new CheckedListBoxItem(role.RoleCode, role.RoleName + " (" + role.RoleCode + ")",
                    profile.RoleCodes.Contains(role.RoleCode, StringComparer.OrdinalIgnoreCase) ? CheckState.Checked : CheckState.Unchecked));
            _order.Value = UiMenuValidator.IsValidOrder(profile.ProfileOrder) ? profile.ProfileOrder : UiMenuValidator.DefaultOrder;
            ApplyProfileKindState();

            _dirty = false;
            RecomputePlaced();
            UpdateTitle();
            SetStatus("Profile " + profile.ProfileCode + " (phiên bản " + profile.ConfigVersion + "). " +
                (profile.Nodes.Count == 0 ? "Chưa có cấu hình: ứng dụng dùng menu chuẩn." : profile.Nodes.Count + " mục."), false);
        }

        /// <summary>DEFAULT is the fallback menu: no roles, no priority, no delete.</summary>
        private void ApplyProfileKindState()
        {
            bool isDefault = _current == null || string.Equals(_current.ProfileCode, "DEFAULT", StringComparison.Ordinal);
            bool can = _service.CanEdit;
            _roles.Enabled = can && !isDefault;
            _order.Enabled = can && !isDefault;
            _delete.Enabled = can && !isDefault;
        }

        private string NodeText(UiMenuNode model)
        {
            if (string.Equals(model.NodeType, UiMenuNodeType.Group, StringComparison.OrdinalIgnoreCase)) return model.Caption ?? string.Empty;
            var info = _catalogue.FirstOrDefault(i => string.Equals(i.ItemKey, model.ItemKey, StringComparison.OrdinalIgnoreCase));
            if (info == null) return "[" + model.ItemKey + "] (không còn trong ứng dụng)";
            return string.IsNullOrWhiteSpace(model.Caption) ? info.Caption : model.Caption;
        }

        // ------------------------------------------------------------------ editing

        private static bool IsGroup(TreeListNode node)
        {
            var model = node.Tag as UiMenuNode;
            return model != null && string.Equals(model.NodeType, UiMenuNodeType.Group, StringComparison.OrdinalIgnoreCase);
        }

        private static int IndexInParent(TreeListNode node)
        {
            var siblings = node.ParentNode != null ? node.ParentNode.Nodes : node.TreeList.Nodes;
            for (int i = 0; i < siblings.Count; i++) if (ReferenceEquals(siblings[i], node)) return i;
            return -1;
        }

        private static int SubtreeHeight(TreeListNode node)
        {
            int height = 0;
            for (int i = 0; i < node.Nodes.Count; i++) height = Math.Max(height, 1 + SubtreeHeight(node.Nodes[i]));
            return height;
        }

        private static bool IsSameOrDescendant(TreeListNode candidate, TreeListNode ancestor)
        {
            for (var n = candidate; n != null; n = n.ParentNode) if (ReferenceEquals(n, ancestor)) return true;
            return false;
        }

        private static void ResolveSlot(TreeListNode target, out TreeListNode parent, out bool afterTarget)
        {
            afterTarget = false;
            if (target == null) { parent = null; return; }
            if (IsGroup(target)) { parent = target; return; }
            parent = target.ParentNode;
            afterTarget = true;
        }

        private static int LevelOfChild(TreeListNode parent) { return parent == null ? 0 : parent.Level + 1; }

        private void AddSelectedLeft()
        {
            if (!_service.CanEdit || _left.FocusedNode == null) return;
            var item = _left.FocusedNode.Tag as UiMenuItemInfo;
            if (item == null) { SetStatus("Chọn một mục (không phải tên nhóm) ở danh sách trái.", true); return; }
            AddItem(item, _right.FocusedNode);
        }

        private void AddItem(UiMenuItemInfo item, TreeListNode target)
        {
            if (_current == null || !_service.CanEdit) return;
            if (_placed.Contains(item.ItemKey)) { SetStatus("Mục \"" + item.Caption + "\" đã có trong menu.", true); return; }
            TreeListNode parent; bool after;
            ResolveSlot(target, out parent, out after);
            if (LevelOfChild(parent) > MaxNodeLevel) { SetStatus("Menu chỉ có tối đa 3 cấp.", true); return; }
            var model = new UiMenuNode { NodeType = UiMenuNodeType.Item, ItemKey = item.ItemKey };
            TreeListNode node = _right.AppendNode(new object[] { NodeText(model) }, parent);
            node.Tag = model;
            if (after) _right.SetNodeIndex(node, IndexInParent(target) + 1);
            if (parent != null) parent.Expanded = true;
            _right.FocusedNode = node;
            Changed();
        }

        private void NewGroup()
        {
            if (_current == null || !_service.CanEdit) return;
            string caption = XtraInputBox.Show("Tên nhóm menu:", "Nhóm mới", string.Empty);
            if (string.IsNullOrWhiteSpace(caption)) return;
            TreeListNode parent; bool after;
            ResolveSlot(_right.FocusedNode, out parent, out after);
            if (LevelOfChild(parent) > MaxNodeLevel) { SetStatus("Menu chỉ có tối đa 3 cấp.", true); return; }
            var model = new UiMenuNode { NodeType = UiMenuNodeType.Group, Caption = caption.Trim() };
            TreeListNode node = _right.AppendNode(new object[] { NodeText(model) }, parent);
            node.Tag = model;
            if (after) _right.SetNodeIndex(node, IndexInParent(_right.FocusedNode) + 1);
            if (parent != null) parent.Expanded = true;
            _right.FocusedNode = node;
            Changed();
        }

        private void RenameFocused()
        {
            var node = _right.FocusedNode;
            if (node == null || !_service.CanEdit) return;
            var model = (UiMenuNode)node.Tag;
            bool group = IsGroup(node);
            string text = XtraInputBox.Show(group ? "Tên nhóm:" : "Tên hiển thị (để trống = tên mặc định):",
                "Đổi tên", node.GetDisplayText(_right.Columns[0]));
            if (text == null) return;
            text = text.Trim();
            if (group && text.Length == 0) { SetStatus("Nhóm phải có tên.", true); return; }
            model.Caption = text.Length == 0 ? null : text;
            node.SetValue(_right.Columns[0], NodeText(model));
            Changed();
        }

        private void RemoveFocused()
        {
            var node = _right.FocusedNode;
            if (node == null || !_service.CanEdit) return;
            if (node.Nodes.Count > 0 && XtraMessageBox.Show(this, "Xóa nhóm cùng các mục bên trong?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _right.DeleteNode(node);
            Changed();
        }

        private void MoveFocused(int delta)
        {
            var node = _right.FocusedNode;
            if (node == null || !_service.CanEdit) return;
            var siblings = node.ParentNode != null ? node.ParentNode.Nodes : _right.Nodes;
            int index = IndexInParent(node);
            int to = index + delta;
            if (to < 0 || to >= siblings.Count) return;
            _right.SetNodeIndex(node, to);
            _right.FocusedNode = node;
            Changed();
        }

        private void ClearMenu()
        {
            if (_current == null || !_service.CanEdit) return;
            if (XtraMessageBox.Show(this, "Xóa toàn bộ cấu hình của profile này (ứng dụng dùng lại menu chuẩn sau khi lưu)?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _right.ClearNodes();
            Changed();
        }

        private DragDropEffects DropEffect(DragEventArgs e)
        {
            if (!_service.CanEdit || _current == null) return DragDropEffects.None;
            if (e.Data.GetDataPresent(ItemFormat)) return DragDropEffects.Copy;
            if (e.Data.GetDataPresent(NodeFormat) && _internalDrag != null) return DragDropEffects.Move;
            return DragDropEffects.None;
        }

        private void HandleDrop(DragEventArgs e)
        {
            var target = _right.CalcHitInfo(_right.PointToClient(new Point(e.X, e.Y))).Node;
            if (e.Data.GetDataPresent(ItemFormat))
            {
                string key = (string)e.Data.GetData(ItemFormat);
                var item = _catalogue.FirstOrDefault(i => string.Equals(i.ItemKey, key, StringComparison.OrdinalIgnoreCase));
                if (item != null) AddItem(item, target);
            }
            else if (e.Data.GetDataPresent(NodeFormat) && _internalDrag != null)
            {
                var node = _internalDrag;
                _internalDrag = null;
                MoveExisting(node, target);
            }
        }

        /// <summary>Re-creates the subtree at the drop slot and deletes the original (uses only Append/Delete/SetNodeIndex).</summary>
        private void MoveExisting(TreeListNode node, TreeListNode target)
        {
            if (target != null && IsSameOrDescendant(target, node)) return;
            TreeListNode parent; bool after;
            ResolveSlot(target, out parent, out after);
            if (LevelOfChild(parent) + SubtreeHeight(node) > MaxNodeLevel) { SetStatus("Menu chỉ có tối đa 3 cấp.", true); return; }
            TreeListNode copy = CopySubtree(node, parent);
            _right.DeleteNode(node);
            if (after) _right.SetNodeIndex(copy, IndexInParent(target) + 1);
            if (parent != null) parent.Expanded = true;
            copy.ExpandAll();
            _right.FocusedNode = copy;
            Changed();
        }

        private TreeListNode CopySubtree(TreeListNode source, TreeListNode newParent)
        {
            TreeListNode copy = _right.AppendNode(new object[] { source.GetDisplayText(_right.Columns[0]) }, newParent);
            copy.Tag = source.Tag;
            for (int i = 0; i < source.Nodes.Count; i++) CopySubtree(source.Nodes[i], copy);
            return copy;
        }

        // ------------------------------------------------------------------ profile + save

        private void CreateProfile()
        {
            if (!_service.CanEdit) return;
            string code = XtraInputBox.Show("Mã profile (A-Z, 0-9, _), ví dụ WAREHOUSE:", "Tạo profile", string.Empty);
            if (string.IsNullOrWhiteSpace(code)) return;
            string name = XtraInputBox.Show("Tên hiển thị:", "Tạo profile", code.Trim());
            if (string.IsNullOrWhiteSpace(name)) return;
            if (_dirty && XtraMessageBox.Show(this, "Menu chưa lưu sẽ bị bỏ. Tiếp tục?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                _service.Create(code, name);
                _dirty = false;
                ReloadProfiles(code.Trim().ToUpperInvariant());
            }
            catch (Exception ex) { SetStatus(ex.Message, true); }
        }

        private void DeleteProfile()
        {
            if (_current == null || !_service.CanEdit) return;
            if (string.Equals(_current.ProfileCode, "DEFAULT", StringComparison.Ordinal))
            {
                SetStatus("Không thể xóa profile DEFAULT. Dùng \"Về menu chuẩn\" rồi Lưu để làm rỗng.", true);
                return;
            }
            var info = _profile.SelectedItem as UiMenuProfileInfo;
            int roleCount = info == null ? CheckedRoles().Count : info.RoleCount;
            if (XtraMessageBox.Show(this,
                    "Xóa profile " + _current.ProfileCode + " (" + _current.DisplayName + ")?" + Environment.NewLine +
                    (roleCount > 0 ? roleCount + " vai trò đang gán sẽ quay về menu DEFAULT." + Environment.NewLine : string.Empty) +
                    "Bản sao được giữ trong lịch sử thay đổi.",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                string code = _current.ProfileCode;
                _service.Delete(code, _current.ConfigVersion);
                Saved = true;
                _dirty = false;
                ReloadProfiles(null);
                SetStatus("Đã xóa profile " + code + ".", false);
            }
            catch (UiConfigConflictException ex) { SetStatus(ex.Message, true); }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private List<UiMenuNode> CollectNodes()
        {
            var list = new List<UiMenuNode>();
            int id = 0;
            Action<TreeListNodes, int?> walk = null;
            walk = (nodes, parentId) =>
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var model = (UiMenuNode)nodes[i].Tag;
                    int myId = ++id;
                    list.Add(new UiMenuNode
                    {
                        NodeId = myId,
                        ParentNodeId = parentId,
                        NodeType = model.NodeType,
                        ItemKey = model.ItemKey,
                        Caption = model.Caption,
                        SequenceNo = i + 1
                    });
                    walk(nodes[i].Nodes, myId);
                }
            };
            walk(_right.Nodes, null);
            return list;
        }

        private List<string> CheckedRoles()
        {
            var roles = new List<string>();
            foreach (CheckedListBoxItem item in _roles.Properties.Items)
                if (item.CheckState == CheckState.Checked) roles.Add(Convert.ToString(item.Value));
            return roles;
        }

        private void Save()
        {
            if (_current == null || !_service.CanEdit) return;
            var profile = new UiMenuProfile
            {
                ProfileCode = _current.ProfileCode,
                DisplayName = _current.DisplayName,
                ConfigVersion = _current.ConfigVersion,
                ProfileOrder = Convert.ToInt32(_order.Value),
                Nodes = CollectNodes(),
                RoleCodes = CheckedRoles()
            };
            try
            {
                _current.ConfigVersion = _service.Save(profile);
                Saved = true;
                if (!string.Equals(profile.ProfileCode, "DEFAULT", StringComparison.Ordinal) && profile.ProfileOrder != _current.ProfileOrder)
                {
                    try
                    {
                        _current.ConfigVersion = _service.SetOrder(profile.ProfileCode, _current.ConfigVersion, profile.ProfileOrder);
                        _current.ProfileOrder = profile.ProfileOrder;
                    }
                    catch (Exception ex)
                    {
                        // The menu itself is saved; only the priority failed. Keep the form dirty so Lưu can be pressed again.
                        MarkDirty();
                        SetStatus("Đã lưu menu nhưng chưa lưu được thứ tự ưu tiên: " + ex.Message, true);
                        return;
                    }
                }
                _dirty = false;
                UpdateTitle();
                SetStatus("Đã lưu profile " + profile.ProfileCode + " (phiên bản " + _current.ConfigVersion +
                          "). Menu của bạn được dựng lại khi đóng cửa sổ này; người dùng khác nhận menu mới khi mở lại ứng dụng.", false);
            }
            catch (UiConfigConflictException ex)
            {
                SetStatus(ex.Message, true);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ------------------------------------------------------------------ bookkeeping

        private void Changed()
        {
            RecomputePlaced();
            MarkDirty();
        }

        private void MarkDirty()
        {
            _dirty = true;
            UpdateTitle();
        }

        private void RecomputePlaced()
        {
            _placed.Clear();
            Action<TreeListNodes> walk = null;
            walk = nodes =>
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var model = nodes[i].Tag as UiMenuNode;
                    if (model != null && !string.IsNullOrEmpty(model.ItemKey)) _placed.Add(model.ItemKey);
                    walk(nodes[i].Nodes);
                }
            };
            walk(_right.Nodes);
            _left.Refresh();
        }

        private void UpdateTitle()
        {
            Text = "Tùy chọn các mục trình đơn" + (_current == null ? string.Empty : " - " + _current.ProfileCode) + (_dirty ? " *" : string.Empty);
        }

        private void SetStatus(string text, bool error)
        {
            _status.Appearance.ForeColor = error ? Color.Firebrick : Color.DimGray;
            _status.Text = text;
        }
    }
}
