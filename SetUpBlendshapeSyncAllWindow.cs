using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.Utility;

namespace jp.illusive_isc.tools.SetUpBlendshapeSync
{
    [System.Serializable]
    public class BlendShapeItem
    {
        public bool isActive;
        public string selectBlendShapeName;
        public List<string> blendShapeSubName = new();

        public void RemoveSubList(string syncName) => blendShapeSubName.Remove(syncName);
        public List<string> GetSubList() => blendShapeSubName;
    }

    public class SetUpBlendshapeSyncAllWindow : EditorWindow
    {
        [SerializeField] public GameObject selectedObj = null;
        private List<Mesh> meshes = new();
        [SerializeField] public List<BlendShapeItem> blendShapeList = new();
        public SerializedObject serializedObject;
        public SerializedProperty selectedObjProperty;
        public SerializedProperty blendShapeListProperty;
        private Vector2 scrollPosLeft;
        private Vector2 scrollPosRight;
        public List<BlendShapeItem> GetBlendShapeList() => blendShapeList;
        private string searchQuery = "";
        private string shapeName;
        [MenuItem("GameObject/Modular Avatar/SetUp Blendshape Sync", false, -1000)]
        private static void SetAsTarget(MenuCommand command)
        {
            GameObject selectedObject = command.context as GameObject;
            if (selectedObject != null)
            {
                ShowWindow().selectedObjProperty.objectReferenceValue = selectedObject;
            }
        }
        [MenuItem("Tools/illusive_tools/SetUpBlendshapeSync")]
        public static void OpenWindow() => ShowWindow();

        private void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            selectedObjProperty = serializedObject.FindProperty("selectedObj");
            blendShapeListProperty = serializedObject.FindProperty("blendShapeList");
        }

        public static SetUpBlendshapeSyncAllWindow ShowWindow() => GetWindow<SetUpBlendshapeSyncAllWindow>("BlendShape Selector");

        private void OnGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(selectedObjProperty, new GUIContent("Selected Object"));
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            // var selected = Selection.activeGameObject;
            // var getSMR = selected?.GetComponent<SkinnedMeshRenderer>();

            // if (selectedObjProperty.objectReferenceValue != (getSMR ? selected : null))
            // {
            //     selectedObjProperty.objectReferenceValue = getSMR ? selected : null;
            //     blendShapeListProperty?.ClearArray();
            //     serializedObject.ApplyModifiedProperties();
            //     ShowWindow(shapeName = "");
            // }

            if (selectedObjProperty.objectReferenceValue != null)
            {

                EditorGUILayout.HelpBox($"MABlendshapeSyncを一括で設定します", MessageType.Info);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("一括適用")) SetUpBlendshapeSync(true);
                if (GUILayout.Button("一括削除")) SetUpBlendshapeSync(false);
                GUILayout.EndHorizontal();

                EditorGUILayout.Space();

                if (GUILayout.Button("選択初期化"))
                {
                    ShowWindow(shapeName = "");
                    blendShapeList = new();
                    return;
                }

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();

