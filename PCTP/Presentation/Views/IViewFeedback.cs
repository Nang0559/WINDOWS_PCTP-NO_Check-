namespace PCTP.Presentation.Views
{
    /// <summary>
    /// Common non-domain-specific UI feedback contract shared by presenter boundaries.
    /// </summary>
    public interface IViewFeedback
    {
        void ShowLoading(bool show, string caption = "Đang xử lý...");
        void ShowError(string message);
        void ShowInfo(string message);
        void ShowWarning(string message);
        bool Confirm(string message);
    }
}
