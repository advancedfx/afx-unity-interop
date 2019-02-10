# Test version

- Tested with Unity 2018.3.5f1.
- Needs HLAE 2.61.0 (2019-02-xxTxx:xxZ) or newer.

Usage:

1) Use HLAE CustomLoader to inject AfxHookUnity.dll into the Unity.exe and open the example project.

2) Launch CS:GO from HLAE with -afxInterop (case-sensitve option).

3) Load a GOTV demo (the example propject has colorfoul boxes at the T spawn on de_mirage).

4) Run the project in Untiy (Editor).

5) Enter `afx_interop connect 1` into console to connect CS:GO to Unity, then it should drawing the boxes at T-spawn on mirage.


Please understand that this is a test version and things will change heavily!