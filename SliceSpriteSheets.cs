using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class SliceSpriteSheets : EditorWindow
{
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
    private int cellsPerRow = 4;
    private int cellsPerColumn = 4;
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
    private SpriteAlignment SelectedPreset = SpriteAlignment.Center;

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
        GUILayout.Label("Slicing Options", EditorStyles.boldLabel);

        sliceMode = (SliceMode)EditorGUILayout.EnumPopup("Slice Mode", sliceMode);

        if (sliceMode == SliceMode.CellCount)
        {
            cellsPerRow = EditorGUILayout.IntField("Cells Per Row", cellsPerRow);
            cellsPerColumn = EditorGUILayout.IntField("Cells Per Column", cellsPerColumn);
        }
        else if (sliceMode == SliceMode.CellSize)
        {
            cellWidth = EditorGUILayout.IntField("Cell Width", cellWidth);
            cellHeight = EditorGUILayout.IntField("Cell Height", cellHeight);
        }

        GUILayout.Space(10);

        GUILayout.Label("Pivot Options", EditorStyles.boldLabel);

        // selectedPivotPreset = EditorGUILayout.Popup("Pivot Preset", selectedPivotPreset, pivotPresets);
        SelectedPreset = (SpriteAlignment)EditorGUILayout.EnumPopup("Pivot Preset", SelectedPreset);

        pivotPosition = GetPivotPosition(SelectedPreset);

        pivotUnitMode = (PivotUnitMode)EditorGUILayout.EnumPopup("Pivot Unit Mode", pivotUnitMode);

        GUILayout.Space(10);

        autoRefresh = EditorGUILayout.Toggle("Auto Refresh", autoRefresh);
        if (autoRefresh)
        {
            autoRefreshInterval = EditorGUILayout.FloatField("Refresh Interval (seconds)", autoRefreshInterval);
            autoRefreshInterval = Mathf.Max(0.1f, autoRefreshInterval);
        }

        if (GUILayout.Button("Slice Selected Sprite Sheets"))
        {
            SliceSelectedSpriteSheets();
        }

        GUILayout.Space(10);

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
    
    private Vector2 GetPivotPosition(SpriteAlignment pivot)
    {
        if(pivot == SpriteAlignment.Custom)
            return EditorGUILayout.Vector2Field("Custom Pivot", pivotPosition);
        //bottom -> 0, center -> 0.5, top -> 1
        float height = 1.0f - (int)pivot / 3 * 0.5f;
        //left -> 0, center -> 0.5, right -> 1
        float width = (int)pivot % 3 * 0.5f;
        return new Vector2(width,height);
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

            if (Directory.Exists(assetPath))
            {
                // If the selected asset is a folder, get all sprite sheets inside it
                string[] spriteSheetPaths = Directory.GetFiles(assetPath, "*.png", SearchOption.AllDirectories);
                foreach (string path in spriteSheetPaths)
                {
                    Texture2D spriteSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (spriteSheet != null && IsValidSpriteSheet(spriteSheet))
                    {
                        selectedSpriteSheets.Add(spriteSheet);
                    }
                }
            }
            else
            {
                // If the selected asset is a sprite sheet, add it to the list
                Texture2D spriteSheet = obj as Texture2D;
                if (spriteSheet != null && IsValidSpriteSheet(spriteSheet))
                {
                    selectedSpriteSheets.Add(spriteSheet);
                }
            }
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
        foreach (Texture2D spriteSheet in selectedSpriteSheets)
        {
            string assetPath = AssetDatabase.GetAssetPath(spriteSheet);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer != null)
            {
                int spriteWidth = spriteSheet.width;
                int spriteHeight = spriteSheet.height;

                int spritesPerRow, spritesPerColumn;
                int spritePixelsPerUnitX, spritePixelsPerUnitY;

                if (sliceMode == SliceMode.CellCount)
                {
                    spritesPerRow = cellsPerRow;
                    spritesPerColumn = cellsPerColumn;
                    spritePixelsPerUnitX = spriteWidth / spritesPerRow;
                    spritePixelsPerUnitY = spriteHeight / spritesPerColumn;
                }
                else
                {
                    spritesPerRow = spriteWidth / cellWidth;
                    spritesPerColumn = spriteHeight / cellHeight;
                    spritePixelsPerUnitX = cellWidth;
                    spritePixelsPerUnitY = cellHeight;
                }

                List<SpriteMetaData> spriteData = new List<SpriteMetaData>();

                for (int i = 0; i < spritesPerColumn; i++)
                {
                    for (int j = 0; j < spritesPerRow; j++)
                    {
                        SpriteMetaData smd = new SpriteMetaData();
                        smd.rect = new Rect(j * spritePixelsPerUnitX, i * spritePixelsPerUnitY, spritePixelsPerUnitX, spritePixelsPerUnitY);
                        smd.name = string.Format("{0}_{1}", Path.GetFileNameWithoutExtension(assetPath), (i * spritesPerRow) + j);
                        
                        smd.alignment = (int)SelectedPreset;
                        if (SelectedPreset == SpriteAlignment.Custom)
                            smd.pivot = pivotPosition; 

                        smd.border = new Vector4(0, 0, 0, 0);

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

        AssetDatabase.Refresh();
        RefreshSelectedSpriteSheets();
    }
}