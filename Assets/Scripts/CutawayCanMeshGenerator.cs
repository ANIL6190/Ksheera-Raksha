using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CutawayCanMeshGenerator : MonoBehaviour
{
    [Header("Cutaway Angles")]
    [Range(90, 360)]
    public float sectorAngle = 180f; // 180 = Half cutaway, 270 = Quarter cutaway

    [Header("Dimensions (Meters)")]
    public float height = 0.6f;
    public float rMilk = 0.178f;
    public float rLiner = 0.180f;
    public float rPcm = 0.210f;
    public float rIns = 0.230f;
    public float rShell = 0.235f;

    [Header("Mesh Quality")]
    [Range(12, 72)]
    public int radialSegments = 36;

    [Header("Material References")]
    public Material milkMaterial;
    public Material linerMaterial;
    public Material pcmMaterial;
    public Material insMaterial;
    public Material shellMaterial;

    private void Start()
    {
        GenerateCutawayModel();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) GenerateCutawayModel();
        };
#endif
    }

    [ContextMenu("Generate Cutaway Mesh")]
    public void GenerateCutawayModel()
    {
        // Enforce valid strictly increasing radii
        rMilk = Mathf.Max(0.05f, rMilk);
        if (rLiner <= rMilk) rLiner = rMilk + 0.005f; // 5mm minimum inner liner
        if (rPcm <= rLiner) rPcm = rLiner + 0.030f;   // 30mm minimum PCM
        if (rIns <= rPcm) rIns = rPcm + 0.020f;       // 20mm minimum insulation
        if (rShell <= rIns) rShell = rIns + 0.005f;   // 5mm minimum shell

        // Create 5 concentric layer children safely with 3D temperature readouts
        CreateOrUpdateLayerChild("1_MilkCore", 0f, rMilk, height, milkMaterial, CanVisualizer.CanType.Proposed);
        CreateOrUpdateLayerChild("2_InnerLiner", rMilk, rLiner, height, linerMaterial, CanVisualizer.CanType.InnerLiner);
        CreateOrUpdateLayerChild("3_PCMJacket", rLiner, rPcm, height, pcmMaterial, CanVisualizer.CanType.PCM);
        CreateOrUpdateLayerChild("4_Insulation", rPcm, rIns, height, insMaterial, CanVisualizer.CanType.Insulation);
        CreateOrUpdateLayerChild("5_OuterShell", rIns, rShell, height, shellMaterial, CanVisualizer.CanType.OuterShell);
    }

    private void CreateOrUpdateLayerChild(string layerName, float innerRadius, float outerRadius, float h, Material mat, CanVisualizer.CanType? visualizerType)
    {
        Transform childTransform = transform.Find(layerName);
        GameObject child;

        if (childTransform == null)
        {
            child = new GameObject(layerName);
            child.transform.SetParent(transform, false);
        }
        else
        {
            child = childTransform.gameObject;
        }

        MeshFilter mf = child.GetComponent<MeshFilter>();
        if (mf == null)
        {
            mf = child.AddComponent<MeshFilter>();
        }

        MeshRenderer mr = child.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            mr = child.AddComponent<MeshRenderer>();
        }

        Mesh mesh = GenerateSectorMesh(innerRadius, outerRadius, h, sectorAngle, radialSegments);
        mesh.name = layerName + "_Mesh";

        mf.sharedMesh = mesh;

        if (mat != null)
        {
            mr.sharedMaterial = mat;
        }
        else if (mr.sharedMaterial == null)
        {
            mr.sharedMaterial = CreateFallbackMaterial(layerName);
        }

        // Automatically attach CanVisualizer and 3D World Space Text Label to every layer
        if (visualizerType.HasValue)
        {
            CanVisualizer vis = child.GetComponent<CanVisualizer>();
            if (vis == null)
            {
                vis = child.AddComponent<CanVisualizer>();
            }
            vis.canType = visualizerType.Value;
            vis.canRenderer = mr;
            vis.SetDefaultPrefix();

            // Create or find 3D World Space Text Label object for this layer
            Transform labelTransform = child.transform.Find("3D_TempLabel");
            GameObject labelObj;
            if (labelTransform == null)
            {
                labelObj = new GameObject("3D_TempLabel");
                labelObj.transform.SetParent(child.transform, false);
            }
            else
            {
                labelObj = labelTransform.gameObject;
            }

            TextMesh tm = labelObj.GetComponent<TextMesh>();
            if (tm == null)
            {
                tm = labelObj.AddComponent<TextMesh>();
            }

            // Position label above mid-radius of the layer in 3D space
            float midR = innerRadius <= 0.001f ? outerRadius * 0.5f : (innerRadius + outerRadius) * 0.5f;
            float midAngleRad = (sectorAngle * 0.5f) * Mathf.Deg2Rad;
            float x = Mathf.Cos(midAngleRad) * midR;
            float z = Mathf.Sin(midAngleRad) * midR;

            // Stagger label heights for clear visibility above cutaway
            float heightOffset = 0.08f + ((int)visualizerType.Value * 0.05f);
            labelObj.transform.localPosition = new Vector3(x, h * 0.5f + heightOffset, z);
            labelObj.transform.localRotation = Quaternion.Euler(0, 180f, 0); // Face front camera
            labelObj.transform.localScale = Vector3.one * 0.03f; // High quality 3D text scale

            tm.characterSize = 0.05f;
            tm.fontSize = 60;
            tm.fontStyle = FontStyle.Bold;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;

            MeshRenderer textMr = labelObj.GetComponent<MeshRenderer>();
            if (textMr != null && tm.font != null && tm.font.material != null)
            {
                textMr.sharedMaterial = tm.font.material;
            }

            vis.world3DTempLabel = tm;
        }
    }

    private Mesh GenerateSectorMesh(float rIn, float rOut, float h, float angleDeg, int segments)
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        float angleRad = angleDeg * Mathf.Deg2Rad;
        float step = angleRad / segments;
        float halfH = h * 0.5f;

        bool isSolid = rIn <= 0.001f;

        // Outer & Inner Curved Surfaces
        for (int i = 0; i <= segments; i++)
        {
            float a = i * step;
            float cos = Mathf.Cos(a);
            float sin = Mathf.Sin(a);

            // Outer Top / Bottom
            vertices.Add(new Vector3(cos * rOut, halfH, sin * rOut));
            vertices.Add(new Vector3(cos * rOut, -halfH, sin * rOut));
            normals.Add(new Vector3(cos, 0, sin));
            normals.Add(new Vector3(cos, 0, sin));
            uvs.Add(new Vector2((float)i / segments, 1f));
            uvs.Add(new Vector2((float)i / segments, 0f));

            if (!isSolid)
            {
                // Inner Top / Bottom
                vertices.Add(new Vector3(cos * rIn, halfH, sin * rIn));
                vertices.Add(new Vector3(cos * rIn, -halfH, sin * rIn));
                normals.Add(new Vector3(-cos, 0, -sin));
                normals.Add(new Vector3(-cos, 0, -sin));
                uvs.Add(new Vector2((float)i / segments, 1f));
                uvs.Add(new Vector2((float)i / segments, 0f));
            }
        }

        // Triangles for Curved Outer Shell
        int stride = isSolid ? 2 : 4;
        for (int i = 0; i < segments; i++)
        {
            int idx = i * stride;

            // Outer Curved Quad
            triangles.Add(idx);
            triangles.Add(idx + 1);
            triangles.Add(idx + stride);

            triangles.Add(idx + 1);
            triangles.Add(idx + stride + 1);
            triangles.Add(idx + stride);

            if (!isSolid)
            {
                // Inner Curved Quad
                triangles.Add(idx + 2);
                triangles.Add(idx + stride + 2);
                triangles.Add(idx + 3);

                triangles.Add(idx + 3);
                triangles.Add(idx + stride + 2);
                triangles.Add(idx + stride + 3);
            }
        }

        // Add Side Caps (Cutaway Flat Planes)
        AddFlatSideCap(vertices, triangles, normals, uvs, rIn, rOut, halfH, 0f, true);
        AddFlatSideCap(vertices, triangles, normals, uvs, rIn, rOut, halfH, angleRad, false);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateBounds();

        return mesh;
    }

    private void AddFlatSideCap(List<Vector3> verts, List<int> tris, List<Vector3> norms, List<Vector2> uvs, float rIn, float rOut, float halfH, float angle, bool isStart)
    {
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);

        Vector3 norm = isStart ? new Vector3(sin, 0, -cos) : new Vector3(-sin, 0, cos);

        int baseIdx = verts.Count;

        verts.Add(new Vector3(cos * rIn, halfH, sin * rIn));
        verts.Add(new Vector3(cos * rOut, halfH, sin * rOut));
        verts.Add(new Vector3(cos * rOut, -halfH, sin * rOut));
        verts.Add(new Vector3(cos * rIn, -halfH, sin * rIn));

        for (int i = 0; i < 4; i++) norms.Add(norm);

        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0, 0));

        if (isStart)
        {
            tris.Add(baseIdx);
            tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 2);

            tris.Add(baseIdx);
            tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 3);
        }
        else
        {
            tris.Add(baseIdx);
            tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 1);

            tris.Add(baseIdx);
            tris.Add(baseIdx + 3);
            tris.Add(baseIdx + 2);
        }
    }

    private Material CreateFallbackMaterial(string name)
    {
        Material mat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default"));
        mat.name = name + "_Mat";

        if (name.Contains("Milk")) mat.color = new Color(0.1f, 0.45f, 1.0f);            // Blue Milk
        else if (name.Contains("InnerLiner")) mat.color = new Color(0.7f, 0.75f, 0.8f); // Steel Liner
        else if (name.Contains("PCM")) mat.color = new Color(0.0f, 0.85f, 0.85f);        // Cyan PCM
        else if (name.Contains("Insulation")) mat.color = new Color(0.95f, 0.8f, 0.1f);  // Yellow Insulation
        else mat.color = new Color(0.4f, 0.45f, 0.5f);                                   // Grey Metal Shell

        return mat;
    }
}
