using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class AfxCanvasScaler : CanvasScaler
{
    protected override void HandleScaleWithScreenSize()
    {
        base.HandleScaleWithScreenSize();

        Canvas canvas = this.GetComponent<Canvas>();        

        if (null != canvas)
        {
            Camera worldCamera = canvas.worldCamera;

            if (null != worldCamera)
            {
                RectTransform rectTransform = this.GetComponent<RectTransform>();

                if (null != rectTransform)
                {
                    float width = worldCamera.rect.width;
                    float height = worldCamera.rect.height;

                    float scale = this.scaleFactor;
                    rectTransform.offsetMin = new Vector2(-width / 2.0f, -height / 2.0f);
                    rectTransform.offsetMax = new Vector2(width / 2.0f, height / 2.0f);
                    //rectTransform.localScale = new Vector2(1.0f / (height * scale), 1.0f / (height * scale));
                }
            }

        }
    }
}
