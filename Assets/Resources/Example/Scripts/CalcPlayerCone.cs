using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CalcPlayerCone : MonoBehaviour
{
    /// <summary>
    /// The name of the mirv_calcs vecAng calc to get the position and angles from.
    /// </summary>
    public string afxVecAngCalcName = "afxObserverEye";

    /// <summary>
    /// The name of the mirv_calcs bool calc to get if alive from.
    /// </summary>
    public string afxAliveBoolCalcName = "afxObserverAlive";

    /// <summary>
    /// The name of the mirv_calcs int calc to get the team number from.
    /// </summary>
    public string afxTeamNumberIntCalcName = "afxObserverTeamNumber";

    //

    private AfxInterop.VecAngCalc afxVecAngCalc;
    private AfxInterop.BoolCalc afxAliveBoolCalc;
    private AfxInterop.IntCalc afxTeamNumberIntCalc;

    private Material playerMaterialOther;
    private Material playerMaterialCT;
    private Material playerMaterialT;

    private Transform selfTransform;
    private MeshRenderer selfMeshRenderer;

    public void Awake()
    {
        selfTransform = GetComponent<Transform>();
        selfMeshRenderer = this.GetComponentInChildren<MeshRenderer>();

        this.playerMaterialOther = Resources.Load("Example/Materials/PlayerCone") as Material;
        this.playerMaterialCT = Resources.Load("Example/Materials/PlayerConeCT") as Material;
        this.playerMaterialT = Resources.Load("Example/Materials/PlayerConeT") as Material;
    }

    public void OnEnable()
    {
        afxVecAngCalc = new AfxInterop.VecAngCalc(this, afxVecAngCalcName, AfxInteropVecAngCalcCallback);
        afxAliveBoolCalc = new AfxInterop.BoolCalc(this, afxAliveBoolCalcName, AfxInteropAliveBoolCalcCallback);
        afxTeamNumberIntCalc = new AfxInterop.IntCalc(this, afxTeamNumberIntCalcName, AfxInteropTeamNumberIntCalcCallback);
    }

    public void OnDisable()
    {
        afxVecAngCalc.Dispose();
        afxAliveBoolCalc.Dispose();
        afxTeamNumberIntCalc.Dispose();
    }

    void AfxInteropVecAngCalcCallback(System.Nullable<AfxInterop.AfxInteropVecAngCalcResult> result)
    {
        if (null != selfTransform && result.HasValue)
        {
            selfTransform.localPosition = AfxInterop.ToUnityVector(result.Value.Vector);
            selfTransform.localRotation = AfxInterop.ToUnityQuaternion(result.Value.QAngle);
        }
    }

    void AfxInteropAliveBoolCalcCallback(System.Nullable<AfxInterop.AfxInteropBoolCalcResult> result)
    {
        if (null != selfMeshRenderer)
        {
            selfMeshRenderer.enabled = result.HasValue && result.Value.Result;
        }
    }

    void AfxInteropTeamNumberIntCalcCallback(System.Nullable<AfxInterop.AfxInteropIntCalcResult> result)
    {
        if (null != selfMeshRenderer)
        {
            if (result.HasValue)
            {
                switch (result.Value.Result)
                {
                    case 2:
                        selfMeshRenderer.material = playerMaterialT;
                        return;
                    case 3:
                        selfMeshRenderer.material = playerMaterialCT;
                        return;
                }
            }

            selfMeshRenderer.material = playerMaterialOther;
        }
    }
}
