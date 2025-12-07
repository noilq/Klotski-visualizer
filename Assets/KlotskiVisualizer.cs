using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using KlotskiDecisionTree;
using TMPro;
using System.Linq;
using UnityEngine.EventSystems;

public class DecisionTreeVisualizer : MonoBehaviour
{
    public DecisionTreeUIController ui;

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
    [SerializeField] private TMP_InputField rowsInput;
    [SerializeField] private TMP_InputField columnsInput;
    [SerializeField] private Toggle pinsToggle;
    [SerializeField] private TMP_InputField winningBlockIdInput;
    [SerializeField] private TMP_InputField winningXInput;
    [SerializeField] private TMP_InputField winningYInput;
    [SerializeField] private TMP_InputField exitWidthInput;

    //board preview panel
    [SerializeField] private RectTransform boardPreviewContainer;
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private GameObject blockPreviewPrefab;
    private readonly List<GameObject> cellObjects = new List<GameObject>();
    private readonly List<GameObject> blockObjects = new List<GameObject>();

    //graph setting config
    [SerializeField] private GameObject graphSettingPanel;
    [SerializeField] private Button graphSettingsButton;
    [SerializeField] private Slider repulsionForceScrollBar;
    [SerializeField] private Slider springForceScrollBar;
    [SerializeField] private Slider dampingScrollBar;
    [SerializeField] private Slider minDistanceScrollBar;
    [SerializeField] private Slider maxVelocityScrollBar;
    [SerializeField] private Slider velocityTresholdScrollBar;

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
    private GraphNode selectedNode;

    [SerializeField] private Button createBlockButton;
    [SerializeField] private Button deleteBlockButton;
    private bool createBlockMode = false;
    private bool deleteBlockMode = false;
    private Vector2Int? firstBlockPoint = null;

    void Start()
    {
        ui.Init(this);

        ui.LoadFromConfig();

        ui.OnGenerateClicked += OnGenerateGraph;
        ui.OnCreateBlockClicked += StartCreateBlockMode;
        ui.OnDeleteBlockClicked += StartDeleteBlockMode;

        SubscribeToGraphSettingsPanelEvents();

        Board initialBoard = CreateBoardFromConfig();
        VisualizeDecisionTree(initialBoard);
    }

