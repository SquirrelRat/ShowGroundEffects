using ExileCore;
using SharpDX;
using System.Collections.Generic;
using System;
using ImGuiNET;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using ExileCore.PoEMemory.MemoryObjects;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using System.Linq;

namespace ShowGroundEffects;

public class ShowGroundEffects : BaseSettingsPlugin<ShowGroundEffectsSettings>
{
    private Camera Camera => GameController.Game.IngameState.Camera;
    private readonly List<string> _buffNames = new List<string>();

    // Last 5 unique discovered animation paths (most recent at end)
    private List<DiscoveredPath> _discoveredPaths = new List<DiscoveredPath>();

    private class DiscoveredPath
    {
        public string FullPath { get; set; } = "";
        public string ShortPath { get; set; } = "";
    }

    public override bool Initialise()
    {
        Name = "Show Ground Effects";

        // Initialize default patterns if empty (first run)
        if (Settings.CustomPatterns == null || Settings.CustomPatterns.Count == 0)
        {
            Settings.CustomPatterns = ShowGroundEffectsSettings.GetDefaultPatterns();
        }

        return true;
    }

    public override void DrawSettings()
    {
        base.DrawSettings();

        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("Discovered Ground Effects:");
        ImGui.TextDisabled("(Last 5 unique buffs)");

        foreach (var str in _buffNames.Take(5))
        {
            ImGui.BulletText(str);
        }

        ImGui.Spacing();

        // Show discovered animation paths with + button
        foreach (var path in _discoveredPaths)
        {
            ImGui.PushID(path.FullPath.GetHashCode());

            if (ImGui.Button("+"))
            {
                var exists = Settings.CustomPatterns.Any(p =>
                    string.Equals(p.Pattern.Value, path.FullPath, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    Settings.CustomPatterns.Add(new CustomEffectPattern(path.FullPath, "Discovered Effect", new Color(255, 0, 0, 60)));
                }
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Add full path to patterns:\n{path.FullPath}");
            }

            ImGui.SameLine();
            ImGui.TextDisabled(path.ShortPath);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(path.FullPath);
            }

            ImGui.PopID();
        }

        if (ImGui.Button("Clear Discovery"))
        {
            _buffNames.Clear();
            _discoveredPaths.Clear();
        }

        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("Custom Effect Patterns:");
        ImGui.Spacing();

        if (ImGui.BeginTable("##patternTable", 6, ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 24);
            ImGui.TableSetupColumn("Pattern", ImGuiTableColumnFlags.WidthStretch, 0.45f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 0.25f);
            ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 110);
            ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed, 64);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableHeadersRow();

