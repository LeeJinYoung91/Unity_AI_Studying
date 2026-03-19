using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.UI;
using System.IO;

public class DigitRecognizer : MonoBehaviour
{
    [Header("AI Model")]
    public ModelAsset modelAsset;

    private Model runtimeModel;
    private Worker worker;
    private Tensor<float> inputTensor;

    [Header("Debug Settings")]
    public GameObject debugImagePrefab;
    public Transform debugUIParent;
    
    void Start()
    {
        if (modelAsset == null) return;
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
        inputTensor = new Tensor<float>(new TensorShape(1, 1, 28, 28));
    }

    public void PredictMultiple(Texture2D fullTexture)
    {
        if (worker == null) return;

        List<Texture2D> cropTextures = SegmentAndSort(fullTexture);

        if (cropTextures.Count == 0)
        {
            Debug.Log("⚠️ 인식된 숫자가 없습니다.");
            return;
        }

        ShowDebugImages(cropTextures);

        List<string> results = new List<string>();

        foreach (var crop in cropTextures)
        {
            int number = RunInference(crop);
            results.Add(number.ToString());
            // Destroy(crop); // 메모리 해제 - Debugging 용으로 임시 주석처리
        }

        string finalResult = string.Join(", ", results);
        Debug.Log($"🤖 AI 결과: [ {finalResult} ]");
    }

    private void ShowDebugImages(List<Texture2D> textures)
    {
        if (debugUIParent != null)
        {
            foreach (Transform child in debugUIParent) Destroy(child.gameObject);
        }

        for (int i = 0; i < textures.Count; i++)
        {
            Texture2D tex = textures[i];

            if (debugUIParent != null && debugImagePrefab != null)
            {
                GameObject obj = Instantiate(debugImagePrefab, debugUIParent);
                RawImage rawImage = obj.GetComponent<RawImage>();
                rawImage.texture = tex;
            }
        }
    }

    private List<Texture2D> SegmentAndSort(Texture2D original)
    {
        // 1. 모든 덩어리(Blob) 찾기
        List<Rect> allBlobs = FindBlobs(original);
        if (allBlobs.Count == 0) return new List<Texture2D>();

        // 2. 줄 단위 정렬 (Y축 -> X축)
        allBlobs.Sort((a, b) => a.y.CompareTo(b.y));
        List<List<Rect>> lines = new List<List<Rect>>();

        while (allBlobs.Count > 0)
        {
            Rect current = allBlobs[0];
            allBlobs.RemoveAt(0);
            List<Rect> currentLine = new List<Rect> { current };

            for (int i = allBlobs.Count - 1; i >= 0; i--)
            {
                Rect other = allBlobs[i];
                
                if (CheckVerticalOverlap(current, other))
                {
                    currentLine.Add(other);
                    allBlobs.RemoveAt(i);
                }
            }
            currentLine.Sort((a, b) => a.x.CompareTo(b.x));
            lines.Add(currentLine);
        }

        List<Texture2D> crops = new List<Texture2D>();

        // 3. 자르기 & 마스킹 (★ 핵심 수정 파트 ★)
        foreach (var line in lines)
        {
            foreach (var myRect in line)
            {
                // A. 정사각형 크기 계산
                float maxSide = Mathf.Max(myRect.width, myRect.height);
                int squareSize = (int)(maxSide * 1.2f); // 1.2배 여유
                
                float centerX = myRect.x + myRect.width / 2f;
                float centerY = myRect.y + myRect.height / 2f;
                int startX = (int)(centerX - squareSize / 2f);
                int startY = (int)(centerY - squareSize / 2f);

                // B. 새 텍스처 생성 (검은색 배경)
                Texture2D crop = new Texture2D(squareSize, squareSize);
                Color[] pixels = new Color[squareSize * squareSize];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.black;
                crop.SetPixels(pixels);

                // C. 픽셀 복사 (마스킹 로직 적용)
                for (int py = 0; py < squareSize; py++)
                {
                    for (int px = 0; px < squareSize; px++)
                    {
                        int globalX = startX + px;
                        int globalY = startY + py;

                        // 원본 범위 체크
                        if (globalX < 0 || globalX >= original.width || 
                            globalY < 0 || globalY >= original.height) continue;

                        Color c = original.GetPixel(globalX, globalY);

                        if (c.r > 0.1f) 
                        {
                            if (IsInsideRect(globalX, globalY, myRect, padding: 2))
                            {
                                crop.SetPixel(px, py, c);
                            }
                        }
                    }
                }
                crop.Apply();
                crops.Add(crop);
            }
        }

        return crops;
    }

