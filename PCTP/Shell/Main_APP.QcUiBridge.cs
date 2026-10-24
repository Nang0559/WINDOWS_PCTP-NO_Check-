using DevExpress.XtraBars.Navigation;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

namespace PCTP
{
    /// <summary>
    /// QC UI automation bridge (TEST ONLY).
    ///
    /// DevExpress AccordionControl paints its own items and does not expose them in the
    /// UI Automation tree (the UIA dump shows only one Pane "Accordion Menu" with no children),
    /// so Invoke-PctpUiJourney.ps1 cannot find 'NHAccordionControlElement' / 'E_NhapTP'.
    ///
    /// When the environment variable PCTP_QC_UI_BRIDGE=1 is set, this partial adds ONE hidden-in-plain-sight
    /// panel of 1x1 real WinForms buttons: one per AccordionControlElement. A WinForms Control.Name becomes
    /// the UIA AutomationId, so the existing journeys keep using automationId = element.Name unchanged.
    ///   Group element -> Invoke expands it.
    ///   Item element  -> Invoke runs the same handler the real menu click runs (see InvokeElement).
    ///
    /// Without the env var nothing is created: production behaviour is unchanged.
    /// LIMIT: this verifies that each menu entry routes to the right screen. It does not verify
    /// the accordion's own mouse hit-testing.
    /// </summary>
    public partial class Main_APP
    {
        private const string QcUiBridgeEnvVar = "PCTP_QC_UI_BRIDGE";
        private Panel _qcUiBridgePanel;

        internal void InstallQcUiBridgeIfRequested()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(QcUiBridgeEnvVar), "1", StringComparison.Ordinal))
                return;
            if (_qcUiBridgePanel != null)
                return;

            _qcUiBridgePanel = new Panel
            {
                Name = "qcUiBridgePanel",
                AccessibleName = "QC UI Bridge",
                Size = new System.Drawing.Size(1, 1),
                Location = new System.Drawing.Point(0, 0)
            };

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (AccordionControlElement root in accordionControl.Elements)
                AddBridgeButtons(root, seen);

            Controls.Add(_qcUiBridgePanel);
            _qcUiBridgePanel.BringToFront();
        }

        private void AddBridgeButtons(AccordionControlElement element, HashSet<string> seen)
        {
            if (element == null)
                return;

            if (!string.IsNullOrEmpty(element.Name) && seen.Add(element.Name))
            {
                var captured = element;
                var button = new Button
                {
                    Name = element.Name,                 // -> UIA AutomationId
                    AccessibleName = element.Text,       // -> UIA Name
                    Text = string.Empty,
                    TabStop = false,
                    Size = new System.Drawing.Size(1, 1),
                    Location = new System.Drawing.Point(0, 0)
                };
                button.Click += (s, e) => InvokeQcUiElement(captured);
                _qcUiBridgePanel.Controls.Add(button);
            }

            foreach (AccordionControlElement child in element.Elements)
                AddBridgeButtons(child, seen);
        }

        private void InvokeQcUiElement(AccordionControlElement element)
        {
            if (element.Style == ElementStyle.Group)
            {
                element.Expanded = true;
                return;
            }

            // Run the same handler the menu would run: designer wiring is "<Name>_Click".
            MethodInfo handler = GetType().GetMethod(
                element.Name + "_Click",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, new[] { typeof(object), typeof(EventArgs) }, null);

            if (handler == null)
                throw new InvalidOperationException(
                    "QC UI bridge: no handler '" + element.Name + "_Click(object, EventArgs)' on Main_APP. " +
                    "Add an explicit mapping for runtime-created menu items.");

            handler.Invoke(this, new object[] { element, EventArgs.Empty });
        }
    }
}