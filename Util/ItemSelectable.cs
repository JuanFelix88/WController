using System.Drawing;

namespace WController.Util;

public class ItemSelectable
{
    public required string Name { get; set; }
    public required Image Image { get; set; }
    public virtual void Open() { }

    public override string ToString()
    {
        return Name;
    }
}
