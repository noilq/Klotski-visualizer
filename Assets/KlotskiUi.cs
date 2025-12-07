using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using KlotskiDecisionTree;

public class DecisionTreeUIController : MonoBehaviour
{
    public DecisionTreeVisualizer visualizer;

    [Header("Board config controls")]
    public Button rowRemoveButton;
    public Button rowAddButton;
    public TMP_InputField rowsInput;
    public Slider rowsSlider;

    public Button columnsAddButton;
    public Button columnsRemoveButton;
    public TMP_InputField columnsInput;
    public Slider columnsSlider;

    public Toggle pinsToggle;
    public TMP_InputField winningBlockIdInput;
    public TMP_InputField winningXInput;
    public TMP_InputField winningYInput;
    public TMP_InputField exitWidthInput;

    public Button generateGraphButton;

    [Header("Block editor")]
    public Button createBlockButton;
    public Button deleteBlockButton;

    [Header("Menu")]
    public Button toggleMenuButton;
    public GameObject configPanel;

    [Header("Preview panel")]
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private RectTransform previewBoardContainer;
    [SerializeField] private GameObject previewCellPrefab;
    [SerializeField] private GameObject previewBlockPrefab;
    [SerializeField] private Button closePreviewButton;

    private readonly List<GameObject> previewCells = new List<GameObject>();
    private readonly List<GameObject> previewBlocks = new List<GameObject>();

    public event Action OnGenerateClicked;
    public event Action OnCreateBlockClicked;
    public event Action OnDeleteBlockClicked;

    private bool _ignoreUIUpdate = false;

    void Awake()
    {
        toggleMenuButton?.onClick.AddListener(() => 
        configPanel.SetActive(!configPanel.activeSelf));

        rowAddButton?.onClick.AddListener(() => ChangeRows(+1));
        rowRemoveButton?.onClick.AddListener(() => ChangeRows(-1));
        columnsAddButton?.onClick.AddListener(() => ChangeColumns(+1));
        columnsRemoveButton?.onClick.AddListener(() => ChangeColumns(-1));

        generateGraphButton?.onClick.AddListener(() => OnGenerateClicked?.Invoke());
        createBlockButton?.onClick.AddListener(() => OnCreateBlockClicked?.Invoke());
        deleteBlockButton?.onClick.AddListener(() => OnDeleteBlockClicked?.Invoke());

        rowsInput?.onEndEdit.AddListener(_ => UpdateVisualizerConfig());
        columnsInput?.onEndEdit.AddListener(_ => UpdateVisualizerConfig());
        pinsToggle?.onValueChanged.AddListener(_ => UpdateVisualizerConfig());
        winningBlockIdInput?.onEndEdit.AddListener(_ => UpdateVisualizerConfig());
        winningXInput?.onEndEdit.AddListener(_ => UpdateVisualizerConfig());
        winningYInput?.onEndEdit.AddListener(_ => UpdateVisualizerConfig());
        exitWidthInput?.onEndEdit.AddListener(_ => UpdateVisualizerConfig());

        rowsInput?.onEndEdit.AddListener(_ => OnRowsInputChanged());
        columnsInput?.onEndEdit.AddListener(_ => OnColumnsInputChanged());

        rowsSlider?.onValueChanged.AddListener(v => OnRowsSliderChanged(v));
        columnsSlider?.onValueChanged.AddListener(v => OnColumnsSliderChanged(v));

        pinsToggle?.onValueChanged.AddListener(_ => UpdateVisualizerConfig());
        winningBlockIdInput?.onEndEdit.AddListener(_ => UpdateVisualizerConfig());
        winningXInput?.onEndEdit.AddListener(_ => UpdateVisualizerConfig());
        winningYInput?.onEndEdit.AddListener(_ => UpdateVisualizerConfig());
        exitWidthInput?.onEndEdit.AddListener(_ => UpdateVisualizerConfig());

        closePreviewButton.onClick.AddListener(() => HidePreview());
    }

    public void Init(DecisionTreeVisualizer v)
    {
        visualizer = v;
        LoadFromConfig();
    }

    private void OnRowsInputChanged()
    {
        if (_ignoreUIUpdate) return;
        if (!int.TryParse(rowsInput.text, out int r)) return;

        r = Mathf.Clamp(r, (int)rowsSlider.minValue, (int)rowsSlider.maxValue);

        _ignoreUIUpdate = true;
        rowsSlider.value = r;
        _ignoreUIUpdate = false;

        UpdateVisualizerConfig();
    }

