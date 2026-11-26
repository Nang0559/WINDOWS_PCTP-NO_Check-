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

        private void ApplyMenuProfile()
        {
            try
            {
                _menuItemSnapshot = CollectMenuItems();
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

        /// <summary>Menu designer. Saving changes the stored profile; it takes effect the next time the app starts.</summary>
        private void OpenUiMenuDesigner(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                using (var form = new UiMenuDesignerForm(UiLayoutModule.CreateMenuDesigner(),
                    () => _menuItemSnapshot ?? CollectMenuItems()))
                    form.ShowDialog(this);
            }, "Thiết kế menu");
        }
    }
}
