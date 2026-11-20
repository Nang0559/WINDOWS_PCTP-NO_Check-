using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests
{
    public sealed class ProductionFlowUiContractTests
    {
        private static string Source(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, name);
            Assert.True(File.Exists(path), "Missing UI source fixture: " + path);
            return File.ReadAllText(path);
        }

        [Fact]
        public void ControlTowerUsesSharedFlowAndHoldControlsAndLayout()
        {
            string form = Source("FormMesProductionControlTower.source.cs");
            Assert.Contains("FvnFlowStepperControl", form);
            Assert.Contains("FvnHoldListControl", form);
            Assert.Contains("LayoutControl", form);
            Assert.Contains("OpenSelectedTimeline", form);
            Assert.DoesNotContain("MemoEdit", form);
            Assert.DoesNotContain("SplitterPosition = 840", form);
            Assert.Contains("_refreshTimer.Interval = 5000", form);
        }

        [Fact]
        public void WorkflowNavigationUsesTypedTaskCodeNotVietnameseText()
        {
            string form = Source("FormMesProductionControlTower.source.cs");
            Assert.Contains("switch (task)", form);
            Assert.Contains("ProductionFlowTaskCode.IssueComponents", form);
            Assert.DoesNotContain("task == \"Cấp linh kiện\"", form);
            Assert.DoesNotContain("task == \"Kiểm tra kế hoạch\"", form);
        }

        [Fact]
        public void SharedFlowControlsAreReadOnlyPresentationComponents()
        {
            string stepper = Source("FvnFlowStepperControl.source.cs");
            string holds = Source("FvnHoldListControl.source.cs");
            Assert.Contains("FvnFlowStepState.Blocked", stepper);
            Assert.Contains("FvnFlowStepState.Hold", stepper);
            Assert.Contains("OptionsBehavior.Editable = false", holds);
            Assert.DoesNotContain("SqlConnection", stepper);
            Assert.DoesNotContain("SqlConnection", holds);
        }
    }
}