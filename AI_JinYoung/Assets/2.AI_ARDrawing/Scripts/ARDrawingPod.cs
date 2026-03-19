using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ARDrawingPad : MonoBehaviour
{
    [Header("Connections")]
    public DigitRecognizer recognizer;

    [Header("Drawing Settings")]
    public GameObject linePrefab;
    public ARRaycastManager raycastManager;
    public Transform drawingRoot;

    [Header("Capture Settings")]
    public Camera captureCamera;
    public RenderTexture renderTexture;

    private LineRenderer currentLine;
    private List<Vector3> points = new List<Vector3>();

    void Update()
    {
        bool isPressed = false;
        Vector3 inputPosition = Vector3.zero;

#if UNITY_EDITOR
        if (Input.GetMouseButton(0))
        {
            isPressed = true;
            inputPosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            currentLine = null;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ProcessDrawing();
        }
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            isPressed = true;
            inputPosition = touch.position;

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                currentLine = null;
            }
        }
#endif

        if (isPressed)
        {
            Vector3 hitPosition = Vector3.zero;
            List<ARRaycastHit> hits = new List<ARRaycastHit>();

            if (raycastManager.Raycast(inputPosition, hits, TrackableType.PlaneWithinPolygon))
            {
                hitPosition = hits[0].pose.position;
                hitPosition.y += 0.005f;
            }
            else
            {
                hitPosition = Camera.main.ScreenToWorldPoint(new Vector3(inputPosition.x, inputPosition.y, 0.3f));
            }

            if (currentLine == null) CreateLine(hitPosition);
            else UpdateLine(hitPosition);
        }
    }

    void CreateLine(Vector3 position)
    {
        GameObject lineObj = Instantiate(linePrefab, position, Quaternion.identity, drawingRoot);
        lineObj.layer = LayerMask.NameToLayer("Drawing");
        currentLine = lineObj.GetComponent<LineRenderer>();
        points.Clear();
        points.Add(position); points.Add(position);
        currentLine.positionCount = 2;
        currentLine.SetPosition(0, position); currentLine.SetPosition(1, position);
    }

    void UpdateLine(Vector3 position)
    {
        if (Vector3.Distance(points[points.Count - 1], position) < 0.01f) return;
        points.Add(position);
        currentLine.positionCount = points.Count;
        currentLine.SetPosition(points.Count - 1, position);
    }

    public void ProcessDrawing()
    {
        if (recognizer == null)
        {
            Debug.LogError("DigitRecognizer가 연결되지 않았습니다!");
            return;
        }
        Texture2D texture = CaptureDrawingToTexture();

        recognizer.PredictMultiple(texture);

        Destroy(texture);
    }

    private void FitCameraToDrawing()
    {
        LineRenderer[] lines = drawingRoot.GetComponentsInChildren<LineRenderer>();
        if (lines.Length == 0) return;

        // 1. 모든 선을 포함하는 영역(Bounds) 계산
        // 첫 번째 점으로 초기화
        Bounds bounds = new Bounds(lines[0].GetPosition(0), Vector3.zero);
        float maxLineWidth = 0f;

        foreach (var line in lines)
        {
            if (line.startWidth > maxLineWidth) maxLineWidth = line.startWidth;
            for (int i = 0; i < line.positionCount; i++)
            {
                bounds.Encapsulate(line.GetPosition(i));
            }
        }
        
        // 선 두께만큼 영역을 확실히 넓혀줌 (반지름만큼)
        bounds.Expand(maxLineWidth * 1.5f); 

        // 2. 카메라 위치 이동 (중심점 맞추기)
        Vector3 center = bounds.center;
        
        captureCamera.transform.position = new Vector3(center.x, center.y, center.z - 10.0f);
        captureCamera.transform.rotation = Quaternion.identity; // 회전 초기화

        // 3. ★ 짤림 방지 핵심 로직 (Aspect Ratio Math) ★
        
        float textureAspect = (float)renderTexture.width / renderTexture.height;
        
        // 그림의 "반지름" (중심에서 끝까지의 거리)
        float objectVertSize = bounds.extents.y; // 세로 반지름
        float objectHorzSize = bounds.extents.x; // 가로 반지름

        // A. 세로 길이에 맞췄을 때 필요한 카메라 사이즈
        float sizeForHeight = objectVertSize;

        // B. 가로 길이에 맞췄을 때 필요한 카메라 사이즈
        float sizeForWidth = objectHorzSize / textureAspect;

        float requiredSize = Mathf.Max(sizeForHeight, sizeForWidth);

        // 4. 패딩 (여백) 추가
        float padding = 1.5f; 
        
        // 최소 사이즈 제한 (점만 찍었을 때 너무 확대되는 것 방지)
        float minSize = 0.5f;

        // 최종 적용
        captureCamera.orthographicSize = Mathf.Max(requiredSize * padding, minSize);
        
        Debug.Log($"📸 캡처 조정 완료: BoundSize({bounds.size}), CamSize({captureCamera.orthographicSize})");
    }
    
    private void OnDrawGizmos()
    {
        if (captureCamera == null) return;

        // 카메라가 찍는 영역을 초록색 박스로 표시
        Gizmos.color = Color.green;
        Vector3 center = captureCamera.transform.position;
        center.z += 10.0f; // 다시 그림 위치로
        
        float height = captureCamera.orthographicSize * 2;
        float width = height * captureCamera.aspect;

        Gizmos.DrawWireCube(center, new Vector3(width, height, 1));
    }

    private Texture2D CaptureDrawingToTexture()
    {
        FitCameraToDrawing();

        RenderTexture oldRT = RenderTexture.active;
        RenderTexture.active = renderTexture;

        if (!captureCamera.gameObject.activeSelf) captureCamera.gameObject.SetActive(true);

        captureCamera.Render();

        Texture2D resultTex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        resultTex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        resultTex.Apply();

        RenderTexture.active = oldRT;
        return resultTex;
    }

    public void ClearDrawing()
    {
        foreach (Transform child in drawingRoot) Destroy(child.gameObject);
    }
}