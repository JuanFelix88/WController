using System.Windows.Forms;

namespace WController.Agent.Rendering;

internal interface IMarkdownRenderer
{
    void Render(string markdown, Control container);

    void RenderAppend(string markdownDelta, Control container);

    void Clear(Control container);
}
