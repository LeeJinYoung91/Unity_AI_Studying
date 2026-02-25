using UnityEngine;

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

public class BadWordFilter : MonoBehaviour
{
    [Header("AI Model File")]
    public Unity.InferenceEngine.ModelAsset modelAsset; // 여기에 BadWordFilter.onnx 드래그 앤 드롭
    
    [Header("Vocab File")]
    public TextAsset vocabFile;   // 여기에 vocab.json 드래그 앤 드롭

    private Unity.InferenceEngine.Model runtimeModel;
    private IWorker worker;
    private Dictionary<string, int> vocab;
    private int vocabSize;

    void Start()
    {
        // 1. 단어장(JSON) 로드
        LoadVocab();

        // 2. 모델 로드 및 워커 생성
        runtimeModel = Unity.InferenceEngine.ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(Unity.InferenceEngine.BackendType.GPUCompute, runtimeModel);
        
        Debug.Log("✅ AI 필터링 시스템 준비 완료!");
    }

    void LoadVocab()
    {
        // JSON 파싱 (Dictionary로 변환)
        vocab = JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabFile.text);
        
        // 0번(UNK)을 포함해야 하므로 +1
        vocabSize = vocab.Count + 1; 
    }

    // ★ 외부에서 호출할 함수
    public bool IsBadWord(string text)
    {
        // 1. 텍스트 -> 벡터 변환 (파이썬과 동일한 로직)
        float[] inputVector = new float[vocabSize];
        
        foreach (char c in text)
        {
            string charStr = c.ToString();
            if (vocab.ContainsKey(charStr))
            {
                int index = vocab[charStr];
                inputVector[index] = 1.0f; // 해당 글자가 있으면 1
            }
        }

        // 2. 텐서(Tensor) 생성
        using TensorFloat inputTensor = new TensorFloat(new Unity.InferenceEngine.TensorShape(1, vocabSize), inputVector);

        // 3. AI 예측 실행
        worker.Execute(inputTensor);

        // 4. 결과 받기
        using TensorFloat outputTensor = worker.PeekOutput() as TensorFloat;
        
        // GPU 데이터를 CPU로 가져옴
        outputTensor.MakeReadable();
        float[] scores = outputTensor.ToReadOnlyArray();

        // scores[0]: 정상 확률, scores[1]: 욕설 확률
        float normalScore = scores[0];
        float badScore = scores[1];

        // 5. 결과 판단 (욕설 점수가 더 크면 true)
        bool isBad = badScore > normalScore;
        
        Debug.Log($"입력: {text} | 정상: {normalScore:F2}, 욕설: {badScore:F2} => {(isBad ? "🚨차단" : "✅통과")}");
        
        return isBad;
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}