    private void OnGenerateGraph()
    {
        if (!ValidateBoardConfig(out var errors))
        {
            ErrorPopup.Instance?.Show("Errors:\n" + string.Join("\n", errors));
            return;
        }

        ClearGraph();
        var board = CreateBoardFromConfig();
        VisualizeDecisionTree(board);
        isStabilized = false;
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

        if (!ValidateBoardConfig(out var errors))
        {
            string msg = "Board configuration errors:\n" + string.Join("\n", errors);

            if (ErrorPopup.Instance != null)
                ErrorPopup.Instance.Show(msg);

            Debug.LogError(msg);
            return;
        }

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
        HandleBlockEditingClicks();

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

    private void CreateGrid(RectTransform container, int rows, int cols, List<GameObject> cellList, GameObject cellPrefab)
    {
        // Clear existing
        foreach (var go in cellList) Destroy(go);
        cellList.Clear();


        float width = container.rect.width;
        float height = container.rect.height;


        // enforce square cells
        float cellSize = Mathf.Min(width / cols, height / rows);


        float totalGridWidth = cellSize * cols;
        float totalGridHeight = cellSize * rows;


        // center grid
        float offsetX = (width - totalGridWidth) * 0.5f;
        float offsetY = (height - totalGridHeight) * 0.5f;


        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                GameObject cellGO = Instantiate(cellPrefab, container);
                RectTransform rt = cellGO.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cellSize, cellSize);


                rt.anchoredPosition = new Vector2(
                offsetX + c * cellSize + cellSize * 0.5f,
                offsetY + (rows - 1 - r) * cellSize + cellSize * 0.5f
                );


                cellList.Add(cellGO);
            }
        }
    }

    public void UpdateBoardPreview()
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

        // единый квадратный размер клетки
        float cellSize = Mathf.Min(width / cols, height / rows);

        float gridWidth = cellSize * cols;
        float gridHeight = cellSize * rows;

        // центрирование
        float offsetX = (width - gridWidth) * 0.5f;
        float offsetY = (height - gridHeight) * 0.5f;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                GameObject cell = Instantiate(cellPrefab, boardPreviewContainer);
                var rect = cell.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;

                rect.sizeDelta = new Vector2(cellSize, cellSize);
                rect.anchoredPosition = new Vector2(
                    offsetX + x * cellSize,
                    offsetY + y * cellSize
                );

                var img = cell.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                    img.color = (x + y) % 2 == 0 ? 
                        new Color(0.51f, 0.46f, 0.48f) : 
                        new Color(0.38f, 0.34f, 0.36f);

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

            rect.sizeDelta = new Vector2(block.width * cellSize, block.height * cellSize);
            rect.anchoredPosition = new Vector2(offsetX + block.x * cellSize, offsetY + block.y * cellSize);

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
                    ui.ShowNodePreview(selectedNode);
                }
            }
        }
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

    private bool ValidateBoardConfig(out List<string> errors)
    {
        errors = new List<string>();

        int rows = boardConfig.rows;
        int cols = boardConfig.columns;

        foreach (var block in boardConfig.blocks)
        {
            if (block.width < 1 || block.height < 1)
                errors.Add($"Block {block.id} has invalid size ({block.width}x{block.height}).");

            if (block.x < 0 || block.y < 0)
                errors.Add($"Block {block.id} has negative position ({block.x}, {block.y}).");

            if (block.x + block.width > cols || block.y + block.height > rows)
                errors.Add($"Block {block.id} does not fit inside the board (pos {block.x},{block.y}, size {block.width}x{block.height}).");
        }

        for (int i = 0; i < boardConfig.blocks.Count; i++)
        {
            for (int j = i + 1; j < boardConfig.blocks.Count; j++)
            {
                var a = boardConfig.blocks[i];
                var b = boardConfig.blocks[j];

                bool overlap =
                    a.x < b.x + b.width &&
                    a.x + a.width > b.x &&
                    a.y < b.y + b.height &&
                    a.y + a.height > b.y;

                if (overlap)
                    errors.Add($"Blocks {a.id} and {b.id} overlap!");
            }
        }

        if (boardConfig.winningBlockId != -1)
        {
            var winning = boardConfig.blocks.FirstOrDefault(b => b.id == boardConfig.winningBlockId);
            if (winning == null)
            {
                errors.Add($"Winning block ID {boardConfig.winningBlockId} does not exist.");
            }
            else
            {
                if (boardConfig.winningX < 0 ||
                    boardConfig.winningY < 0 ||
                    boardConfig.winningX + boardConfig.exitWidth > cols)
                {
                    errors.Add("Winning exit position is outside the board.");
                }
            }
        }

        return errors.Count == 0;
    }



    private void StartCreateBlockMode()
    {
        createBlockMode = true;
        deleteBlockMode = false;
        firstBlockPoint = null;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // можно заменить
    }

    private void StartDeleteBlockMode()
    {
        deleteBlockMode = true;
        createBlockMode = false;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // можно заменить
    }

    private void HandleBlockEditingClicks()
    {
        if (!createBlockMode && !deleteBlockMode)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        Vector2 localPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                boardPreviewContainer,
                Input.mousePosition,
                uiCanvas.worldCamera,
                out localPos))
            return;

        float width = boardPreviewContainer.rect.width;
        float height = boardPreviewContainer.rect.height;

        int cols = boardConfig.columns;
        int rows = boardConfig.rows;

        // квадратная клетка
        float cellSize = Mathf.Min(width / cols, height / rows);

        float gridWidth = cellSize * cols;
        float gridHeight = cellSize * rows;

        // центрирование
        float offsetX = (width - gridWidth) * 0.5f;
        float offsetY = (height - gridHeight) * 0.5f;

        // localPos приходит в координатах (0,0) в центре → переводим в левый нижний угол
        float adjustedX = localPos.x + width * 0.5f - offsetX;
        float adjustedY = localPos.y + height * 0.5f - offsetY;

        // определяем клетку
        int x = Mathf.FloorToInt(adjustedX / cellSize);
        int y = Mathf.FloorToInt(adjustedY / cellSize);

        // вне сетки
        if (x < 0 || y < 0 || x >= cols || y >= rows)
            return;

        // ---------- УДАЛЕНИЕ ----------
        if (deleteBlockMode)
        {
            var block = boardConfig.blocks.FirstOrDefault(b =>
                x >= b.x && x < b.x + b.width &&
                y >= b.y && y < b.y + b.height);

            if (block != null)
            {
                boardConfig.blocks.Remove(block);
                UpdateBoardPreview();
            }

            deleteBlockMode = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        // ---------- СОЗДАНИЕ ----------
        if (createBlockMode)
        {
            if (firstBlockPoint == null)
            {
                firstBlockPoint = new Vector2Int(x, y);
                return;
            }

            Vector2Int p1 = firstBlockPoint.Value;
            Vector2Int p2 = new Vector2Int(x, y);

            int minX = Mathf.Min(p1.x, p2.x);
            int minY = Mathf.Min(p1.y, p2.y);
            int widthB = Mathf.Abs(p1.x - p2.x) + 1;
            int heightB = Mathf.Abs(p1.y - p2.y) + 1;

            foreach (var b in boardConfig.blocks)
            {
                bool overlap =
                    minX < b.x + b.width &&
                    minX + widthB > b.x &&
                    minY < b.y + b.height &&
                    minY + heightB > b.y;

                if (overlap)
                {
                    Debug.LogWarning("Block overlaps existing block!");
                    firstBlockPoint = null;
                    createBlockMode = false;
                    return;
                }
            }

            int newId = boardConfig.blocks.Count > 0 ?
                        boardConfig.blocks.Max(b => b.id) + 1 : 1;

            boardConfig.blocks.Add(new BlockConfig
            {
                id = newId,
                x = minX,
                y = minY,
                width = widthB,
                height = heightB
            });

            UpdateBoardPreview();

            firstBlockPoint = null;
            createBlockMode = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}