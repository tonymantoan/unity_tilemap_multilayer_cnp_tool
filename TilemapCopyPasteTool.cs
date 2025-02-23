using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[EditorTool("Tilemap Multi Layer Copy Paste Tool", typeof(Tilemap))]
public class TilemapCopyPasteTool : EditorTool
{
    private Vector3Int selectionStart;
    private Vector3Int selectionEnd;
    private Vector3 selectionFrameStart;
    private Vector3 selectionFrameSize;
    private bool selecting;
    private bool pasting;
    private Dictionary<Tilemap, Dictionary<Vector3Int, (TileBase tile, Matrix4x4 transform)>> copiedTiles = new();

    public override void OnActivated()
    {
        SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Entering Tile Layer Copy Select Mode"), .2f);
        copiedTiles.Clear();
        pasting = false;
    }

    public override void OnToolGUI(EditorWindow window)
    {
        SceneView sceneView = window as SceneView;
        if (sceneView == null) return;

        Event e = Event.current;

        SetupToolMenu();
        DrawSelectionRect( e );

        if (e.type == EventType.MouseDown && e.button == 0 && !pasting)
        {
            // Begin a multilayer tile selection
            Vector3 worldPoint = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            selectionFrameStart = worldPoint;
            selectionStart = GridSelection(worldPoint);
            selecting = true;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && selecting)
        {
            // Complete the selection and copy all of the tiles
            Vector3 worldPoint = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            selectionEnd = GridSelection(worldPoint);
            selecting = false;
            selectionFrameSize = selectionFrameStart - worldPoint;
            e.Use();
            CopyTiles();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && pasting)
        {
            // In paste mode, paste the tiles when the user clicks a space
            Vector3 worldPoint = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            selectionEnd = GridSelection(worldPoint);
            selecting = false;
            e.Use();
            PasteTiles();
        }
    }

    private void SetupToolMenu()
    {
        Handles.BeginGUI();
        using (new GUILayout.HorizontalScope())
        {
            using (new GUILayout.VerticalScope(EditorStyles.toolbar))
            {
                pasting = EditorGUILayout.Toggle("Paste Tiles", pasting);
                // TODO: add snapToGrid option
            }

            GUILayout.FlexibleSpace();
        }
        Handles.EndGUI();
    }

    private void DrawSelectionRect( Event e )
    {
        if( selecting )
        {
            Vector3 size = selectionFrameStart - HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            Vector3 startPoint = selectionFrameStart;
            Vector3[] selectionRecVerts = new Vector3[]
            {
                startPoint,
                new Vector3(startPoint.x - size.x, startPoint.y, startPoint.z),
                new Vector3(startPoint.x - size.x, startPoint.y - size.y, startPoint.z),
                new Vector3(startPoint.x, startPoint.y - size.y)
            };
            Handles.DrawSolidRectangleWithOutline(selectionRecVerts, new Color(0.5f, 0.5f, 0.5f, 0.2f), new Color(0, 0, 0, 1));
        }

        if( pasting )
        {
            Vector3Int size = new Vector3Int( (int)Mathf.Abs( selectionFrameSize.x), (int)Mathf.Abs( selectionFrameSize.y), (int)Mathf.Abs( selectionFrameSize.z) );
            Vector3 startPoint =HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            // Handles.DrawSelectionFrame( 1, selectionFrameStart, Quaternion.identity, HandleUtility.GetHandleSize, EventType.Repaint);
            Vector3[] selectionRecVerts = new Vector3[]
            {
                startPoint,
                new Vector3(startPoint.x + size.x, startPoint.y, startPoint.z),
                new Vector3(startPoint.x + size.x, startPoint.y + size.y, startPoint.z),
                new Vector3(startPoint.x, startPoint.y + size.y)
            };
            Handles.DrawSolidRectangleWithOutline(selectionRecVerts, new Color(0.5f, 0.8f, 0.5f, 0.2f), new Color(0, 0, 0, 1));
        }
    }

    // Finds the Grid for a tilemap and converts the world point to a grid cell
    private Vector3Int GridSelection(Vector3 worldPoint)
    {
        Tilemap tilemap = Selection.activeGameObject?.GetComponent<Tilemap>();
        return tilemap ? tilemap.layoutGrid.WorldToCell(worldPoint) : Vector3Int.zero;
    }
    private void CopyTiles()
    {
        copiedTiles.Clear();
        int copiedTilesCount = 0;

        BoundsInt bounds = new BoundsInt(
            Vector3Int.Min( selectionStart, selectionEnd ),
            new Vector3Int(Mathf.Abs(selectionEnd.x - selectionStart.x), Mathf.Abs(selectionEnd.y - selectionStart.y), Mathf.Abs(selectionEnd.z - selectionStart.z)) + Vector3Int.one
        );

        foreach (GameObject obj in Selection.gameObjects)
        {
            Tilemap tilemap = obj.GetComponent<Tilemap>();
            if (tilemap)
            {
                Dictionary<Vector3Int, (TileBase, Matrix4x4)> tileData = new();
                foreach (Vector3Int pos in bounds.allPositionsWithin)
                {
                    TileBase tile = tilemap.GetTile(pos);
                    if (tile)
                    {
                        tileData[pos - bounds.position] = (tile, tilemap.GetTransformMatrix(pos));
                    }
                }
                copiedTilesCount = copiedTilesCount + tileData.Count;
                copiedTiles[tilemap] = tileData;
            }
        }

        SceneView.lastActiveSceneView.ShowNotification(new GUIContent( $"Copied {copiedTilesCount} Tiles" ), .2f);

    }

    private void PasteTiles()
    {
        if (copiedTiles.Count == 0) return;

        Vector3Int pastePosition = selectionEnd;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName( "Paste Tiles");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var tilemapEntry in copiedTiles)
        {
            Tilemap tilemap = tilemapEntry.Key;
            Undo.RegisterCompleteObjectUndo(tilemap, "Paste Tiles");
            foreach (var tileEntry in tilemapEntry.Value)
            {
                tilemap.SetTile(pastePosition + tileEntry.Key, tileEntry.Value.tile);
                tilemap.SetTransformMatrix(pastePosition + tileEntry.Key, tileEntry.Value.transform);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
    }
}
