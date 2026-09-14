using System;
using System.Data;

namespace PCTP.Presentation.Views
{
    /// <summary>
    /// Compatibility facade for the presenter layer.
    /// The view contract is now split by workflow while this interface keeps
    /// the existing HVN_Presenter dependency unchanged.
    /// </summary>
    public interface IHVNView : IPhieuView, IDocQrView, IGiaoDbView, IYmvnView
    {
      
    }
}
