using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;
using System.Collections.Generic;

namespace ShowGroundEffects;

public class ShowGroundEffectsSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Render distance")]
    public RangeNode<int> RenderDistance { get; set; } = new RangeNode<int>(80, 0, 200);

    [Menu("If you want to hide an element, set the color transparency to max", 100)]
    public EmptyNode Description { get; set; }

    [Menu("Fire damage", parentIndex = 100)]
    public ColorNode FireColor { get; set; } = new ColorNode(WithAlpha60(Color.Red));

    [Menu("Cold damage", parentIndex = 100)]
    public ColorNode ColdColor { get; set; } = new ColorNode(WithAlpha60(Color.Blue));

    [Menu("Lightning damage", parentIndex = 100)]
    public ColorNode LightningColor { get; set; } = new ColorNode(WithAlpha60(Color.Yellow));

    [Menu("Chaos damage", parentIndex = 100)]
    public ColorNode ChaosColor { get; set; } = new ColorNode(WithAlpha60(Color.Purple));

    [Menu("Physical damage", parentIndex = 100)]
    public ColorNode PhysicalColor { get; set; } = new ColorNode(WithAlpha60(Color.Brown));

    [Menu("Ground size multiplier", parentIndex = 100)]
    public RangeNode<float> GroundSizeMultiplier { get; set; } = new RangeNode<float>(1f, 0.1f, 5f);

    [Menu("Debug mode (shows encountered ground effects)")]
    public ToggleNode DebugMode { get; set; } = new ToggleNode(false);

    [Menu("Enable Custom Effect Detection", 200)]
    public ToggleNode EnableCustomEffects { get; set; } = new ToggleNode(true);

    [IgnoreMenu]
    public List<CustomEffectPattern> CustomPatterns { get; set; } = new List<CustomEffectPattern>();

    public static List<CustomEffectPattern> GetDefaultPatterns()
    {
        // Patterns match as case-insensitive substrings of an effect's animation metadata
        // path (Animated.BaseAnimatedObjectEntity.Path), e.g.
        // "Metadata/Effects/Spells/monsters_effects/act6/Brine_King/lightning_strike/...".
        // Use the in-game "Discovered Ground Effects" section to capture real paths.
        // Only Brine King's lightning strike is confirmed against actual game data; the
        // rest are candidates that may need adjusting after discovery.
        return new List<CustomEffectPattern>
        {
            // Brine King (Act 6) - confirmed working: charges up, then strikes in a circle
            new CustomEffectPattern("Brine_King/lightning_strike", "Brine King Lightning", WithAlpha60(Color.Yellow)),

            // Sirus - Desolation of the Awakener is the dangerous degen ground
            new CustomEffectPattern("Sirus/desolation", "Sirus Desolation Ground", WithAlpha60(Color.Purple)),

            // Maven - Gravity Well (cold DOT ground)
            new CustomEffectPattern("Maven/gravity_well", "Maven Gravity Well", WithAlpha60(Color.Cyan)),

            // Shaper - Vortex (cold DOT circles)
            new CustomEffectPattern("Shaper/vortex", "Shaper Vortex", WithAlpha60(Color.Cyan)),

            // Elder - Eldritch Decay (physical DOT ground)
            new CustomEffectPattern("Elder/decay", "Elder Decay", WithAlpha60(Color.DarkMagenta)),

            // Searing Exarch - Searing Rune (fire DOT ground)
            new CustomEffectPattern("Exarch/searing_rune", "Exarch Searing Rune", WithAlpha60(Color.Orange)),
            new CustomEffectPattern("Exarch/flame_wall", "Exarch Flame Wall", WithAlpha60(Color.OrangeRed)),

            // General dangerous buildup/impact patterns (these are commonly reused)
            // Only keeping the most specific ones that indicate actual danger
            new CustomEffectPattern("/slam/buildup", "Slam Buildup", WithAlpha60(Color.Orange)),
            new CustomEffectPattern("/explosion/buildup", "Explosion Buildup", WithAlpha60(Color.Red)),
        };
    }

    private static Color WithAlpha60(Color color) => new Color(color.R, color.G, color.B, (byte)60);
}
