# Test version

- Tested with Unity 2018.3.8f1.
- Needs HLAE 2.64.0 (2019-04-14T18:00Z) or newer.

# Breaking change:

- If you convert a version 2 to version 3 (current) project, then you need to scale everything by 12/16 (because 1 Unit is 1/16 foot = 12/16 inch and not 1 inch) and this has been corrected.

# Important

Make sure your Unity console does not have the "Error Pause" button pushed in, otherwise the game and Unity will freeze upon exceptions in your other Unity code.

Please understand that this is a test version and things will change heavily!

# Usage:

1) Use HLAE CustomLoader to inject AfxHookUnity.dll into the Unity.exe and open the example project.

2) Launch CS:GO from HLAE with -afxInterop (case-sensitve option).

3) Load a GOTV demo (the example propject has colorfoul boxes at the T spawn on de_mirage).

4) Enter these commands (if you want the observer target cone):  
```
mirv_calcs handle add localPlayer afxLocalPlayer
mirv_calcs handle add observerTarget afxObserverTarget afxLocalPlayer
mirv_calcs vecAng add handleEye afxObserverEye afxObserverTarget
mirv_calcs bool add alive afxObserverAlive afxObserverTarget
mirv_calcs int add teamNumber afxObserverTeamNumber afxObserverTarget
```  
Hint: There's a spectator handle key calc, so you could make a view cone for every player ;)

5) Run the project in Untiy (Editor).

6) Enter `afx_interop connect 1` into console to connect CS:GO to Unity, then it should be drawing the boxes at T-spawn on mirage.

# Known problems:

- Transparent materials will be overdrawn (e.g. smoke particles or worst: if fully in smoke (can be dampened a bit by putting a baseFx stream into preview and setting smokeOverlayAlphaFactor less than 1 on that and forceBuildingCubeMaps 0).

# Planned changes:

Support different rendering order with different passes support, so translucency will be handled better.