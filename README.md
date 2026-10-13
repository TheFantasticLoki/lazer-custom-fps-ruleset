![icon](assets/icon.png)

# custom-fps

An osu! ruleset that adds a **per-thread custom frame limiter** to the game.

Originally a patch in osu-plugins osu-patches plugin, then rebuilt natively into a fork of lazer due to osu-plugins blacklisting for unrestricted loading of external code via IL-Weaving; Now reworked into a restricted targeted ruleset providing easier maintenance accross updates while utilizing the osu!framework API, standard ruleset configuration and no IL-Weaving to comply with rules.

Should comply with rules but use at your own risk, osu-plugins was blacklisted with no player bans so in theory you should be okay to use this even if peppy decides to blacklist in the future.


## Installation

1. Build the project with the .NET 10 SDK (`dotnet build osu.Game.Rulesets.CustomFps/osu.Game.Rulesets.CustomFps.csproj -c Release`) or grab a release DLL. In VS Code, run **build (Release)** from the task list.
2. Drop `osu.Game.Rulesets.CustomFps.dll` into your osu! `rulesets` folder (Settings → General → Open rulesets folder).
3. Restart osu! if it was running.

## How to use

1. Open the **Custom frame limiter** section in Settings.
2. For rendering, choose **Use Frame Sync** to keep the game's Graphics → Frame Sync choices (including VSync and refresh-rate multipliers), or choose **Custom** to reveal a custom draw-thread slider.
3. Turn on **Enable update-side limiter** to control update, audio and input threads. Pick a **Preset** (1k / 2× / 4× / 8×) or choose **Custom** to reveal their per-thread sliders.

The render and update-side limiters are independent. **Use Frame Sync** leaves rendering entirely to the game's Graphics settings; **Custom** temporarily overrides its draw-thread rate and restores the current Frame Sync-selected rate when switched back. The update-side toggle overrides update, input and audio regardless of Frame Sync mode.

The settings are wired the first time the ruleset's settings panel is opened in each game session. Open the section once after launch to apply the saved settings; the ruleset API does not provide a cold-start hook.

| Control | Effect |
| --- | --- |
| **Render thread limiter** | **Use Frame Sync** follows Graphics settings; **Custom** shows the draw-thread slider. |
| **Draw thread** | Custom render cap, independent of update-side presets. |
| **Enable update-side limiter** | Turns the update/input/audio limiter on or off. When off, those threads use game-selected limits. |
| **Preset** | Drives update, input and audio together: 1k / 2× / 4× / 8×. Choose **Custom** to show per-thread sliders. Changing presets does not overwrite saved Custom values. |
| **Update thread** | FPS cap for the update (logic) thread — visible in update-side Custom mode. |
| **Audio thread** | FPS cap for the audio thread — visible in update-side Custom mode. |
| **Input thread** | FPS cap for the input thread — visible in update-side Custom mode. |
| **0 on an active custom limit** | Truly unlimited for that thread. A critical warning appears only while an active custom limit is set to 0. Benchmarking only — see below. |

Your values are stored in the game's database alongside your other settings and survive restarts. If the ruleset is removed, its settings are no longer active; database rows may remain and be reused if the ruleset is installed again.

## ⚠️ Warnings

### Report issues upstream *without* this ruleset first

If you find a bug that you suspect is caused by this ruleset:

> **Reproduce it with this ruleset removed before reporting anything on the official osu! GitHub.**

Issues that only occur with this ruleset installed will be closed on the official issue tracker. If it still happens without the ruleset, it is a genuine game bug — report that, and don't mention this project in the report.

### Don't play with 0 (unlimited)

An **active custom limit** set to `0` means that thread runs **completely uncapped**. A saved zero in a hidden Custom slider or an inactive limiter does not apply. Running uncapped can stutter, spike frametimes, and use excess power for no gameplay benefit.

The intended workflow:

1. Enable the relevant limiter, choose **Custom**, and set only the thread(s) you're testing to `0`.
2. Play a demanding map briefly.
3. Watch your frame times and find your **1% lows** (the framerate you dip *to*, not the average).
4. Set your custom limits **slightly above** those 1% low values.
5. Play like that daily. That's the whole point of the tool.

## Roadmap

Ordered by priority (startup → presets → Frame Sync → benchmark):

- **Startup application** — *investigated.* A ruleset alone can't apply limits at cold boot: the only ruleset code that runs at launch (`CreateInstance`/`CreateConfig`) is handed no host, and osu gives rulesets no startup hook on purpose. Limits persist and re-apply whenever the settings panel opens. The planned ruleset-only way to skip opening settings is an optional **apply-on-play** path (engage limits the moment you start a map, via the ruleset's drawables).
- **Preset tiers** — *done.* A **Preset** dropdown (1k / 2× / 4× / 8× / Custom) drives update, input and audio together; the custom sliders only appear for **Custom** and retain their values when presets change.
- **Frame Sync integration** — *reworked.* The update-side limiter has its own on/off toggle and re-asserts when Frame Sync changes. Rendering can follow the official Frame Sync modes and refresh-rate multipliers, or use a separate custom draw-thread limit.
- **Benchmark mode** — measure your own 1% lows in-game and suggest limits automatically.

## Compatibility & credits

- Works on official osu! (lazer) — verified against upstream osu!framework public API only.
- The bundled infinity glyph is [Font Awesome Free 5.15.4](https://fontawesome.com) infinity (CC BY 4.0).
- Code was generated by Mimo-v2.5 (AI Disclosure)

## Licence

MIT — see [LICENSE](LICENSE).