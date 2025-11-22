using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using KlotskiDecisionTree;
using TMPro;
using System.Linq;
using UnityEngine.EventSystems;

public class DecisionTreeVisualizer : MonoBehaviour
{
    //graph physics settings
    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float repulsionForce = 200f;   //forces pushing nodes away from each other
    [SerializeField] private float springForce = 50f;   //force pulling connected nodes together
    [SerializeField] private float damping = 0.95f;     //damping to stabilize simulation
    [SerializeField] private float minDistance = 4f;    //minimum distance between nodes
    [SerializeField] private float maxVelocity = 0.3f;  //maximum velocity cap
    [SerializeField] private float velocityThreshold = 0.1f;    //threshold for stopping simulation

    //ui elements
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Button toggleMenuButton;
    [SerializeField] private GameObject configPanel;
    [SerializeField] private Button rowAddButton;
    [SerializeField] private Button rowRemoveButton;
    [SerializeField] private Button columnsAddButton;
    [SerializeField] private Button columnsRemoveButton;
    [SerializeField] private TMP_InputField rowsInput;
    [SerializeField] private TMP_InputField columnsInput;
    [SerializeField] private Toggle pinsToggle;
    [SerializeField] private TMP_InputField winningBlockIdInput;
    [SerializeField] private TMP_InputField winningXInput;
    [SerializeField] private TMP_InputField winningYInput;
    [SerializeField] private TMP_InputField exitWidthInput;
    [SerializeField] private Button generateGraphButton;

    //board preview panel
    [SerializeField] private RectTransform boardPreviewContainer;
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private GameObject blockPreviewPrefab;
    private readonly List<GameObject> cellObjects = new List<GameObject>();
    private readonly List<GameObject> blockObjects = new List<GameObject>();

    //node preview panel
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private RectTransform previewBoardContainer;
    [SerializeField] private GameObject previewCellPrefab;
    [SerializeField] private GameObject previewBlockPrefab;
    [SerializeField] private Button closePreviewButton;

    private GraphNode selectedNode;
    private readonly List<GameObject> previewCells = new List<GameObject>();
    private readonly List<GameObject> previewBlocks = new List<GameObject>();

    //graph setting config
    [SerializeField] private GameObject graphSettingPanel;
    [SerializeField] private Button graphSettingsButton;
    [SerializeField] private Scrollbar repulsionForceScrollBar;
    [SerializeField] private Scrollbar springForceScrollBar;
    [SerializeField] private Scrollbar dampingScrollBar;
    [SerializeField] private Scrollbar minDistanceScrollBar;
    [SerializeField] private Scrollbar maxVelocityScrollBar;
    [SerializeField] private Scrollbar velocityTresholdScrollBar;

    //board config
    public BoardConfig boardConfig = new BoardConfig
    {
        rows = 4,
        columns = 4,
        pinsEnabled = true,
        winningBlockId = 1,
        winningX = 2,
        winningY = 3,
        exitWidth = 2,
        blocks = new List<BlockConfig>
        {
            new BlockConfig { id = 1, width = 2, height = 1, x = 0, y = 0 }
        }
    };

    [System.Serializable]
    public class BoardConfig
    {
        public int rows = 4;
        public int columns = 4;
        public bool pinsEnabled = true;
        public int winningBlockId = -1;
        public int winningX = 0;
        public int winningY = 0;
        public int exitWidth = 1;
        public List<BlockConfig> blocks = new List<BlockConfig>();
    }

    [System.Serializable]
    public class BlockConfig
    {
        public int id;
        public int width = 1;
        public int height = 1;
        public int x = 0;
        public int y = 0;
    }

    private Dictionary<GraphNode, GameObject> nodeObjects = new Dictionary<GraphNode, GameObject>();
    private Dictionary<GraphNode, Vector3> velocities = new Dictionary<GraphNode, Vector3>();
    private List<(GraphNode from, GraphNode to, LineRenderer line)> edges = new List<(GraphNode, GraphNode, LineRenderer)>();
    private bool isStabilized = false;

    void Awake()
    {
        //init ui
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
            if (uiCanvas == null)
            {
                GameObject canvasGO = new GameObject("UI Canvas");
                uiCanvas = canvasGO.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }
        }

