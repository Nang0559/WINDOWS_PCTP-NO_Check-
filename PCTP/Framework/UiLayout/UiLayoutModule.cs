using System;
using PCTP.Framework.Documents;

namespace PCTP.Framework.UiLayout
{
    /// <summary>
    /// Composition root of the UI Layout framework (Bravo-style "Khai báo cơ sở dữ liệu / Khai báo layout").
    /// The shell asks this class for services; nothing here grants access: every call is re-checked by RBAC in C#
    /// and again inside SQL (usp_FVN_UiScreen_* / usp_FVN_UiDataSource_*).
    /// </summary>
    public static class UiLayoutModule
    {
        /// <summary>Screen seeded by migration 056. Only screens listed here have a shell menu entry.</summary>
        public const string ComponentPoScreenCode = "COMPONENT_PO";

        /// <summary>RBAC function that owns the VIEW right of the COMPONENT_PO views (see migrations 044 and 056).</summary>
        public const string ComponentPoFunctionCode = "WAREHOUSE.COMPONENT_PO";

        /// <summary>Designer service: VIEW to open, EDIT to save a layout, APPROVE to declare data sources / create screens.</summary>
        public static UiLayoutDesignerService CreateDesigner()
        {
            return new UiLayoutDesignerService(new SqlUiLayoutStore(), new SessionDocumentDesignerAuthorizer());
        }

        /// <summary>Read-only runtime that draws a screen from its approved layout.</summary>
        public static UiScreenRuntime CreateRuntime()
        {
            return UiScreenRuntime.CreateDefault();
        }

        /// <summary>Runtime form for <paramref name="screenCode"/>, wired with the designer so right-click declaration works.</summary>
        public static UiScreenRuntimeForm CreateScreenForm(string screenCode, string siteCode)
        {
            if (string.IsNullOrWhiteSpace(screenCode)) throw new ArgumentException("ScreenCode là bắt buộc.", "screenCode");
            return new UiScreenRuntimeForm(CreateRuntime(), screenCode, siteCode, CreateDesigner);
        }
    }
}
