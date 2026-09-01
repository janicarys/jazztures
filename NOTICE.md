# Third-party notices | Jazztures

This project bundles or depends on the following third-party works. Nothing here is
original to the thesis; the terms below are the authors'.

---

## Salamander Grand Piano V3

**Files:** `Assets/Jazztures/Audio/piano/*.wav`
**Author:** Alexander Holm
**Instrument:** Yamaha C5 grand piano
**Licence:** Creative Commons Attribution 3.0 (CC BY 3.0) — https://creativecommons.org/licenses/by/3.0/
**Source:** https://freepats.zenvoid.org/Piano/acoustic-grand-piano.html

Used by `Jazztures.Audio.SamplerNoteSink` for on-device piano playback. The samples are
unmodified; `SampleLibrary` selects the nearest recorded pitch at runtime and applies a
playback-rate shift. This attribution satisfies the CC BY requirement.

---

## Meta XR All-in-One SDK

**Packages:** `com.meta.xr.sdk.*` (v205.0.0), `com.meta.xr.sdk.audio` (v85.0.0)
**Author:** Meta Platforms, Inc.
**Licence:** Oculus SDK License Agreement — see the package licence files under
`Library/PackageCache/`.

Hand-pose and hand-size data obtained through this SDK is used only to enable hand
tracking in the app and is never persisted off-device (see `CLAUDE.md` §4.1).

---

## Unity

**Engine:** Unity 6000.5.0f1, under the applicable Unity Software licence.
