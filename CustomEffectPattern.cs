using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace ShowGroundEffects;

public class CustomEffectPattern
{
    public ToggleNode Enabled { get; set; } = new ToggleNode(true);

    public TextNode Pattern { get; set; } = new TextNode("");
    public TextNode DisplayName { get; set; } = new TextNode("");
    public ColorNode Color { get; set; } = new ColorNode(new Color(255, 0, 0, 60));

    public CustomEffectPattern() { }

    public CustomEffectPattern(string pattern, string displayName, Color color)
    {
        Pattern = new TextNode(pattern);
        DisplayName = new TextNode(displayName);
        Color = new ColorNode(color);
    }
}
