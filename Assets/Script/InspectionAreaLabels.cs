using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class InspectionAreaLabels : MonoBehaviour
{
    [System.Serializable]
    public struct LabelTarget
    {
        public string labelText;
        public Transform targetTransform;
        public Vector3 localOffset;
    }

    [Header("Target Inspection Areas")]
    public LabelTarget[] labelTargets = new LabelTarget[]
    {
        new LabelTarget { labelText = "FALLEN BOTTLE CHECK", localOffset = new Vector3(0, 1.2f, 0) },
        new LabelTarget { labelText = "HEIGHT CHECK", localOffset = new Vector3(0, 1.2f, 0) },
        new LabelTarget { labelText = "CONTAINER VERIFIER", localOffset = new Vector3(0, 1.2f, 0) },
        new LabelTarget { labelText = "REJECTION AREA", localOffset = new Vector3(0, 1.2f, 0) }
    };

    [Header("Label Styling")]
    public Vector2 labelSize = new Vector2(280f, 60f);
    public int fontSize = 20;
    public Color textColor = Color.white;
    public Color backgroundColor = new Color(0.1f, 0.12f, 0.15f, 0.85f);
    public bool billboardToCamera = true;

    [Header("Container Folder")]
    public string containerName = "_InspectionLabelsContainer";

    private Transform _labelsContainer;

    private void Start()
    {
        AutoFindTargetsIfMissing();
        GenerateLabels();
    }

    private void LateUpdate()
    {
        if (billboardToCamera && _labelsContainer != null)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) mainCam = FindFirstObjectByType<Camera>();

            if (mainCam != null)
            {
                foreach (Transform child in _labelsContainer)
                {
                    child.rotation = Quaternion.LookRotation(child.position - mainCam.transform.position);
                }
            }
        }
    }

    [ContextMenu("Auto-Find Targets From Hierarchy")]
    public void AutoFindTargetsIfMissing()
    {
        Transform inspectionSys = transform.Find("DummyInspectionSys") ?? transform;

        string[] defaultNames = { "Fallen_BottleCheck", "Height_Check", "ContainerVerify", "RejectionUnit" };
        string[] displayLabels = { "FALLEN BOTTLE CHECK", "HEIGHT CHECK", "CONTAINER VERIFIER", "REJECTION AREA" };

        labelTargets = new LabelTarget[defaultNames.Length];

        for (int i = 0; i < defaultNames.Length; i++)
        {
            Transform found = FindChildRecursive(inspectionSys, defaultNames[i]);
            labelTargets[i] = new LabelTarget
            {
                labelText = displayLabels[i],
                targetTransform = found,
                localOffset = new Vector3(0, 1.2f, 0)
            };
        }
    }

    [ContextMenu("Generate Labels")]
    public void GenerateLabels()
    {
        ClearLabels();

        GameObject containerObj = new GameObject(containerName);
        containerObj.transform.SetParent(transform, false);
        _labelsContainer = containerObj.transform;

        Font arialFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        foreach (var target in labelTargets)
        {
            if (target.targetTransform == null) continue;

            // 1. Create Canvas GameObject
            GameObject canvasObj = new GameObject($"Label_{target.labelText}");
            canvasObj.transform.SetParent(_labelsContainer, false);
            canvasObj.transform.position = target.targetTransform.position + target.localOffset;

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
            canvasRt.sizeDelta = labelSize;
            canvasRt.localScale = new Vector3(0.005f, 0.005f, 0.005f); // Scale down World Space UI

            // 2. Add Background Image Panel
            GameObject panelObj = new GameObject("Background");
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image bgImage = panelObj.AddComponent<Image>();
            bgImage.color = backgroundColor;
            RectTransform panelRt = panelObj.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;

            // 3. Add Text Label
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(panelObj.transform, false);
            Text labelText = textObj.AddComponent<Text>();
            labelText.font = arialFont;
            labelText.text = target.labelText;
            labelText.fontSize = fontSize;
            labelText.fontStyle = FontStyle.Bold;
            labelText.color = textColor;
            labelText.alignment = TextAnchor.MiddleCenter;

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
        }
    }

    [ContextMenu("Clear Labels")]
    public void ClearLabels()
    {
        Transform existing = transform.Find(containerName);
        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform result = FindChildRecursive(child, childName);
            if (result != null) return result;
        }
        return null;
    }
}