            for (var i = 0; i < Settings.CustomPatterns.Count; i++)
            {
                var pattern = Settings.CustomPatterns[i];
                ImGui.PushID(i);

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var enabled = pattern.Enabled.Value;
                if (ImGui.Checkbox("##enabled", ref enabled))
                {
                    pattern.Enabled.Value = enabled;
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                var patternText = pattern.Pattern.Value;
                if (ImGui.InputText("##pattern", ref patternText, 256))
                {
                    pattern.Pattern.Value = patternText;
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                var nameText = pattern.DisplayName.Value;
                if (ImGui.InputText("##name", ref nameText, 128))
                {
                    pattern.DisplayName.Value = nameText;
                }

                ImGui.TableNextColumn();
                var color = new Vector4(pattern.Color.Value.R / 255f, pattern.Color.Value.G / 255f, pattern.Color.Value.B / 255f, pattern.Color.Value.A / 255f);
                if (ImGui.ColorEdit4("##color", ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                {
                    pattern.Color.Value = new Color((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255), (byte)(color.W * 255));
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                var size = pattern.SizeMultiplier.Value;
                if (ImGui.DragFloat("##size", ref size, 0.05f, pattern.SizeMultiplier.Min, pattern.SizeMultiplier.Max, "%.1f"))
                {
                    pattern.SizeMultiplier.Value = size;
                }

                ImGui.TableNextColumn();
                if (ImGui.Button("Remove"))
                {
                    Settings.CustomPatterns.RemoveAt(i);
                    i--;
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();

        if (ImGui.Button("Add Pattern"))
        {
            Settings.CustomPatterns.Add(new CustomEffectPattern("", "New Pattern", new Color(255, 255, 255, 60)));
        }

        ImGui.SameLine();

        if (ImGui.Button("Reset to Defaults"))
        {
            Settings.CustomPatterns = ShowGroundEffectsSettings.GetDefaultPatterns();
        }
    }

    public override void Render()
    {
        try
        {
            if (!Settings.Enable
            || GameController.Area.CurrentArea == null
            || GameController.Area.CurrentArea.IsTown
            || GameController.Area.CurrentArea.IsHideout
            || GameController.IsLoading
            || !GameController.InGame
            || GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal
            )
            {
                return;
            }

            RenderDaemonEffects();
            RenderGroundEffects();

            if (Settings.EnableCustomEffects)
            {
                RenderCustomServerEffects();
            }
        }
        catch { }
    }

    private void RenderDaemonEffects()
    {
        foreach (var daemon in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Daemon])
        {
            if (daemon.Path == null || !daemon.IsHostile) continue;
            if (daemon.DistancePlayer > Settings.RenderDistance) continue;
            if (daemon.Path.Contains("UberMapExarchDaemon"))
            {
                var positioned = daemon.GetComponent<Positioned>();
                if (positioned == null) continue;
                DrawFilledCircleInWorldPosition(GameController.IngameState.Data.ToWorldWithTerrainHeight(positioned.GridPosition), positioned.Size, 1, Settings.FireColor);
            }
        }
    }

    private void RenderGroundEffects()
    {
        foreach (var (effect, positioned) in GetHostileEffectsWithinRange())
        {
            if (!effect.Path.Contains("ground_effects") || effect.Buffs == null) continue;

            var drawColor = Color.Transparent;

            foreach (var buff in effect.Buffs)
            {
                var buffColor = MatchDamageColor(buff.Description);

                if (buffColor != Color.Transparent)
                {
                    drawColor = buffColor;
                }
                else if (Settings.DebugMode)
                {
                    if (!_buffNames.Contains(buff.Name))
                    {
                        _buffNames.Add(buff.Name);
                    }

                    var screenPos = Camera.WorldToScreen(effect.PosNum);
                    Graphics.DrawText(buff.Name, screenPos);
                    var background = new RectangleF(screenPos.X, screenPos.Y, 150, 20);
                    Graphics.DrawBox(background, Color.Black);
                }
            }

            if (drawColor != Color.Transparent)
            {
                DrawFilledCircleInWorldPosition(
                    GameController.IngameState.Data.ToWorldWithTerrainHeight(positioned.GridPosition),
                    positioned.Size * Settings.GroundSizeMultiplier.Value, 1, drawColor);
            }
        }
    }

    private void RenderCustomServerEffects()
    {
        foreach (var (effect, positioned) in GetHostileEffectsWithinRange())
        {
            // Check if this is a ServerEffect with animation data
            if (!effect.Path.Contains("ServerEffect")) continue;

            // Get the animation path from the Animated component
            var animationPath = GetAnimationPath(effect);
            if (string.IsNullOrEmpty(animationPath)) continue;

            // Skip player spells (e.g., Convocation, player skills)
            if (animationPath.Contains("/Spells/") && !animationPath.Contains("monsters_effects")) continue;

            // Track discovered path
            TrackDiscoveredPath(animationPath);

            // Check against custom patterns
            var matchingPattern = GetMatchingPattern(animationPath);
            if (matchingPattern != null)
            {
                DrawFilledCircleInWorldPosition(
                    GameController.IngameState.Data.ToWorldWithTerrainHeight(positioned.GridPosition),
                    positioned.Size * matchingPattern.SizeMultiplier.Value, 1, matchingPattern.Color);
            }
        }
    }

    private IEnumerable<(Entity Effect, Positioned Positioned)> GetHostileEffectsWithinRange()
    {
        foreach (var effect in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Effect])
        {
            if (effect.Path == null || !effect.IsHostile) continue;
            if (effect.DistancePlayer > Settings.RenderDistance) continue;

            var positioned = effect.GetComponent<Positioned>();
            if (positioned == null) continue;

            yield return (effect, positioned);
        }
    }

    private static readonly (string[] Keywords, Func<ShowGroundEffectsSettings, Color> ColorSelector)[] DamageColorMap =
    {
        (new[] { "fire", "burning" }, s => s.FireColor),
        (new[] { "cold" }, s => s.ColdColor),
        (new[] { "lightning" }, s => s.LightningColor),
        (new[] { "chaos" }, s => s.ChaosColor),
        (new[] { "physical" }, s => s.PhysicalColor)
    };

    private Color MatchDamageColor(string description)
    {
        var lower = description.ToLower();

        foreach (var (keywords, colorSelector) in DamageColorMap)
        {
            if (keywords.Any(keyword => lower.Contains(keyword)))
            {
                return colorSelector(Settings);
            }
        }

        return Color.Transparent;
    }

    private string GetAnimationPath(Entity entity)
    {
        var animated = entity.GetComponent<Animated>();
        return animated?.BaseAnimatedObjectEntity?.Path;
    }

    private void TrackDiscoveredPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        if (_discoveredPaths.All(p => p.FullPath != path))
        {
            _discoveredPaths.Add(new DiscoveredPath
            {
                FullPath = path,
                ShortPath = GetShortPath(path)
            });

            // Keep only last 5
            if (_discoveredPaths.Count > 5)
            {
                _discoveredPaths.RemoveAt(0);
            }
        }
    }

    private string GetShortPath(string fullPath)
    {
        // Extract just the filename or last meaningful part
        // e.g., "Metadata/Effects/Spells/monsters_effects/act6/Brine_King/lightning_strike/buildup.ao"
        // becomes "Brine_King/lightning_strike/buildup.ao"

        if (string.IsNullOrEmpty(fullPath)) return "";

        // Try to find the last few meaningful segments
        var parts = fullPath.Split('/');
        if (parts.Length <= 2) return fullPath;

        // Take last 3 parts or less if not available
        int takeCount = Math.Min(3, parts.Length);
        return string.Join("/", parts.Skip(parts.Length - takeCount));
    }

    private CustomEffectPattern GetMatchingPattern(string animationPath)
    {
        if (string.IsNullOrEmpty(animationPath)) return null;

        foreach (var pattern in Settings.CustomPatterns)
        {
            if (!pattern.Enabled.Value) continue;
            if (string.IsNullOrEmpty(pattern.Pattern.Value)) continue;

            // Simple substring match (case-insensitive)
            if (animationPath.IndexOf(pattern.Pattern.Value, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return pattern;
            }
        }

        return null;
    }

    private void DrawFilledCircleInWorldPosition(Vector3 position, float radius, int thickness, Color color)
    {
        var circlePoints = new List<Vector2>();
        const int segments = 15;
        const float segmentAngle = 2f * MathF.PI / segments;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * segmentAngle;
            var currentOffset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var nextOffset = new Vector2(MathF.Cos(angle + segmentAngle), MathF.Sin(angle + segmentAngle)) * radius;

            var currentWorldPos = position + new Vector3(currentOffset, 0);
            var nextWorldPos = position + new Vector3(nextOffset, 0);

            circlePoints.Add(Camera.WorldToScreen(currentWorldPos));
            circlePoints.Add(Camera.WorldToScreen(nextWorldPos));
        }

        Graphics.DrawConvexPolyFilled(circlePoints.ToArray(), color);
    }
}