        //setup button listeners
        if (toggleMenuButton != null)
            toggleMenuButton.onClick.AddListener(ToggleMenu);
        if (rowAddButton != null)
            rowAddButton.onClick.AddListener(IncreaseRows);
        if (rowRemoveButton != null)
            rowRemoveButton.onClick.AddListener(DecreaseRows);
        if (columnsAddButton != null)
            columnsAddButton.onClick.AddListener(IncreaseColumns);
        if (columnsRemoveButton != null)
            columnsRemoveButton.onClick.AddListener(DecreaseColumns);
        if (generateGraphButton != null)
            generateGraphButton.onClick.AddListener(GenerateGraphFromUI);
        if (closePreviewButton != null)
            closePreviewButton.onClick.AddListener(ClosePreviewPanel);

        LoadUIFromConfig();
        UpdateBoardPreview();
        SubscribeToGraphSettingsPanelEvents();
    }

    void Start()
    {
        Board initialBoard = CreateBoardFromConfig();
        VisualizeDecisionTree(initialBoard);
    }

    private void ToggleMenu()
    {
        if (configPanel != null)
            configPanel.SetActive(!configPanel.activeSelf);
    }

    private void IncreaseRows()
    {
        boardConfig.rows = Mathf.Min(boardConfig.rows + 1, 20); //cap at 20
        if (rowsInput != null)
            rowsInput.text = boardConfig.rows.ToString();
    }

    private void DecreaseRows()
    {
        boardConfig.rows = Mathf.Max(boardConfig.rows - 1, 1); //minimum 1
        if (rowsInput != null)
            rowsInput.text = boardConfig.rows.ToString();
    }

    private void IncreaseColumns()
    {
        boardConfig.columns = Mathf.Min(boardConfig.columns + 1, 20); //cap at 20
        if (columnsInput != null)
            columnsInput.text = boardConfig.columns.ToString();
    }

    private void DecreaseColumns()
    {
        boardConfig.columns = Mathf.Max(boardConfig.columns - 1, 1); //minimum 1
        if (columnsInput != null)
            columnsInput.text = boardConfig.columns.ToString();
    }

    private void LoadUIFromConfig()
    {
        if (rowsInput != null) rowsInput.text = boardConfig.rows.ToString();
        if (columnsInput != null) columnsInput.text = boardConfig.columns.ToString();
        if (pinsToggle != null) pinsToggle.isOn = boardConfig.pinsEnabled;
        if (winningBlockIdInput != null) winningBlockIdInput.text = boardConfig.winningBlockId.ToString();
        if (winningXInput != null) winningXInput.text = boardConfig.winningX.ToString();
        if (winningYInput != null) winningYInput.text = boardConfig.winningY.ToString();
        if (exitWidthInput != null) exitWidthInput.text = boardConfig.exitWidth.ToString();
    }

    private void UpdateConfigFromUI()
    {
        try
        {
            if (rowsInput != null)
                boardConfig.rows = int.Parse(rowsInput.text);
            if (columnsInput != null)
                boardConfig.columns = int.Parse(columnsInput.text);
            if (pinsToggle != null)
                boardConfig.pinsEnabled = pinsToggle.isOn;
            if (winningBlockIdInput != null)
                boardConfig.winningBlockId = int.Parse(winningBlockIdInput.text);
            if (winningXInput != null)
                boardConfig.winningX = int.Parse(winningXInput.text);
            if (winningYInput != null)
                boardConfig.winningY = int.Parse(winningYInput.text);
            if (exitWidthInput != null)
                boardConfig.exitWidth = int.Parse(exitWidthInput.text);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"invalid input in ui, {e.Message}");
        }
    }

    private void GenerateGraphFromUI()
    {
        UpdateConfigFromUI();
        UpdateBoardPreview();
        ClearGraph();
        Board initialBoard = CreateBoardFromConfig();
        VisualizeDecisionTree(initialBoard);
        isStabilized = false; //reset stabilization for new graph
    }

    private void ClearGraph()
    {
        foreach (var nodeObj in nodeObjects.Values)
        {
            if (nodeObj != null) Destroy(nodeObj);
        }
        foreach (var (_, _, line) in edges)
        {
            if (line != null && line.gameObject != null)
                Destroy(line.gameObject);
        }
        nodeObjects.Clear();
        velocities.Clear();
        edges.Clear();
    }

    private Board CreateBoardFromConfig()
    {
        Board board = new Board(boardConfig.rows, boardConfig.columns, boardConfig.pinsEnabled)
        {
            WinningBlockId = boardConfig.winningBlockId,
            WinningX = boardConfig.winningX,
            WinningY = boardConfig.winningY,
            ExitWidth = boardConfig.exitWidth
        };

        foreach (var blockConfig in boardConfig.blocks)
        {
            board.AddBlock(new Block(blockConfig.id, blockConfig.width, blockConfig.height, blockConfig.x, blockConfig.y));
        }

        return board;
    }

    public void VisualizeDecisionTree(Board initialBoard)
    {
        DecisionGraphBuilder builder = new DecisionGraphBuilder();
        GraphNode root = builder.BuildGraph(initialBoard);
        InitializeNodes(root);
        CreateEdges(root);
    }

    private void InitializeNodes(GraphNode root)
    {
        var visited = new HashSet<GraphNode>();
        var queue = new Queue<GraphNode>();
        queue.Enqueue(root);

        //random initial positions in a 3D space
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (visited.Contains(node)) continue;
            visited.Add(node);

            GameObject nodeObj = Instantiate(nodePrefab, Random.insideUnitSphere * 10f, Quaternion.identity);
            nodeObj.name = $"Node_{node.StateHash.Substring(0, 8)}";
            nodeObjects[node] = nodeObj;
            velocities[node] = Vector3.zero;

            foreach (var (child, _) in node.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    private void CreateEdges(GraphNode root)
    {
        var visited = new HashSet<GraphNode>();
        var queue = new Queue<GraphNode>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (visited.Contains(node)) continue;
            visited.Add(node);

            foreach (var (child, moveDesc) in node.Children)
            {
                if (!nodeObjects.ContainsKey(node) || !nodeObjects.ContainsKey(child)) continue;

                GameObject edgeObj = new GameObject($"Edge_{node.StateHash.Substring(0, 8)}_to_{child.StateHash.Substring(0, 8)}");
                LineRenderer line = edgeObj.AddComponent<LineRenderer>();
                line.material = lineMaterial;
                line.startWidth = 0.05f;
                line.endWidth = 0.05f;
                line.positionCount = 2;
                line.startColor = Color.white;
                line.endColor = Color.white;
                edges.Add((node, child, line));

                queue.Enqueue(child);
            }
        }
    }

    private void UpdateEdges()
    {
        foreach (var (from, to, line) in edges)
        {
            line.SetPosition(0, nodeObjects[from].transform.position);
            line.SetPosition(1, nodeObjects[to].transform.position);
        }
    }

    private void OnValidate()
    {
        //restart simulation if properties change in Inspector
        isStabilized = false;
    }

    private void Update()
    {   
        HandleNodeClick();

        if (isStabilized)
        {
            UpdateEdges();
            return;
        }

        //calculate forces
        var forces = new Dictionary<GraphNode, Vector3>();
        foreach (var node in nodeObjects.Keys)
        {
            forces[node] = Vector3.zero;
        }

        //repulsion between all pairs of nodes
        var nodes = new List<GraphNode>(nodeObjects.Keys);
        for (int j = 0; j < nodes.Count; j++)
        {
            for (int k = j + 1; k < nodes.Count; k++)
            {
                var node1 = nodes[j];
                var node2 = nodes[k];
                Vector3 pos1 = nodeObjects[node1].transform.position;
                Vector3 pos2 = nodeObjects[node2].transform.position;
                Vector3 delta = pos1 - pos2;
                float distance = delta.magnitude;
                if (distance < 0.01f) distance = 0.01f; //avoid division by zero
                if (distance < minDistance)
                {
                    float forceMagnitude = repulsionForce * (1f / distance);
                    Vector3 force = delta.normalized * forceMagnitude;
                    forces[node1] += force;
                    forces[node2] -= force;
                }
            }
        }

        //attraction along edges
        foreach (var (from, to, _) in edges)
        {
            Vector3 pos1 = nodeObjects[from].transform.position;
            Vector3 pos2 = nodeObjects[to].transform.position;
            Vector3 delta = pos2 - pos1;
            float distance = delta.magnitude;
            if (distance > 0.01f)
            {
                Vector3 force = delta * springForce;
                forces[from] += force;
                forces[to] -= force;
            }
        }

        //update positions
        foreach (var node in nodeObjects.Keys)
        {
            Vector3 force = forces[node];
            velocities[node] += force * Time.deltaTime;
            velocities[node] *= damping;
            velocities[node] = Vector3.ClampMagnitude(velocities[node], maxVelocity);
            nodeObjects[node].transform.position += velocities[node] * Time.deltaTime;
        }

        //check for stabilization
        float totalVelocity = 0f;
        foreach (var vel in velocities.Values)
        {
            totalVelocity += vel.magnitude;
        }
        if (totalVelocity < velocityThreshold * nodeObjects.Count)
        {
            isStabilized = true;
        }

        UpdateEdges();
    }

    private void OnDestroy()
    {
        ClearGraph();
    }

    private void UpdateBoardPreview()
    {
        if (boardPreviewContainer == null) return;

        //clear old elements
        foreach (var go in cellObjects) Destroy(go);
        foreach (var go in blockObjects) Destroy(go);
        cellObjects.Clear();
        blockObjects.Clear();

        int rows = boardConfig.rows;
        int cols = boardConfig.columns;

        if (rows <= 0 || cols <= 0) return;

        float width = boardPreviewContainer.rect.width;
        float height = boardPreviewContainer.rect.height;
        float cellWidth = width / cols;
        float cellHeight = height / rows;

        //create cell grid
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                GameObject cell = Instantiate(cellPrefab, boardPreviewContainer);
                var rect = cell.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(cellWidth, cellHeight);
                rect.anchoredPosition = new Vector2(x * cellWidth, y * cellHeight);

                //alternate colors
                var img = cell.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                    img.color = (x + y) % 2 == 0 ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.75f, 0.75f, 0.75f);

                cellObjects.Add(cell);
            }
        }

        Color[] blockColors =
        {
            new Color(0.9f, 0.3f, 0.3f),    //red
            new Color(0.3f, 0.6f, 0.9f),    //blue
            new Color(0.4f, 0.9f, 0.4f),    //green
            new Color(0.9f, 0.8f, 0.4f),    //yellow
            new Color(0.8f, 0.4f, 0.9f),    //violet
            new Color(0.9f, 0.6f, 0.3f),    //orange
            new Color(0.4f, 0.8f, 0.8f),    //light blue
            new Color(0.7f, 0.7f, 0.7f)     //grey
        };

        //place blocks
        foreach (var block in boardConfig.blocks)
        {
            GameObject blockGO = Instantiate(blockPreviewPrefab, boardPreviewContainer);
            var rect = blockGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;

            rect.sizeDelta = new Vector2(block.width * cellWidth, block.height * cellHeight);
            rect.anchoredPosition = new Vector2(block.x * cellWidth, block.y * cellHeight);

            var img = blockGO.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                //img.color = block.id == boardConfig.winningBlockId ? new Color(1f, 0.8f, 0.2f) : new Color(0.6f, 0.2f, 0.2f);
                if (block.id == boardConfig.winningBlockId)
                {
                    img.color = new Color(1f, 0.95f, 0.3f); //yellow for winning block
                }
                else
                {
                    //color by index
                    int colorIndex = block.id % blockColors.Length;
                    img.color = blockColors[colorIndex];
                }
            }

            blockObjects.Add(blockGO);
        }
    }

    private void HandleNodeClick()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {   
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var clickedObj = hit.collider.gameObject;
                var nodeEntry = nodeObjects.FirstOrDefault(kvp => kvp.Value == clickedObj);
                if (nodeEntry.Key != null)
                {
                    selectedNode = nodeEntry.Key;
                    ShowPreviewPanel(selectedNode);
                }
            }
        }
    }

    private void ShowPreviewPanel(GraphNode node)
    {
        if (previewPanel == null || previewBoardContainer == null)
            return;

        previewPanel.SetActive(true);

        //clear old blocks
        foreach (var go in previewCells) Destroy(go);
        foreach (var go in previewBlocks) Destroy(go);
        previewCells.Clear();
        previewBlocks.Clear();

        var board = node.Board;
        int rows = board.Rows;
        int cols = board.Columns;

        float width = previewBoardContainer.rect.width;
        float height = previewBoardContainer.rect.height;
        float cellWidth = width / cols;
        float cellHeight = height / rows;

        //create cell grid
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                GameObject cell = Instantiate(previewCellPrefab, previewBoardContainer);
                var rect = cell.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(cellWidth, cellHeight);
                rect.anchoredPosition = new Vector2(x * cellWidth, y * cellHeight);

                var img = cell.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                    img.color = (x + y) % 2 == 0 ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.8f, 0.8f, 0.8f);

                previewCells.Add(cell);
            }
        }

        Color[] blockColors =
        {
            new Color(0.9f, 0.3f, 0.3f),
            new Color(0.3f, 0.6f, 0.9f),
            new Color(0.4f, 0.9f, 0.4f),
            new Color(0.9f, 0.8f, 0.4f),
            new Color(0.8f, 0.4f, 0.9f),
            new Color(0.9f, 0.6f, 0.3f),
            new Color(0.4f, 0.8f, 0.8f),
            new Color(0.7f, 0.7f, 0.7f)
        };

        foreach (var block in board.Blocks)
        {
            GameObject blockGO = Instantiate(previewBlockPrefab, previewBoardContainer);
            var rect = blockGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(block.Width * cellWidth, block.Height * cellHeight);
            rect.anchoredPosition = new Vector2(block.X * cellWidth, block.Y * cellHeight);

            var img = blockGO.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                if (block.Id == board.WinningBlockId)
                    img.color = new Color(1f, 0.95f, 0.3f);
                else
                    img.color = blockColors[block.Id % blockColors.Length];
            }

            previewBlocks.Add(blockGO);
        }
    }

    private void ClosePreviewPanel()
    {
        if (previewPanel != null)
            previewPanel.SetActive(false);
    }  

    //graph settings panel
    private void ToggleGraphSettingsPanel()
    {
        if (graphSettingPanel == null)
            return;

        bool newState = !graphSettingPanel.activeSelf;
        graphSettingPanel.SetActive(newState);

        if (newState)
        {
            repulsionForceScrollBar.value = Mathf.InverseLerp(0f, 500f, repulsionForce);
            springForceScrollBar.value = Mathf.InverseLerp(0f, 200f, springForce);
            dampingScrollBar.value = Mathf.InverseLerp(0.1f, 1f, damping);
            minDistanceScrollBar.value = Mathf.InverseLerp(1f, 10f, minDistance);
            maxVelocityScrollBar.value = Mathf.InverseLerp(0.1f, 10f, maxVelocity);
            velocityTresholdScrollBar.value = Mathf.InverseLerp(0.01f, 1f, velocityThreshold);
        }
    }

    private void SubscribeToGraphSettingsPanelEvents()
    {   
        if (graphSettingsButton != null)
            graphSettingsButton.onClick.AddListener(ToggleGraphSettingsPanel);
            
        if (repulsionForceScrollBar != null)
            repulsionForceScrollBar.onValueChanged.AddListener(OnRepulsionForceChanged);

        if (springForceScrollBar != null)
            springForceScrollBar.onValueChanged.AddListener(OnSpringForceChanged);

        if (dampingScrollBar != null)
            dampingScrollBar.onValueChanged.AddListener(OnDampingChanged);

        if (minDistanceScrollBar != null)
            minDistanceScrollBar.onValueChanged.AddListener(OnMinDistanceChanged);

        if (maxVelocityScrollBar != null)
            maxVelocityScrollBar.onValueChanged.AddListener(OnMaxVelocityChanged);

        if (velocityTresholdScrollBar != null)
            velocityTresholdScrollBar.onValueChanged.AddListener(OnVelocityThresholdChanged);
    }

    private float ReMap(float value, float minIn, float maxIn, float minOut, float maxOut)
    {
        return minOut + (value - minIn) * (maxOut - minOut) / (maxIn - minIn);
    }

    private void OnRepulsionForceChanged(float value)
    {
        repulsionForce = ReMap(value, 0f, 1f, 0f, 500f);
        isStabilized = false;
    }

    private void OnSpringForceChanged(float value)
    {
        springForce = ReMap(value, 0f, 1f, 0f, 200f);
        isStabilized = false;
    }

    private void OnDampingChanged(float value)
    {
        damping = ReMap(value, 0f, 1f, 0.1f, 1f);
        isStabilized = false;
    }

    private void OnMinDistanceChanged(float value)
    {
        minDistance = ReMap(value, 0f, 1f, 1f, 10f);
        isStabilized = false;
    }

    private void OnMaxVelocityChanged(float value)
    {
        maxVelocity = ReMap(value, 0f, 1f, 0.1f, 10f);
        isStabilized = false;
    }

    private void OnVelocityThresholdChanged(float value)
    {
        velocityThreshold = ReMap(value, 0f, 1f, 0.01f, 1f);
        isStabilized = false;
    }
}