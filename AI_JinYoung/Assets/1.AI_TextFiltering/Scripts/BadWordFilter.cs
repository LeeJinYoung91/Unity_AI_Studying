using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading.Tasks;

public class BadWordFilter : MonoBehaviour
{
    [Header("Assets")]
    public ModelAsset modelAsset; 
    public TextAsset vocabFile;   

    private Model runtimeModel;
    private Worker worker;
    
    private Dictionary<string, int> vocab;
    private int vocabSize;

    void Start()
    {
        if (vocabFile) {
            vocab = JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabFile.text);
            vocabSize = vocab.Count + 1;
        }

        runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
        
        Debug.Log("✅ AI 필터 로드 완료 (InferenceEngine)");
    }

    public async Task<bool> IsBadWord(string text)
    {
        float[] inputVector = new float[vocabSize];
        foreach (char c in text)
        {
            string s = c.ToString();
            if (vocab.ContainsKey(s)) inputVector[vocab[s]] = 1.0f;
        }

        using Tensor inputTensor = new Tensor<float>(new TensorShape(1, vocabSize), inputVector);
        
        worker.Schedule(inputTensor);

        using Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
        
        await outputTensor.ReadbackAndCloneAsync();
        float[] scores = outputTensor.DownloadToArray();

        // scores[0]: 정상, scores[1]: 욕설
        return scores[1] > scores[0]; 
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}