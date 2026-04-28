using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class SliceSpriteSheets : EditorWindow
{
    private const float SPACER = 10.0f;
    public enum SliceMode
    {
        CellCount,
        CellSize
    }

    public enum PivotUnitMode
    {
        Normalized,
        Pixels
    }

    private SliceMode sliceMode = SliceMode.CellCount;
    private int RowCount = 4;
    private int ColCount = 4;
    private int cellWidth = 32;
    private int cellHeight = 32;

    private Vector2 pivotPosition = new Vector2(0.5f, 0.5f);
    private PivotUnitMode pivotUnitMode = PivotUnitMode.Normalized;

    private bool autoRefresh = false;
    private float autoRefreshInterval = 0.1f;
    private double lastRefreshTime = 0f;

    private Vector2 scrollPosition;
    private List<Texture2D> selectedSpriteSheets = new List<Texture2D>();

    //Allignments defined by unity
    private SpriteAlignment SelectedPivot = SpriteAlignment.Center;

    [MenuItem("Tools/Slice Sprite Sheets")]
    public static void ShowWindow()
    {
        GetWindow<SliceSpriteSheets>("Slice Sprite Sheets");
    }

    private void OnEnable()
    {
        Selection.selectionChanged += Repaint;
        RefreshSelectedSpriteSheets();
        EditorApplication.update += UpdateAutoRefresh;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= Repaint;
        EditorApplication.update -= UpdateAutoRefresh;
    }

    private void OnGUI()
    {
        DrawSliceMode();

        GUILayout.Space(SPACER);

        DrawPivot();

        GUILayout.Space(SPACER);

        DrawAutoRefresh();

        if (GUILayout.Button("Slice Selected Sprite Sheets"))
            SliceSelectedSpriteSheets();

        GUILayout.Space(SPACER);

        DrawSpriteSheets();
    }

    private void DrawSliceMode()
    {
        GUILayout.Label("Slicing Options", EditorStyles.boldLabel);

        sliceMode = (SliceMode)EditorGUILayout.EnumPopup("Slice Mode", sliceMode);

        if (sliceMode == SliceMode.CellCount){
            RowCount = EditorGUILayout.IntField("Cells Per Row", RowCount);
            ColCount = EditorGUILayout.IntField("Cells Per Column", ColCount);
            return;
        }
        cellWidth = EditorGUILayout.IntField("Cell Width", cellWidth);
        cellHeight = EditorGUILayout.IntField("Cell Height", cellHeight);
    }

    private void DrawAutoRefresh()
    {
        autoRefresh = EditorGUILayout.Toggle("Auto Refresh", autoRefresh);
        if(!autoRefresh)
            return;
        autoRefreshInterval = EditorGUILayout.FloatField("Refresh Interval (seconds)", autoRefreshInterval);
        autoRefreshInterval = Mathf.Max(0.1f, autoRefreshInterval);
    }

    private void DrawPivot()
    {
        GUILayout.Label("Pivot Options", EditorStyles.boldLabel);
        SelectedPivot = (SpriteAlignment)EditorGUILayout.EnumPopup("Pivot Preset", SelectedPivot);

        if(SelectedPivot == SpriteAlignment.Custom)
        {
            pivotUnitMode = (PivotUnitMode)EditorGUILayout.EnumPopup("Pivot Unit Mode", pivotUnitMode);
            pivotPosition = EditorGUILayout.Vector2Field("Custom Pivot", pivotPosition);
            return;
        }

        //bottom -> 0, center -> 0.5, top -> 1
        float height = 1.0f - (int)SelectedPivot / 3 * 0.5f;
        //left -> 0, center -> 0.5, right -> 1
        float width = (int)SelectedPivot % 3 * 0.5f;
        pivotPosition = new(width,height);
    }
    private void DrawSpriteSheets()
    {
        GUILayout.Label("Selected Sprite Sheets", EditorStyles.boldLabel);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        foreach (Texture2D spriteSheet in selectedSpriteSheets)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(spriteSheet.name, GUILayout.Width(200));
            GUILayout.Label(string.Format("{0}x{1}", spriteSheet.width, spriteSheet.height));
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }
    private void UpdateAutoRefresh()
    {
        if (autoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime >= autoRefreshInterval)
        {
            lastRefreshTime = EditorApplication.timeSinceStartup;
            RefreshSelectedSpriteSheets();
            Repaint();
        }
    }
    private void RefreshSelectedSpriteSheets()
    {
        selectedSpriteSheets.Clear();

        Object[] selectedAssets = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
        foreach (Object obj in selectedAssets)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            Texture2D spriteSheet;
            // If the selected asset is a folder, get all sprite sheets inside it
            if (Directory.Exists(assetPath))
            {
                string[] spriteSheetPaths = Directory.GetFiles(assetPath, "*.png", SearchOption.AllDirectories);
                foreach (string path in spriteSheetPaths)
                {
                    spriteSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (spriteSheet != null && IsValidSpriteSheet(spriteSheet))
                        selectedSpriteSheets.Add(spriteSheet);
                }
                continue;
            }
            // If the selected asset is a sprite sheet, add it to the list
            spriteSheet = obj as Texture2D;
            if (spriteSheet != null && IsValidSpriteSheet(spriteSheet))
                selectedSpriteSheets.Add(spriteSheet);
        }
    }
    private bool IsValidSpriteSheet(Texture2D spriteSheet)
    {
        string assetPath = AssetDatabase.GetAssetPath(spriteSheet);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        return importer != null && importer.textureType == TextureImporterType.Sprite;
    }
    private void SliceSelectedSpriteSheets()
    {
        foreach(Texture2D spritesheet in selectedSpriteSheets)
            SliceSpriteSheet(spritesheet);
        AssetDatabase.Refresh();
        RefreshSelectedSpriteSheets();
    }
    private void SliceSpriteSheet(Texture2D spriteSheet)
    {
        string assetPath = AssetDatabase.GetAssetPath(spriteSheet);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if(importer == null)
            return;

        int spritesPerRow, spritesPerColumn;
        int SpriteUnitHeight, SpriteUnitWidth;

        if (sliceMode == SliceMode.CellCount)
        {
            spritesPerRow = RowCount;
            spritesPerColumn = ColCount;
            SpriteUnitHeight = spriteSheet.width / spritesPerRow;
            SpriteUnitWidth = spriteSheet.height / spritesPerColumn;
        }
        else
        {
            spritesPerRow = spriteSheet.width / cellWidth;
            spritesPerColumn = spriteSheet.height / cellHeight;
            SpriteUnitHeight = cellWidth;
            SpriteUnitWidth = cellHeight;
        }

        List<SpriteMetaData> spriteData = new();

        for (int i = 0; i < spritesPerColumn; i++)
        {
            for (int j = 0; j < spritesPerRow; j++)
            {
                SpriteMetaData smd = new()
                {
                    rect = new Rect(j * SpriteUnitHeight, i * SpriteUnitWidth, SpriteUnitHeight, SpriteUnitWidth),
                    name = string.Format("{0}_{1}", Path.GetFileNameWithoutExtension(assetPath), (i * spritesPerRow) + j),
                    alignment = (int)SelectedPivot
                };
                if (SelectedPivot == SpriteAlignment.Custom)
                    smd.pivot = pivotPosition; 

                smd.border = new(0, 0, 0, 0);

                spriteData.Add(smd);
            }
        }

        importer.spritesheet = spriteData.ToArray();
        importer.spriteImportMode = SpriteImportMode.Multiple;

        float originalPixelsPerUnit = importer.spritePixelsPerUnit;
        importer.spritePixelsPerUnit = 1;

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        importer.spritePixelsPerUnit = originalPixelsPerUnit;
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }
}


