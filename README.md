![icon](assets/icon.png)

# custom-fps

An osu! ruleset that adds a **per-thread custom frame limiter** to the game.

> Originally a patch in osu-plugins osu-patches plugin, then rebuilt natively into a fork of lazer due to osu-plugins blacklisting for unrestricted loading of external code via IL-Weaving; Now reworked into a restricted targeted ruleset providing easier maintenance accross updates while utilizing the osu!framework API, standard ruleset configuration and no IL-Weaving to comply with rules.
> 
>Should comply with rules but use at your own risk, osu-plugins was blacklisted with no player bans so in theory you should be okay to use this even if peppy decides to blacklist in the future.

This patch ruleset was created for myself to resolve an issue with a tiny visible delay between my hand and cursor that genuinely just makes it far less enjoyable to play the game at high star ratings where I enjoy the game. Some other benefits did follow like slightly better audio delay for hitsounds providing a closer to stable feel since stable allowed for running at higher fps which even tho single threaded, could run stably at several thousands of frames per second with modern adaquately built hardware.

I've made it available publicly as to share what is helpful to me, some may only see it as placebo while others may notice an improvement immediately, for a proper test I recommend trying with fps display turned off entirely so you can't tell if it's actually engaged or not until you play. My understanding behind it is that even tho the decrease in delay is small, we are still shortening the overall chain of delay as this is not a perfect world where everything syncs up perfectly and brute-force fps will always reign supreme. Personally I only run higher fps on my non-render threads as my gpu is not the best but I still get a far more responsive game due to the frame being rendered by the gpu being newer a majority of the time compared to even the 1ms you get at 1k fps.

A common misconception I often see is, There is no way a human can tell the difference because humans can't react that fast. But this is fortunately not true, because reactions are not what is being aided by a higher fps at these numbers, but rather the consistency of everything, as reducing the time between frames by running at a higher fps reduces the highest possible age of information between threads and the system. The goal being to reduce the overall impact of the medium to your ability to play the game, allowing only raw skill to dictate your performance in game with supporting hardware. But sadly some people are also not built to perceive these small differences in time frames and genuinely think play at even 4ms is fine which is genuinely a blessing as those of us who can easily do so are forever chasing a less restrictive feeling.

My philosophy is, educate the user about the risks, how they should approach the tool, maybe even hide it behind more technical barriers to entry like environment variables but allow the user the freedom to choose.

## Installation

1. Build the project with the .NET 10 SDK (`dotnet build osu.Game.Rulesets.CustomFps/osu.Game.Rulesets.CustomFps.csproj -c Release`) or grab a [release](https://github.com/TheFantasticLoki/lazer-custom-fps-ruleset/releases) DLL. In VS Code, run **build (Release)** from the task list.
2. Drop `osu.Game.Rulesets.CustomFps.dll` into your osu! `rulesets` folder (Settings → General → Open rulesets folder).
3. Restart osu! if it was running.

## How to use

> Options must always be opened at least once after starting the client for ruleset changes to engage due to no startup hook for rulesets.

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

If you find a bug:

> **Try to reproduce it with this ruleset removed before reporting anything**

Issues that only occur with this ruleset installed will be closed on the official issue tracker. If it still happens without the ruleset, it is a genuine game bug. Report that, and don't mention this project in the report. If the issue does not occur without this ruleset you may open an issue on this repo. Micro-stutters and hitching should be expected if you thoughtlessly try to play at the absolute limits of your hardware and should not be reported on either repos. Set your limits properly.

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
