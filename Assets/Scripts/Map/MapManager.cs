using UnityEngine;
using PaperIO.Core;

namespace PaperIO.Map
{
    /// <summary>
    /// Builds and manages the visual map: ground plane, grid overlay, and
    /// soft boundary walls.
    ///
    /// One Unity world unit equals one grid cell.  The map occupies
    /// (0,0,0) → (gridSize, 0, gridSize) in world space.
    ///
    /// Attach to a "Map" GameObject in the scene.
    /// </summary>
    public class MapManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Ground")]
        [Tooltip("Material applied to the ground plane.")]
        public Material groundMaterial;

        [Tooltip("Colour of the grid lines drawn on the ground.")]
        public Color gridColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);

        [Tooltip("Number of subdivisions for the grid helper lines.")]
        public int gridSubdivisions = 40;

        [Header("Boundary walls")]
        [Tooltip("Material for the soft boundary wall meshes.")]
        public Material boundaryMaterial;

        [Tooltip("Height of the boundary wall meshes.")]
        public float wallHeight = 4f;

        [Tooltip("Thickness of the boundary wall meshes (visual only).")]
        public float wallThickness = 1f;

        // ── Runtime ────────────────────────────────────────────────────────────
        private int _gridSize;

        // Generated GameObjects.
        private GameObject _groundPlane;
        private GameObject _gridHelper;
        private GameObject _walls;

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>Called by GameManager.Start().</summary>
        public void Initialize(int gridSize)
        {
            _gridSize = gridSize;
            BuildGround();
            BuildGridHelper();
            BuildBoundaryWalls();
        }

        /// <summary>World-space centre of the map (XZ).</summary>
        public Vector3 MapCenter => new Vector3(_gridSize * 0.5f, 0f, _gridSize * 0.5f);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Ground plane

        private void BuildGround()
        {
            _groundPlane = new GameObject("Ground");
            _groundPlane.transform.SetParent(transform);

            MeshFilter   mf = _groundPlane.AddComponent<MeshFilter>();
            MeshRenderer mr = _groundPlane.AddComponent<MeshRenderer>();

            // Simple quad scaled to cover the entire grid.
            mf.mesh = CreateQuadMesh(_gridSize, _gridSize);
            mr.sharedMaterial = groundMaterial
                ?? CreateDefaultGroundMaterial();

            _groundPlane.transform.position = new Vector3(_gridSize * 0.5f, -0.01f, _gridSize * 0.5f);
            _groundPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private Material CreateDefaultGroundMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.10f, 0.10f, 0.12f);
            return mat;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Grid helper

        private void BuildGridHelper()
        {
            _gridHelper = new GameObject("GridHelper");
            _gridHelper.transform.SetParent(transform);

            // Use Unity's built-in LineRenderer grid approach:
            // Draw horizontal and vertical grid lines as child GameObjects.
            int   step  = _gridSize / gridSubdivisions;
            float gs    = _gridSize;
            float yPos  = 0.005f; // slightly above ground to avoid z-fighting.

            for (int i = 0; i <= gridSubdivisions; i++)
            {
                float t = i * step;
                CreateGridLine($"H_{i}", new Vector3(0, yPos, t), new Vector3(gs, yPos, t));
                CreateGridLine($"V_{i}", new Vector3(t, yPos, 0), new Vector3(t, yPos, gs));
            }
        }

        private void CreateGridLine(string lineName, Vector3 from, Vector3 to)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(_gridHelper.transform);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount  = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth     = 0.05f;
            lr.endWidth       = 0.05f;
            lr.material       = CreateLineMaterial();
            lr.startColor     = gridColor;
            lr.endColor       = gridColor;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.useWorldSpace  = true;
        }

        private Material CreateLineMaterial()
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = gridColor;
            return mat;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Boundary walls

        private void BuildBoundaryWalls()
        {
            _walls = new GameObject("BoundaryWalls");
            _walls.transform.SetParent(transform);

            float gs = _gridSize;
            float hw = wallHeight;
            float th = wallThickness;

            // Four walls: bottom, top, left, right.
            CreateWall("Wall_Bottom", new Vector3(gs * 0.5f, hw * 0.5f, -th * 0.5f), new Vector3(gs, hw, th));
            CreateWall("Wall_Top",    new Vector3(gs * 0.5f, hw * 0.5f, gs + th * 0.5f), new Vector3(gs, hw, th));
            CreateWall("Wall_Left",   new Vector3(-th * 0.5f, hw * 0.5f, gs * 0.5f), new Vector3(th, hw, gs));
            CreateWall("Wall_Right",  new Vector3(gs + th * 0.5f, hw * 0.5f, gs * 0.5f), new Vector3(th, hw, gs));
        }

        private void CreateWall(string wallName, Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = wallName;
            go.transform.SetParent(_walls.transform);
            go.transform.position    = position;
            go.transform.localScale  = size;

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = boundaryMaterial ?? CreateDefaultWallMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Remove collider (collision is handled by GameManager boundary logic).
            Destroy(go.GetComponent<BoxCollider>());
        }

        private Material CreateDefaultWallMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.20f, 0.20f, 0.25f, 0.6f);
            return mat;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Mesh utilities

        private static Mesh CreateQuadMesh(float width, float height)
        {
            var mesh = new Mesh { name = "GroundQuad" };
            float hw = width  * 0.5f;
            float hh = height * 0.5f;

            mesh.vertices = new Vector3[]
            {
                new(-hw, -hh, 0), new(hw, -hh, 0),
                new(hw,  hh,  0), new(-hw, hh, 0)
            };
            mesh.uv = new Vector2[]
            {
                new(0,0), new(1,0), new(1,1), new(0,1)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        #endregion
    }
}
