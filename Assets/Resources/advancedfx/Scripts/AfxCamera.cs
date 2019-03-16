using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class AfxCamera : MonoBehaviour
{
    public Camera AfxCameraComponent
    {
        get
        {
            return afxCamera;
        }
    }

    void Start()
    {
        afxCamera = GetComponent<Camera>();

        if (null != afxCamera)
        {
            afxCamera.allowHDR = false;
            afxCamera.allowMSAA = false;
            afxCamera.allowDynamicResolution = false;
        }

        GameObject objAfxInterop = GameObject.FindWithTag("AfxInterop");
        if(null != objAfxInterop)
        {
            afxInterop = objAfxInterop.GetComponent<AfxInterop>();
        }
    }

    private Camera afxCamera;
    private AfxInterop afxInterop;
}