                // 左カラム
                EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2));
                scrollPosLeft = EditorGUILayout.BeginScrollView(scrollPosLeft, GUILayout.ExpandHeight(true));
                EditorGUILayout.LabelField("設定するBlendShape:", EditorStyles.boldLabel);

                int i = 0;
                foreach (var item in GetBlendShapeList())
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{(GetBlendShapeList().Count == ++i ? "└" : "├")} {item.selectBlendShapeName}");
                    if (GUILayout.Button("名称不一致のものを検索"))
                    {
                        shapeName = item.selectBlendShapeName;
                        ShowWindow(item.selectBlendShapeName);
                    }
                    GUILayout.EndHorizontal();

                    int j = 0;
                    foreach (var syncName in item.GetSubList())
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"{(GetBlendShapeList().Count == i ? "    └" : (item.GetSubList().Count == ++j ? "│└" : "│├"))} {syncName}");
                        if (GUILayout.Button("×", GUILayout.Width(20))) item.RemoveSubList(syncName);
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                // 右カラム
                EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2));
                scrollPosRight = EditorGUILayout.BeginScrollView(scrollPosRight, GUILayout.ExpandHeight(true));


                GUIStyle customStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    fontSize = 32,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(10, 10, 10, 10)
                };

                Color backgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = string.IsNullOrEmpty(shapeName) ? Color.red : Color.blue;
                GUILayout.Label(shapeName == "" ? "ブレンドシェイプ検索" : "名称不一致キー検索", customStyle, GUILayout.ExpandWidth(true));
                GUI.backgroundColor = backgroundColor;
                searchQuery = EditorGUILayout.TextField("検索:", searchQuery);

                List<string> blendShapeNames = meshes.SelectMany(mesh => Enumerable.Range(0, mesh.blendShapeCount).Select(i => mesh.GetBlendShapeName(i))).Distinct().ToList();

                foreach (var blendShapeName in blendShapeNames)
                {
                    if (shapeName == "") SelectMain(blendShapeName);
                    else if (shapeName != blendShapeName) SelectSub(blendShapeName);
                }

                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("選択されたオブジェクトがありません。", MessageType.Warning);
            }
        }

        private void SetUpBlendshapeSync(bool v)
        {
            var root = selectedObj.transform.root.gameObject;
            var sMRAll = root.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var sMR in sMRAll)
            {
                if (sMR.gameObject == selectedObj) continue;

                // パフォーマンス向上のため HashSet を使用
                HashSet<string> blendShapeNameSet = new HashSet<string>();
                for (int i = 0; i < sMR.sharedMesh.blendShapeCount; i++)
                {
                    blendShapeNameSet.Add(sMR.sharedMesh.GetBlendShapeName(i));
                }

                foreach (var blendShape in blendShapeList)
                {
                    if (blendShapeNameSet.Contains(blendShape.selectBlendShapeName))
                    {
                        var MABlendshapeSync = sMR.gameObject.GetComponent<ModularAvatarBlendshapeSync>() ?? sMR.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
                        var blendshapeBindingList = MABlendshapeSync.Bindings;

                        var existingBinding = blendshapeBindingList.FirstOrDefault(b => b.Blendshape == blendShape.selectBlendShapeName);
                        var newBinding = new BlendshapeBinding { ReferenceMesh = new AvatarObjectReference(), Blendshape = blendShape.selectBlendShapeName };
                        newBinding.ReferenceMesh.referencePath = selectedObj.transform.Path();

                        if (!existingBinding.Equals(default))
                        {
                            // 既存バインディングを更新
                            existingBinding.ReferenceMesh.referencePath = selectedObj.transform.Path();
                        }
                        else
                        {
                            // 新しいバインディングを追加
                            blendshapeBindingList.Add(newBinding);
                        }
                    }

                    foreach (var blendShapeNameSub in blendShape.blendShapeSubName)
                    {
                        if (blendShapeNameSet.Contains(blendShapeNameSub))
                        {
                            var MABlendshapeSync = sMR.gameObject.GetComponent<ModularAvatarBlendshapeSync>() ?? sMR.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
                            var blendshapeBindingList = MABlendshapeSync.Bindings;

                            var existingBinding = blendshapeBindingList.FirstOrDefault(b => b.LocalBlendshape == blendShapeNameSub);
                            var newBinding = new BlendshapeBinding { ReferenceMesh = new AvatarObjectReference(), Blendshape = blendShape.selectBlendShapeName, LocalBlendshape = blendShapeNameSub };
                            newBinding.ReferenceMesh.referencePath = selectedObj.transform.Path();

                            if (!existingBinding.Equals(default))
                            {
                                existingBinding.ReferenceMesh.referencePath = selectedObj.transform.Path();
                            }
                            else
                            {
                                blendshapeBindingList.Add(newBinding);
                            }
                            break;
                        }
                    }
                }
            }
        }
        public void ShowWindow(string blendShapeName)
        {
            if (string.IsNullOrEmpty(blendShapeName))
            {
                SkinnedMeshRenderer SMR = ((GameObject)selectedObjProperty.objectReferenceValue)?.GetComponent<SkinnedMeshRenderer>();
                if (SMR) meshes = new() { SMR.sharedMesh };
            }
            else
            {
                shapeName = blendShapeName;
                foreach (var SMR in ((GameObject)selectedObjProperty.objectReferenceValue).transform.root?.GetComponentsInChildren<SkinnedMeshRenderer>())
                    meshes.Add(SMR.sharedMesh);
            }
        }

        private void SelectSub(string blendShapeName)
        {
            if (!string.IsNullOrEmpty(searchQuery) && !blendShapeName.ToLower().Contains(searchQuery.ToLower())) return;

            var blendShapeItem = blendShapeList.FirstOrDefault(b => b.selectBlendShapeName == shapeName);

            if (blendShapeItem == null) return;

            bool isAlreadyAdded = blendShapeItem.blendShapeSubName.Contains(blendShapeName);

            GUILayout.BeginHorizontal();
            GUI.enabled = !isAlreadyAdded;
            if (GUILayout.Button(blendShapeName, GUILayout.ExpandWidth(true)))
            {
                ShowWindow(shapeName = "");
                blendShapeItem.blendShapeSubName.Add(blendShapeName);
            }
            GUI.enabled = true;


            GUILayout.EndHorizontal();
        }

        private void SelectMain(string blendShapeName)
        {
            if (!string.IsNullOrEmpty(searchQuery) && !blendShapeName.ToLower().Contains(searchQuery.ToLower())) return;


            bool isAlreadyAdded = blendShapeList.Any(item => item.selectBlendShapeName == blendShapeName);

            GUILayout.BeginHorizontal();
            GUI.enabled = isAlreadyAdded;
            if (GUILayout.Button("×", GUILayout.Width(20))) blendShapeList.RemoveAll(item => item.selectBlendShapeName == blendShapeName);
            GUI.enabled = true;

            GUI.enabled = !isAlreadyAdded;
            if (GUILayout.Button(blendShapeName, GUILayout.ExpandWidth(true)))
            {
                blendShapeList.Add(new BlendShapeItem { isActive = true, selectBlendShapeName = blendShapeName });
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }
    }
}
