using ExileCore;
using SharpDX;
using System.Collections.Generic;
using System;
using ImGuiNET;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Nodes;
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
    private readonly HashSet<string> _knownBuffNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Vector2[] _circlePoints = new Vector2[30];
    private bool _resetPatternsOpen;

    // Last 5 unique discovered animation paths (most recent first)
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
        ImGui.TextColored(Settings.Enable ? new Vector4(0.49f, 1f, 0.25f, 1f) : new Vector4(0.65f, 0.65f, 0.7f, 1f),
            Settings.Enable ? "SHOW GROUND EFFECTS  /  ACTIVE" : "SHOW GROUND EFFECTS  /  PAUSED");
        ImGui.SameLine();
        HelpMarker("Highlights dangerous ground effects. Toggles control visibility; colors control appearance.");
        ImGui.Separator();

        if (!ImGui.BeginTabBar("##groundEffectsTabs")) return;
        if (ImGui.BeginTabItem("Overview")) { DrawOverviewTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Damage Types")) { DrawDamageTypesTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Custom Patterns")) { DrawCustomPatternsTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Discovery")) { DrawDiscoveryTab(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Diagnostics")) { DrawDiagnosticsTab(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    private void DrawOverviewTab()
    {
        ImGui.TextWrapped("Make dangerous ground effects obvious without relying on transparency as a hidden disable switch.");
        ImGui.Spacing();

        var cardWidth = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2f;
        DrawStatusCard("Plugin", Settings.Enable ? "Enabled" : "Disabled", Settings.Enable ? Color.LawnGreen : Color.Gray, cardWidth);
        ImGui.SameLine();
        DrawStatusCard("Visibility", $"{GetEnabledDamageTypeCount()} damage types", Color.Cyan, cardWidth);

        Section("Common Settings");
        DrawToggle("Enable Plugin", Settings.Enable, "Master switch for all effect rendering.");
        DrawSlider("Render Distance (m)", Settings.RenderDistance, "Effects farther than this distance are ignored.");
        DrawSlider("Ground Size Multiplier", Settings.GroundSizeMultiplier, "Scales built-in ground-effect circles.");
        DrawToggle("Show Exarch Daemon Hazards", Settings.ShowExarchDaemons, "Shows the Exarch daemon hazard circles.");
        DrawToggle("Enable Custom Effect Detection", Settings.EnableCustomEffects, "Scans server-effect animation paths against your custom patterns.");

        Section("Quick Guide");
        ImGui.BulletText("Use Damage Types to enable or hide fire, cold, lightning, chaos, and physical effects.");
        ImGui.BulletText("Use Custom Patterns for effects that do not expose normal ground-effect buffs.");
        ImGui.BulletText("Use Discovery to inspect unknown ground buffs and animation paths seen in the current zone.");
    }

    private void DrawDamageTypesTab()
    {
        ImGui.TextWrapped("Each damage type has its own visibility toggle and color. Set opacity for appearance only; use the checkbox to disable a type.");
        ImGui.Spacing();
        DrawDamageTypeRow("Fire", Settings.ShowFireDamage, Settings.FireColor, "Fire and burning ground effects.");
        DrawDamageTypeRow("Cold", Settings.ShowColdDamage, Settings.ColdColor, "Cold ground effects.");
        DrawDamageTypeRow("Lightning", Settings.ShowLightningDamage, Settings.LightningColor, "Lightning ground effects.");
        DrawDamageTypeRow("Chaos", Settings.ShowChaosDamage, Settings.ChaosColor, "Chaos ground effects.");
        DrawDamageTypeRow("Physical", Settings.ShowPhysicalDamage, Settings.PhysicalColor, "Physical ground effects.");

        ImGui.Spacing();
        DrawSlider("Ground Size Multiplier", Settings.GroundSizeMultiplier, "Scales all built-in ground-effect circles.");
    }

    private void DrawDamageTypeRow(string label, ToggleNode enabled, ColorNode color, string help)
    {
        ImGui.PushID(label);
        DrawToggle(label, enabled, help);
        ImGui.SameLine(180f);
        DrawColor("Color", color, "Opacity controls how strongly the effect is drawn.");
        ImGui.PopID();
    }

    private static void HelpMarker(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(text);
    }

    private static void DrawToggle(string label, ToggleNode node, string help = null)
    {
        var value = node.Value;
        if (ImGui.Checkbox(label, ref value)) node.Value = value;
        if (!string.IsNullOrWhiteSpace(help)) HelpMarker(help);
    }

    private static void DrawColor(string label, ColorNode node, string help = null)
    {
        var color = node.Value;
        var value = new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        if (ImGui.ColorEdit4(label, ref value))
            node.Value = new Color((byte)(value.X * 255), (byte)(value.Y * 255), (byte)(value.Z * 255), (byte)(value.W * 255));
        if (!string.IsNullOrWhiteSpace(help)) HelpMarker(help);
    }

    private static void DrawSlider(string label, RangeNode<int> node, string help = null)
    {
        var value = node.Value;
        if (ImGui.SliderInt($"{label}: {value}##{label}", ref value, node.Min, node.Max)) node.Value = value;
        if (!string.IsNullOrWhiteSpace(help)) HelpMarker(help);
    }

    private static void DrawSlider(string label, RangeNode<float> node, string help = null)
    {
        var value = node.Value;
        if (ImGui.SliderFloat($"{label}: {value:0.0}##{label}", ref value, node.Min, node.Max)) node.Value = value;
        if (!string.IsNullOrWhiteSpace(help)) HelpMarker(help);
    }

    private int GetEnabledDamageTypeCount()
    {
        var count = 0;
        if (Settings.ShowFireDamage) count++;
        if (Settings.ShowColdDamage) count++;
        if (Settings.ShowLightningDamage) count++;
        if (Settings.ShowChaosDamage) count++;
        if (Settings.ShowPhysicalDamage) count++;
        return count;
    }

    private static Vector4 ToImGuiColor(Color color) =>
        new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

    private static void Section(string title) => ImGui.SeparatorText(title);

    private void DrawStatusCard(string title, string value, Color color, float width)
    {
        ImGui.BeginChild($"##card_{title}", new Vector2(width, 58f), ImGuiChildFlags.Border, ImGuiWindowFlags.NoScrollbar);
        ImGui.PushStyleColor(ImGuiCol.Text, ToImGuiColor(color));
        ImGui.Text(title);
        ImGui.PopStyleColor();
        ImGui.TextDisabled(value);
        ImGui.EndChild();
    }

    private void DrawCustomPatternsTab()
    {
        DrawCustomPatternsEditor();
    }

    private void DrawDiscoveryTab()
    {
        ImGui.TextWrapped("Unknown ground buffs and animation paths are collected while Debug Mode is enabled. Add a discovered path as a custom pattern when you have confirmed it is dangerous.");
        DrawDiscoveryList();
    }

    private void DrawDiscoveryList()
    {
        ImGui.Text("Discovered Ground Effects");
        ImGui.TextDisabled("Latest 5 unique unknown buffs and animation paths");
        foreach (var name in _buffNames)
            ImGui.BulletText(name);

        ImGui.Spacing();
        foreach (var path in _discoveredPaths)
        {
            ImGui.PushID(path.FullPath);
            if (ImGui.Button("+"))
            {
                var exists = Settings.CustomPatterns.Any(p =>
                    string.Equals(p.Pattern.Value, path.FullPath, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                    Settings.CustomPatterns.Add(new CustomEffectPattern(path.FullPath, "Discovered Effect", new Color(255, 0, 0, 60)));
            }
            HelpMarker($"Add full path to patterns:\n{path.FullPath}");
            ImGui.SameLine();
            ImGui.TextDisabled(path.ShortPath);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(path.FullPath);
            ImGui.PopID();
        }

        if (ImGui.Button("Clear Discovery"))
        {
            _buffNames.Clear();
            _knownBuffNames.Clear();
            _discoveredPaths.Clear();
        }
    }

    private void DrawDiagnosticsTab()
    {
        DrawToggle("Debug Mode", Settings.DebugMode, "Shows unmatched buff names over ground effects and enables discovery collection.");
        DrawToggle("Enable Custom Effect Detection", Settings.EnableCustomEffects);
        Section("Current Configuration");
        ImGui.BulletText($"Damage types enabled: {GetEnabledDamageTypeCount()} / 5");
        ImGui.BulletText($"Custom patterns: {Settings.CustomPatterns.Count(p => p.Enabled.Value && !string.IsNullOrWhiteSpace(p.Pattern.Value))} enabled / {Settings.CustomPatterns.Count} total");
        ImGui.BulletText($"Recent unknown buffs: {_buffNames.Count} / 5");
        ImGui.BulletText($"Recent animation paths: {_discoveredPaths.Count} / 5");
        ImGui.TextWrapped("Rendering errors are isolated by the render loop. Enable Debug Mode when investigating an effect that is not being detected.");
    }

    private void DrawCustomPatternsEditor()
    {
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
            _resetPatternsOpen = true;
        if (_resetPatternsOpen)
            ImGui.OpenPopup("Reset custom patterns?");
        if (ImGui.BeginPopupModal("Reset custom patterns?", ref _resetPatternsOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped("This removes all custom pattern changes and restores the built-in patterns.");
            if (ImGui.Button("Reset Patterns"))
            {
                Settings.CustomPatterns = ShowGroundEffectsSettings.GetDefaultPatterns();
                _resetPatternsOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                _resetPatternsOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
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

            RenderEffects();
        }
        catch { }
    }

    private void RenderDaemonEffects()
    {
        if (!Settings.ShowExarchDaemons) return;

        if (!GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Daemon, out var daemons)) return;
        foreach (var daemon in daemons)
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

    private void RenderEffects()
    {
        RenderDaemonEffects();
        if (!GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Effect, out var effects)) return;

        foreach (var effect in effects)
        {
            if (effect.Path == null || !effect.IsHostile || effect.DistancePlayer > Settings.RenderDistance) continue;
            var positioned = effect.GetComponent<Positioned>();
            if (positioned == null) continue;

            if (effect.Path.Contains("ground_effects") && effect.Buffs != null)
                RenderGroundEffect(effect, positioned);

            if (Settings.EnableCustomEffects && effect.Path.Contains("ServerEffect"))
            {
                var animationPath = GetAnimationPath(effect);
                if (!string.IsNullOrEmpty(animationPath) &&
                    (!animationPath.Contains("/Spells/") || animationPath.Contains("monsters_effects")))
                {
                    TrackDiscoveredPath(animationPath);
                    var matchingPattern = GetMatchingPattern(animationPath);
                    if (matchingPattern != null)
                        DrawFilledCircleInWorldPosition(
                            GameController.IngameState.Data.ToWorldWithTerrainHeight(positioned.GridPosition),
                            positioned.Size * matchingPattern.SizeMultiplier.Value, 1, matchingPattern.Color);
                }
            }
        }
    }

    private void RenderGroundEffect(Entity effect, Positioned positioned)
    {
        var drawColor = Color.Transparent;
        foreach (var buff in effect.Buffs)
        {
            if (drawColor == Color.Transparent && TryGetDamageColor(buff.Description, out var buffColor))
                drawColor = buffColor;
            else if (Settings.DebugMode && !string.IsNullOrWhiteSpace(buff.Name))
            {
                if (_knownBuffNames.Add(buff.Name))
                {
                    _buffNames.Insert(0, buff.Name);
                    if (_buffNames.Count > 5) _buffNames.RemoveAt(_buffNames.Count - 1);
                }

                var screenPos = Camera.WorldToScreen(effect.PosNum);
                Graphics.DrawBox(new RectangleF(screenPos.X, screenPos.Y, 150, 20), Color.Black);
                Graphics.DrawText(buff.Name, screenPos);
            }
        }

        if (drawColor != Color.Transparent)
            DrawFilledCircleInWorldPosition(
                GameController.IngameState.Data.ToWorldWithTerrainHeight(positioned.GridPosition),
                positioned.Size * Settings.GroundSizeMultiplier.Value, 1, drawColor);
    }

    private static readonly (string[] Keywords, Func<ShowGroundEffectsSettings, bool> Enabled, Func<ShowGroundEffectsSettings, Color> ColorSelector)[] DamageColorMap =
    {
        (new[] { "fire", "burning" }, s => s.ShowFireDamage, s => s.FireColor),
        (new[] { "cold" }, s => s.ShowColdDamage, s => s.ColdColor),
        (new[] { "lightning" }, s => s.ShowLightningDamage, s => s.LightningColor),
        (new[] { "chaos" }, s => s.ShowChaosDamage, s => s.ChaosColor),
        (new[] { "physical" }, s => s.ShowPhysicalDamage, s => s.PhysicalColor)
    };

    private bool TryGetDamageColor(string description, out Color color)
    {
        color = Color.Transparent;
        if (string.IsNullOrWhiteSpace(description)) return false;

        foreach (var (keywords, enabled, colorSelector) in DamageColorMap)
        {
            if (enabled(Settings) && keywords.Any(keyword => description.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                color = colorSelector(Settings);
                return true;
            }
        }

        return false;
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
        const int segments = 15;
        const float segmentAngle = 2f * MathF.PI / segments;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * segmentAngle;
            var currentOffset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var nextOffset = new Vector2(MathF.Cos(angle + segmentAngle), MathF.Sin(angle + segmentAngle)) * radius;

            var currentWorldPos = position + new Vector3(currentOffset, 0);
            var nextWorldPos = position + new Vector3(nextOffset, 0);

            _circlePoints[i * 2] = Camera.WorldToScreen(currentWorldPos);
            _circlePoints[i * 2 + 1] = Camera.WorldToScreen(nextWorldPos);
        }

        Graphics.DrawConvexPolyFilled(_circlePoints, color);
    }
}
