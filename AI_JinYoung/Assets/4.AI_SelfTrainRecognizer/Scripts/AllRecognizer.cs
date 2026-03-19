using UnityEngine;
using Unity.InferenceEngine;

public class AllRecognizer : MonoBehaviour
{
    [Header("AI Model")]
    public ModelAsset animalModelAsset;
    [Header("Text Asset")]
    public TextAsset classLableAsset;

    private Model runtimeModel;
    private Worker worker;
    private Tensor<float> inputTensor;
    private string[] classLabels;


    void Start()
    {
        if (classLableAsset != null)
        {
            classLabels = classLableAsset.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            Debug.Log($"✅ 텍스트 파일에서 {classLabels.Length}개의 라벨을 성공적으로 불러왔습니다!");
        }
        else
        {
            Debug.LogError("🚨 인스펙터에 정답지(labels.txt) 텍스트 파일을 넣어주세요!");
            return;
        }

        if (animalModelAsset == null)
        {
            Debug.LogError("🚨 모델 에셋이 없습니다!");
            return;
        }

        runtimeModel = ModelLoader.Load(animalModelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);

        inputTensor = new Tensor<float>(new TensorShape(1, 3, 224, 224));

        Debug.Log("🤖 다중 사물 분류기 준비 완료!");
    }

    public void PredictImage(Texture2D image)
    {
        if (image == null)
        {
            Debug.LogWarning("테스트할 사진이 없습니다!");
            return;
        }

        TextureTransform transform = new TextureTransform();
        transform.SetTensorLayout(TensorLayout.NCHW);

        transform.SetChannelSwizzle(ChannelSwizzle.RGBA);

        TextureConverter.ToTensor(image, inputTensor, transform);

        NormalizeTensor(inputTensor);

        worker.Schedule(inputTensor);

        using Tensor<float> output = worker.PeekOutput() as Tensor<float>;
        float[] scores = output.DownloadToArray();

        int bestIndex = 0;
        float bestScore = scores[0];

        for (int i = 1; i < scores.Length; i++)
        {
            if (scores[i] > bestScore)
            {
                bestScore = scores[i];
                bestIndex = i;
            }
        }

        float sumExp = 0f;
        foreach (float score in scores)
        {
            sumExp += Mathf.Exp(score);
        }
        float confidence = (Mathf.Exp(bestScore) / sumExp) * 100f;

        string predictedName = bestIndex < classLabels.Length ? classLabels[bestIndex] : $"알 수 없음 (인덱스 {bestIndex})";

        if (confidence >= 75.0f)
        {
            Debug.Log($"🎉 판독 결과: [{predictedName}] (확신도: {confidence:F1}%)");
        }
        else
        {
            Debug.Log($"🤔 음... [{predictedName}] 같긴 한데, 너무 헷갈리네요. (확신도: {confidence:F1}%밖에 안됨)");
        }
    }

    private void NormalizeTensor(Tensor<float> tensor)
    {
        float[] mean = { 0.485f, 0.456f, 0.406f };
        float[] std = { 0.229f, 0.224f, 0.225f };

        float[] data = tensor.DownloadToArray();
        int pixels = 224 * 224;

        for (int c = 0; c < 3; c++)
        {
            for (int i = 0; i < pixels; i++)
            {
                int index = c * pixels + i;
                data[index] = (data[index] - mean[c]) / std[c];
            }
        }
        tensor.Upload(data);
    }

    void OnDestroy()
    {
        worker?.Dispose();
        inputTensor?.Dispose();
    }
}