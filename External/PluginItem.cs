using WController.Util;

namespace WController.External;

public class PluginItem : ItemSelectable
{
    public required string Id { get; set; }
    public required Plugin Plugin { get; set; }
    public override void Open()
    {
        Plugin.DispatchSelectItem(this);
    }
}
