using System;
using UnityEngine;

public class AnimalRecognizeTest : MonoBehaviour
{
    [SerializeField] Texture2D tex;
    [SerializeField] AnimalRecognizer recognizer;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            recognizer.PredictImage(tex);
    }

}