    private void OnColumnsInputChanged()
    {
        if (_ignoreUIUpdate) return;
        if (!int.TryParse(columnsInput.text, out int c)) return;

        c = Mathf.Clamp(c, (int)columnsSlider.minValue, (int)columnsSlider.maxValue);

        _ignoreUIUpdate = true;
        columnsSlider.value = c;
        _ignoreUIUpdate = false;

        UpdateVisualizerConfig();
    }

    private void OnRowsSliderChanged(float v)
    {
        if (_ignoreUIUpdate) return;

        _ignoreUIUpdate = true;
        rowsInput.text = ((int)v).ToString();
        _ignoreUIUpdate = false;

        UpdateVisualizerConfig();
    }

    private void OnColumnsSliderChanged(float v)
    {
        if (_ignoreUIUpdate) return;

        _ignoreUIUpdate = true;
        columnsInput.text = ((int)v).ToString();
        _ignoreUIUpdate = false;

        UpdateVisualizerConfig();
    }


    private void ChangeRows(int delta)
    {
        if (int.TryParse(rowsInput.text, out int r))
        {
            r = Mathf.Clamp(r + delta, (int)rowsSlider.minValue, (int)rowsSlider.maxValue);

            _ignoreUIUpdate = true;
            rowsInput.text = r.ToString();
            rowsSlider.value = r;
            _ignoreUIUpdate = false;

            UpdateVisualizerConfig();
        }
    }

    private void ChangeColumns(int delta)
    {
        if (int.TryParse(columnsInput.text, out int c))
        {
            c = Mathf.Clamp(c + delta, (int)columnsSlider.minValue, (int)columnsSlider.maxValue);

            _ignoreUIUpdate = true;
            columnsInput.text = c.ToString();
            columnsSlider.value = c;
            _ignoreUIUpdate = false;

            UpdateVisualizerConfig();
        }
    }

    // -----------------------------
    // UPDATE VISUALIZER
    // -----------------------------

    public void UpdateVisualizerConfig()
    {
        if (visualizer == null) return;

        var cfg = visualizer.boardConfig;

        int.TryParse(rowsInput.text, out cfg.rows);
        int.TryParse(columnsInput.text, out cfg.columns);
        int.TryParse(winningBlockIdInput.text, out cfg.winningBlockId);
        int.TryParse(winningXInput.text, out cfg.winningX);
        int.TryParse(winningYInput.text, out cfg.winningY);
        int.TryParse(exitWidthInput.text, out cfg.exitWidth);

        cfg.pinsEnabled = pinsToggle.isOn;

        visualizer.UpdateBoardPreview();
    }

    public void LoadFromConfig()
    {
        var cfg = visualizer.boardConfig;

        _ignoreUIUpdate = true;

        rowsInput.text = cfg.rows.ToString();
        columnsInput.text = cfg.columns.ToString();

        rowsSlider.value = cfg.rows;
        columnsSlider.value = cfg.columns;

        pinsToggle.isOn = cfg.pinsEnabled;
        winningBlockIdInput.text = cfg.winningBlockId.ToString();
        winningXInput.text = cfg.winningX.ToString();
        winningYInput.text = cfg.winningY.ToString();
        exitWidthInput.text = cfg.exitWidth.ToString();

        _ignoreUIUpdate = false;

        UpdateVisualizerConfig();
    }

    /*
    preview panel
    */

    public void ShowNodePreview(GraphNode node)
    {
        previewPanel.SetActive(true);
        DrawPreviewBoard(node.Board);
    }

    public void HidePreview()
    {
        previewPanel.SetActive(false);
    }

    public void DrawPreviewBoard(Board board)
    {
        // очистка
        foreach (var go in previewCells) Destroy(go);
        foreach (var go in previewBlocks) Destroy(go);
        previewCells.Clear();
        previewBlocks.Clear();

        int rows = board.Rows;
        int cols = board.Columns;

        float width = previewBoardContainer.rect.width;
        float height = previewBoardContainer.rect.height;
        float cellWidth = width / cols;
        float cellHeight = height / rows;

        Color tileLight = new Color(0.51f, 0.46f, 0.48f);
        Color tileDark  = new Color(0.38f, 0.34f, 0.36f);

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

        if (previewPanel != null && !previewPanel.activeSelf)
            previewPanel.SetActive(true);

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
                    img.color = ((x + y) % 2 == 0) ? tileLight : tileDark;

                previewCells.Add(cell);
            }
        }

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
                {
                    img.color = new Color(1f, 0.95f, 0.3f);
                }
                else
                {
                    int colorIndex = (block.Id >= 0) ? (block.Id % blockColors.Length) : 0;
                    img.color = blockColors[colorIndex];
                }
            }

            previewBlocks.Add(blockGO);
        }
    }
}