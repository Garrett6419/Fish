using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System;

public class HoverTipManager : MonoBehaviour
{
    public TextMeshProUGUI tipText;
    public RectTransform tipWindow;

    public static Action<string, Vector2> OnMouseHover;
    public static Action OnMouseLoseFocus;

    private void OnEnable()
    {
        OnMouseHover += ShowTip;
        OnMouseLoseFocus += HideTip;
    }

    private void OnDisable()
    {
        OnMouseHover -= ShowTip;
        OnMouseLoseFocus -= HideTip;
    }

    void Start()
    {
        HideTip();
    }

    private void ShowTip(string tip, Vector2 mousePos)
    {
        tipText.text = tip;
        tipWindow.sizeDelta = new Vector2(300.0f, tipText.preferredHeight * 2);

        //Backup
        //tipText.preferredWidth > 300 ? 300 : tipText.preferredWidth

        tipWindow.gameObject.SetActive(true);
        if (mousePos.x < 500)
            tipWindow.transform.position = new Vector2(mousePos.x + 100.0f, mousePos.y);
        else
            tipWindow.transform.position = new Vector2(mousePos.x - 100.0f, mousePos.y);
    }

    private void HideTip()
    {
        tipText.text = default;
        tipWindow.gameObject.SetActive(false);
    }

}
