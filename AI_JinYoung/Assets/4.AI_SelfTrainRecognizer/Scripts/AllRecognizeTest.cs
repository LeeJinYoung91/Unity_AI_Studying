using System;
using UnityEngine;

public class AllRecognizeTest : MonoBehaviour
{
    [SerializeField] Texture2D tex;
    [SerializeField] AllRecognizer recognizer;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            recognizer.PredictImage(tex);
    }
}
