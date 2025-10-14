using UnityEngine;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;

public class ForceFullModel : MonoBehaviour
{
    // Awake() - самый первый метод, который вызывается при старте
    void Awake()
    {
        var runner = GetComponent<PoseLandmarkerRunner>();
        if (runner != null)
        {
            runner.config.Model = ModelType.BlazePoseFull;
            Debug.Log("<color=green>МОДЕЛЬ ПРИНУДИТЕЛЬНО ИЗМЕНЕНА НА: " + runner.config.Model + " (через Awake)</color>");
        }
    }
}