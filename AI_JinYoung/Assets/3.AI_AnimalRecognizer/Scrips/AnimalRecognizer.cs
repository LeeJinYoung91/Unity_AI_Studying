using UnityEngine;
using Unity.InferenceEngine;

public class AnimalRecognizer : MonoBehaviour
{
    [Header("AI Model")]
    public ModelAsset animalModelAsset;    

    private Model runtimeModel;
    private Worker worker;
    private Tensor<float> inputTensor;

    void Start()
    {
        if (animalModelAsset == null)
        {
            return;
        }
        
        runtimeModel = ModelLoader.Load(animalModelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
        
        inputTensor = new Tensor<float>(new TensorShape(1, 3, 224, 224));
        
        Debug.Log("🐶🐱 강아지/고양이 분류기 준비 완료!");
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

        float catScore = scores[0];
        float dogScore = scores[1];

        float expCat = Mathf.Exp(catScore);
        float expDog = Mathf.Exp(dogScore);
        float sum = expCat + expDog;
        
        float catPercent = (expCat / sum) * 100f;
        float dogPercent = (expDog / sum) * 100f;

        if (dogPercent > catPercent)
        {
            Debug.Log($"이것은 [강아지] 입니다! (확신도: {dogPercent:F1}%)");
        }
        else
        {
            Debug.Log($"이것은 [고양이] 입니다! (확신도: {catPercent:F1}%)");
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