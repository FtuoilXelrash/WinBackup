using WinBackup.Models;

namespace WinBackup.Engine;

public static class PlanRules
{
    public static bool Allowed(CopyMode mode, PlanItem item, ItemAction action)
    {
        var twoWay = mode == CopyMode.Sync;
        var sameType = item.Source == null || item.Destination == null
                       || item.Source.IsDirectory == item.Destination.IsDirectory;
        return action switch
        {
            ItemAction.None => true,
            ItemAction.CopyToDestination => item.Source != null && sameType,
            ItemAction.CopyToSource => twoWay && item.Destination != null && sameType,
            ItemAction.DeleteDestination => mode != CopyMode.Backup && item.Destination != null,
            ItemAction.DeleteSource => twoWay && item.Source != null,
            _ => false
        };
    }
}
