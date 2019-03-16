# Test version

- Tested with Unity 2018.3.8f1.
- Needs HLAE 2.62.0 (2019-02-12T17:00Z) or newer.

# Important

Make sure your Unity console does not have the "Error Pause" button pushed in, otherwise the game and Unity will freeze upon exceptions in your other Unity code.

Please understand that this is a test version and things will change heavily!

# Usage:

1) Use HLAE CustomLoader to inject AfxHookUnity.dll into the Unity.exe and open the example project.

2) Launch CS:GO from HLAE with -afxInterop (case-sensitve option).

3) Load a GOTV demo (the example propject has colorfoul boxes at the T spawn on de_mirage).

4) Run the project in Untiy (Editor).

5) Enter `afx_interop connect 1` into console to connect CS:GO to Unity, then it should be drawing the boxes at T-spawn on mirage.

# Known problems:

- FPS drops at round start
- Transparent materials will be overdrawn (e.g. smoke particles or worst: if fully in smoke (can be dampened a bit by putting a baseFx stream into preview and setting smokeOverlayAlphaFactor less than 1 on that and forceBuildingCubeMaps 0).