    // 픽셀이 사각형 안에 있는지 확인하는 헬퍼 함수
    private bool IsInsideRect(int x, int y, Rect rect, int padding)
    {
        return x >= (rect.x - padding) && 
               x <= (rect.x + rect.width + padding) &&
               y >= (rect.y - padding) && 
               y <= (rect.y + rect.height + padding);
    }

    // ★ 두 박스가 수직(Y축)으로 겹치는지 판단하는 함수
    private bool CheckVerticalOverlap(Rect a, Rect b)
    {
        // Y축 투영했을 때 겹치는 길이 계산
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        float overlapHeight = Mathf.Max(0, yMax - yMin);

        float minHeight = Mathf.Min(a.height, b.height);
        return overlapHeight > (minHeight * 0.7f); 
    }

    // 정사각형으로 잘라내기
    private Texture2D CropSquareTexture(Texture2D original, Rect rect)
    {
        float maxSide = Mathf.Max(rect.width, rect.height);
        int squareSize = (int)(maxSide * 1.2f);
        
        float centerX = rect.x + rect.width / 2f;
        float centerY = rect.y + rect.height / 2f;
        int x = (int)(centerX - squareSize / 2f);
        int y = (int)(centerY - squareSize / 2f);

        Texture2D crop = new Texture2D(squareSize, squareSize);
        Color[] blackPixels = new Color[squareSize * squareSize];
        for(int i=0; i<blackPixels.Length; i++) blackPixels[i] = Color.black;
        crop.SetPixels(blackPixels);

        for (int py = 0; py < squareSize; py++)
        {
            for (int px = 0; px < squareSize; px++)
            {
                int targetX = x + px;
                int targetY = y + py;
                if (targetX >= 0 && targetX < original.width && targetY >= 0 && targetY < original.height)
                {
                    Color c = original.GetPixel(targetX, targetY);
                    if (c.r > 0.1f) crop.SetPixel(px, py, c); 
                }
            }
        }
        crop.Apply();
        return crop;
    }

    private List<Rect> FindBlobs(Texture2D tex)
    {
        List<Rect> rects = new List<Rect>();
        int w = tex.width;
        int h = tex.height;
        
        bool[,] visited = new bool[w, h];

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (!visited[x, y] && tex.GetPixel(x, y).r > 0.5f)
                {
                    Rect blobRect = GetBlobRect(tex, visited, x, y);
                    
                    // 너무 작은 잡티(노이즈)는 무시 (5x5 픽셀 이하)
                    if (blobRect.width > 5 && blobRect.height > 5)
                    {
                        rects.Add(blobRect);
                    }
                }
            }
        }
        return rects;
    }

    private Rect GetBlobRect(Texture2D tex, bool[,] visited, int startX, int startY)
    {
        int w = tex.width;
        int h = tex.height;

        int minX = startX, maxX = startX;
        int minY = startY, maxY = startY;

        // 스택을 이용한 탐색 (재귀보다 안전함)
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        while (stack.Count > 0)
        {
            Vector2Int pos = stack.Pop();
            int cx = pos.x;
            int cy = pos.y;

            // 영역 갱신 (덩어리의 최소/최대 좌표 확장)
            if (cx < minX) minX = cx;
            if (cx > maxX) maxX = cx;
            if (cy < minY) minY = cy;
            if (cy > maxY) maxY = cy;

            // 상하좌우 4방향 탐색
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i];
                int ny = cy + dy[i];

                // 텍스처 범위 안이고 + 방문 안 했고 + 잉크가 있다면
                if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                {
                    if (!visited[nx, ny] && tex.GetPixel(nx, ny).r > 0.5f)
                    {
                        visited[nx, ny] = true;
                        stack.Push(new Vector2Int(nx, ny));
                    }
                }
            }
        }

        // 완성된 사각형 반환 (너비/높이 계산)
        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
    
    private int RunInference(Texture2D texture)
    {
        TextureTransform transform = new TextureTransform();
        transform.SetTensorLayout(TensorLayout.NCHW);
        TextureConverter.ToTensor(texture, inputTensor, transform);
        worker.Schedule(inputTensor);
        using Tensor<float> output = worker.PeekOutput() as Tensor<float>;
        float[] scores = output.DownloadToArray();

        int maxIndex = 0;
        float maxScore = scores[0];
        for (int i = 1; i < scores.Length; i++)
        {
            if (scores[i] > maxScore) { maxScore = scores[i]; maxIndex = i; }
        }
        return maxIndex;
    }
    
    void OnDestroy()
    {
        worker?.Dispose();
        inputTensor?.Dispose();
    }
}