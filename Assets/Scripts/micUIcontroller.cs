using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class micUIcontroller : MonoBehaviour
{
    public GameObject micOnImage;
    public GameObject micOffImage;

    [SerializeField]
    private bool isMicOn = false;

    public void SetMicStatus(bool micOn)
    {
        isMicOn = micOn;
        UpdateMicDisplay();
    }

    void Start()
    {
        UpdateMicDisplay();
    }

    void UpdateMicDisplay()
    {
        if (micOnImage != null && micOffImage != null)
        {
            micOnImage.SetActive(isMicOn);
            micOffImage.SetActive(!isMicOn);
        }
    }
}
