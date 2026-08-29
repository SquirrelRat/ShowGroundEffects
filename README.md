# Show Ground Effects

An ExileAPI plugin that highlights dangerous ground effects so you don't have to visually parse what is and isn't lethal in a boss fight.

This is a custom fork of [arturino009/ShowGroundEffects](https://github.com/arturino009/ShowGroundEffects) with added custom effect-pattern detection.

## What it highlights

- **Elemental degen ground** — ground effect entities (`ground_effects`) are colored by their damage type (fire / cold / lightning / chaos / physical) based on their buff descriptions.
- **Exarch runes** — `UberMapExarchDaemon` entities are circled in the fire color.
- **Custom server effects** — anything that is *not* a "ground effect" but still telegraphs danger (e.g. Brine King's lightning strike that charges up then hits a circle, boss slam/explosion build-ups, degen pools). These are matched by their **animation metadata path** against a user-configurable pattern list.

## Custom effect patterns

Server effects don't carry the usual buff data, so the plugin reads each effect's animation metadata path
(`Animated.BaseAnimatedObjectEntity.Path`, e.g. `Metadata/Effects/Spells/monsters_effects/act6/Brine_King/lightning_strike/...`)
and matches your patterns as **case-insensitive substrings** of that path.

### Adding patterns

1. Open the plugin settings.
2. Under **Discovered Ground Effects**, the plugin lists the last 5 unique animation paths it has seen in the current zone (they show up as you play).
3. Hover a path to see the full metadata path, then press **+** to add it as a pattern, or type/paste a path fragment directly into the pattern table.
4. Give it a name and pick a color (all default colors start at alpha 60 so circles are semi-transparent).

Only `Enabled` patterns with a non-empty pattern string are considered.

### Defaults

The built-in defaults are candidates for common encounters (Sirus, Maven, Shaper, Elder, Searing Exarch, slam/explosion build-ups). Only the Brine King lightning strike is confirmed against live data — use the discovery tool to verify the rest in your own encounters.

## Settings

| Setting | Description |
| --- | --- |
| Enable | Master toggle. |
| Render distance | Maximum distance at which effects are drawn. |
| Show Fire / Cold / Lightning / Chaos / Physical damage | Explicit visibility toggles for each degen ground type. |
| Fire / Cold / Lightning / Chaos / Physical color | Colors used for degen ground of each type. Alpha controls opacity. |
| Show Exarch daemon hazards | Toggles the Exarch daemon hazard circles. |
| Debug mode | Shows the buff names of unmatched ground effects. |
| Enable Custom Effect Detection | Master toggle for the custom pattern feature. |
| Custom Effect Patterns | The pattern table: enable flag, path substring, display name, color, size, remove. |

## Building

Targets an ExileAPI build against .NET 10 (see `ExileCore.deps.json`). The project references ExileCore via the `exapiPackage` MSBuild property pointing at your ExileAPI folder:

```
dotnet build -p:exapiPackage=C:\path\to\ExileAPI
```

Requires .NET 10 SDK. Place the source under your ExileAPI's `Plugins/Source` directory (or point `exapiPackage` at it) and ExileAPI will compile/load it.
