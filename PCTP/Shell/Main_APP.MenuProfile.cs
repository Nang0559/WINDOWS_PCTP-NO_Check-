using DevExpress.XtraBars.Navigation;
using PCTP.Framework.UiLayout;
using PCTP.Security;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP
{
    /// <summary>
    /// Applies the menu profile saved with the UI Menu Designer (migration 059). The profile only names existing items:
    /// the shell moves the real menu elements into the profile's groups, ignores unknown keys, and never makes visible
    /// an item that authorization already hid. Items the profile does not mention stay where the standard menu put them,
    /// and any failure leaves the standard menu untouched.
    /// </summary>
    public partial class Main_APP
    {
        private IList<UiMenuItemInfo> _menuItemSnapshot;
        private StandardMenuState _standardMenu;

        /// <summary>
        /// The menu exactly as the standard shell built it (after authorization), taken once before the first profile is
        /// applied. It lets a profile be applied again, or removed, while the app is running.
        /// </summary>
        private sealed class StandardMenuState
        {
            public readonly List<KeyValuePair<AccordionControlElement, List<AccordionControlElement>>> Children =
                new List<KeyValuePair<AccordionControlElement, List<AccordionControlElement>>>();
            public readonly Dictionary<AccordionControlElement, bool> Visible = new Dictionary<AccordionControlElement, bool>();
            public readonly Dictionary<AccordionControlElement, string> ItemText = new Dictionary<AccordionControlElement, string>();
        }

        private StandardMenuState CaptureStandardMenu()
        {
            var state = new StandardMenuState();
            Action<AccordionControlElement> walk = null;
            walk = owner =>
            {
                var kids = owner.Elements.Cast<AccordionControlElement>().ToList();
                state.Children.Add(new KeyValuePair<AccordionControlElement, List<AccordionControlElement>>(owner, kids));
                foreach (var kid in kids)
                {
                    state.Visible[kid] = kid.Visible;
                    if (kid.Style == ElementStyle.Item) state.ItemText[kid] = kid.Text;
                    walk(kid);
                }
            };
            walk(mainAccordionGroup);
            return state;
        }

        /// <summary>Puts every element back where the standard menu had it, with its own text and its authorized visibility.</summary>
        private void RestoreStandardMenu(StandardMenuState state)
        {
            // 1) detach everything that was rearranged: profile groups first (they hold moved items), then every owner
            foreach (var top in mainAccordionGroup.Elements.Cast<AccordionControlElement>().ToList())
                if (IsProfileGroup(top)) ClearProfileGroup(top);
            foreach (var entry in state.Children) entry.Key.Elements.Clear();
            // 2) re-attach in the original order
            foreach (var entry in state.Children)
                foreach (var kid in entry.Value) entry.Key.Elements.Add(kid);
            // 3) undo renames and the "empty group hidden" step; authorization's own result is what was captured
            foreach (var pair in state.Visible) pair.Key.Visible = pair.Value;
            foreach (var pair in state.ItemText) pair.Key.Text = pair.Value;
        }

        private static bool IsProfileGroup(AccordionControlElement element)
        {
            return element.Style == ElementStyle.Group && element.Name != null
                   && element.Name.StartsWith("menuProfileGroup_", StringComparison.Ordinal);
        }

        private static void ClearProfileGroup(AccordionControlElement group)
        {
            foreach (var kid in group.Elements.Cast<AccordionControlElement>().ToList())
                if (IsProfileGroup(kid)) ClearProfileGroup(kid);
            group.Elements.Clear();
        }

        /// <summary>Every real item of the shell as it is before a profile is applied (default text and default group).</summary>
        private IList<UiMenuItemInfo> CollectMenuItems()
        {
            var items = new List<UiMenuItemInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in EnumerateMenuItems(accordionControl.Elements.Cast<AccordionControlElement>()))
            {
                if (!seen.Add(element.Name)) continue;
                AccordionControlElement owner = element.OwnerElement;
                items.Add(new UiMenuItemInfo
                {
                    ItemKey = element.Name,
                    Caption = string.IsNullOrWhiteSpace(element.Text) ? element.Name : element.Text,
                    GroupCaption = owner == null || owner == mainAccordionGroup ? null : owner.Text
                });
            }
            return items;
        }

        private static IEnumerable<AccordionControlElement> EnumerateMenuItems(IEnumerable<AccordionControlElement> elements)
        {
            foreach (var element in elements)
            {
                if (element.Style == ElementStyle.Item && !string.IsNullOrWhiteSpace(element.Name))
                    yield return element;
                foreach (var child in EnumerateMenuItems(element.Elements.Cast<AccordionControlElement>()))
                    yield return child;
            }
        }

        /// <summary>
        /// Rebuilds the menu while the app is running (after the designer saved or deleted a profile): back to the standard
        /// menu, bring back dynamic UI screens, apply the profile of the signed-in user, restore icons.
        /// </summary>
        private void RebuildMenuProfile()
        {
            try
            {
                accordionControl.BeginUpdate();
                try
                {
                    if (_standardMenu != null) RestoreStandardMenu(_standardMenu);
                    RefreshUiScreenMenu();
                    ApplyMenuProfile();
                    ApplyFluentMenuIcons();
                }
                finally { accordionControl.EndUpdate(); }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("Menu rebuild skipped: " + ex.GetBaseException().Message);
            }
        }

        private void ApplyMenuProfile()
        {
            try
            {
                // Taken once, the first time: this is the standard menu a later rebuild returns to.
                if (_standardMenu == null)
                {
                    _menuItemSnapshot = CollectMenuItems();
                    _standardMenu = CaptureStandardMenu();
                }
                var session = UserSession.Current;
                if (session == null) return;

                var nodes = UiLayoutModule.CreateMenuStore().LoadRuntime(session.UserId);
                if (nodes == null || nodes.Count == 0) return;

                var items = new Dictionary<string, AccordionControlElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var element in EnumerateMenuItems(accordionControl.Elements.Cast<AccordionControlElement>()))
                    if (!items.ContainsKey(element.Name)) items[element.Name] = element;

                var plan = UiMenuResolver.Resolve(nodes, new HashSet<string>(items.Keys, StringComparer.OrdinalIgnoreCase));
                if (plan.Count == 0) return;

                var standardTop = mainAccordionGroup.Elements.Cast<AccordionControlElement>().ToList();
                var touchedOwners = new List<AccordionControlElement>();
                int groupSeq = 0;
                var profileTop = plan.Select(p => BuildProfileElement(p, items, touchedOwners, ref groupSeq)).ToList();

                mainAccordionGroup.Elements.Clear();
                foreach (var element in profileTop) mainAccordionGroup.Elements.Add(element);
                foreach (var element in standardTop) mainAccordionGroup.Elements.Add(element);

                // Standard groups that lost all their visible items disappear; nothing is ever made visible here.
                foreach (var owner in touchedOwners.Distinct())
                    for (var g = owner; g != null && g != mainAccordionGroup; g = g.OwnerElement)
                        if (!g.Elements.Cast<AccordionControlElement>().Any(c => c.Visible)) g.Visible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("Menu profile skipped: " + ex.GetBaseException().Message);
            }
        }

        private static AccordionControlElement BuildProfileElement(UiMenuPlanNode node,
            Dictionary<string, AccordionControlElement> items, List<AccordionControlElement> touchedOwners, ref int groupSeq)
        {
            if (!node.IsGroup)
            {
                var item = items[node.ItemKey];
                if (item.OwnerElement != null)
                {
                    touchedOwners.Add(item.OwnerElement);
                    item.OwnerElement.Elements.Remove(item);
                }
                if (!string.IsNullOrWhiteSpace(node.Caption)) item.Text = node.Caption;
                return item;
            }

            var group = new AccordionControlElement(ElementStyle.Group)
            {
                Name = "menuProfileGroup_" + (++groupSeq),
                Text = node.Caption,
                Expanded = true
            };
            foreach (var child in node.Children)
                group.Elements.Add(BuildProfileElement(child, items, touchedOwners, ref groupSeq));
            group.Visible = group.Elements.Cast<AccordionControlElement>().Any(c => c.Visible);
            return group;
        }

        /// <summary>
        /// Menu designer. After a save or delete the menu of the signed-in user is rebuilt at once; other users get their
        /// new menu the next time their app starts.
        /// </summary>
        private void OpenUiMenuDesigner(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                bool changed;
                using (var form = new UiMenuDesignerForm(UiLayoutModule.CreateMenuDesigner(),
                    () => _menuItemSnapshot ?? CollectMenuItems()))
                {
                    form.ShowDialog(this);
                    changed = form.Saved;
                }
                if (changed) RebuildMenuProfile();
            }, "Thiết kế menu");
        }
    }
}
