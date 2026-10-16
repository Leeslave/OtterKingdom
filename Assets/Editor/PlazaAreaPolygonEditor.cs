using UnityEditor;
using UnityEngine;

// Scene-view editing for PlazaAreaPolygon:
//   drag a dot            -> move vertex
//   click an edge circle  -> insert a vertex there
//   Ctrl/Cmd + click dot  -> delete vertex (keeps at least 3)
// All edits go through SerializedObject, so Undo works.
[CustomEditor(typeof(PlazaAreaPolygon))]
public class PlazaAreaPolygonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "Scene 뷰에서 편집:\n" +
            "• 점 드래그: 꼭짓점 이동\n" +
            "• 변 가운데 원 클릭: 꼭짓점 추가\n" +
            "• Ctrl(Cmd) + 점 클릭: 꼭짓점 삭제 (최소 3개)\n" +
            "기준은 해달 발밑입니다. WalkableArea를 선택하면 실제 이동 격자가 보입니다.",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        var polygon = (PlazaAreaPolygon)target;
        Transform t = polygon.transform;

        serializedObject.Update();
        SerializedProperty points = serializedObject.FindProperty("points");
        int count = points.arraySize;
        if (count == 0) return;

        bool deleteMode = Event.current.control || Event.current.command;
        Color baseColor = polygon.Kind == PlazaAreaKind.Walkable ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.3f, 0.2f);

        for (int i = 0; i < count; i++)
        {
            SerializedProperty point = points.GetArrayElementAtIndex(i);
            Vector3 world = t.TransformPoint(point.vector2Value);
            float size = HandleUtility.GetHandleSize(world) * 0.07f;

            if (deleteMode)
            {
                Handles.color = Color.red;
                if (Handles.Button(world, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap) && count > 3)
                {
                    points.DeleteArrayElementAtIndex(i);
                    break;
                }
                continue;
            }

            Handles.color = baseColor;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                point.vector2Value = t.InverseTransformPoint(moved);
            }
        }

        if (!deleteMode && points.arraySize == count)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.8f);
            for (int i = 0; i < count; i++)
            {
                Vector2 a = points.GetArrayElementAtIndex(i).vector2Value;
                Vector2 b = points.GetArrayElementAtIndex((i + 1) % count).vector2Value;
                Vector2 midLocal = (a + b) * 0.5f;
                Vector3 mid = t.TransformPoint(midLocal);
                float size = HandleUtility.GetHandleSize(mid) * 0.05f;
                if (Handles.Button(mid, Quaternion.identity, size, size * 1.5f, Handles.CircleHandleCap))
                {
                    points.InsertArrayElementAtIndex(i + 1);
                    points.GetArrayElementAtIndex(i + 1).vector2Value = midLocal;
                    break;
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
