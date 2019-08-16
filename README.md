# Test version

- afx_interop version 5
- Tested with Unity 2019.1.1f1.
- Needs HLAE 2.65.0 (2019-05-01T18:30) or newer.

# Important

Make sure your Unity console does not have the "Error Pause" button pushed in, otherwise the game and Unity will freeze upon exceptions in your other Unity code.

Please understand that this is a test version and things will change heavily!

# Installation

We recommend downloading with git, because that way you will get the submodules and have less traffic upon updates.

Don't forget: `git submodule update` after checking out the latest version.

https://git-scm.com/downloads

```
cd /c/source/folder/here
git clone --no-checkout https://github.com/advancedfx/afx-unity-interop.git
cd afx-unity-interop
git checkout v0.5.2
git submodule update --init
```

# Usage:

1) Launch CS:GO from HLAE with -afxInterop (case-sensitve option).

2) Load a GOTV demo (the example propject has colorfoul boxes at the T spawn on de_mirage).

3) Enter these commands (if you want the observer target cone):  
```
mirv_calcs handle add localPlayer afxLocalPlayer
mirv_calcs handle add observerTarget afxObserverTarget afxLocalPlayer
mirv_calcs vecAng add handleEye afxObserverEye afxObserverTarget
mirv_calcs bool add alive afxObserverAlive afxObserverTarget
mirv_calcs int add teamNumber afxObserverTeamNumber afxObserverTarget
```  
Hint: There's a spectator handle key calc, so you could make a view cone for every player ;)

4) Run the project in Untiy (Editor).

5) Enter `afx_interop connect 1` into console to connect CS:GO to Unity, then it should be drawing the boxes at T-spawn on mirage.

# Known problems:
- Needs queued / threaded rendering, so [mat_queue_mode must not be 0](https://github.com/advancedfx/afx-unity-interop/issues/2).
- Transparent materials will be overdrawn (e.g. smoke particles or worst: if fully in smoke (can be dampened a bit by putting a baseFx stream into preview and setting smokeOverlayAlphaFactor less than 1 on that and forceBuildingCubeMaps 0).

# Planned changes:

Support different rendering order with different passes support, so translucency will be handled better.
