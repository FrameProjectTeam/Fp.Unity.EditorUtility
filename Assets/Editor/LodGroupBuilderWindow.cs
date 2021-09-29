using System;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;
using UnityEngine.Assertions;

using Object = UnityEngine.Object;

namespace Fp.Editor
{
    public sealed class LodGroupBuilderWindow : EditorWindow
    {
        private readonly List<LODGroup> _lodGroupBuffer = new List<LODGroup>();

        private readonly List<List<Renderer>> _lodMergeGroups = new List<List<Renderer>>();

        private GameObject[] _targetObjects = Array.Empty<GameObject>();

        private void OnGUI()
        {
            var objectCount = 0;

            foreach (GameObject go in Selection.gameObjects)
            {
                if (!go.activeInHierarchy)
                {
                    continue;
                }

                if (_targetObjects.Length <= objectCount)
                {
                    Array.Resize(ref _targetObjects, (objectCount + 1) * 2);
                }

                _targetObjects[objectCount++] = go;
            }

            if (objectCount > 1)
            {
                EditorGUILayout.LabelField("Selected objects:");
                for (var i = 0; i < objectCount; i++)
                {
                    GUILayout.BeginHorizontal();
                    try
                    {
                        EditorGUILayout.ObjectField(_targetObjects[i], typeof(GameObject), true);
                        if (GUILayout.Button("Combine into"))
                        {
                            for (var j = 0; j < objectCount; j++)
                            {
                                if (j == i)
                                {
                                    continue;
                                }

                                if (!CanMoveObject(_targetObjects[j]))
                                {
                                    continue;
                                }

                                _targetObjects[j].transform.SetParent(_targetObjects[i].transform);
                            }

                            Selection.activeGameObject = _targetObjects[i];
                        }
                    }
                    finally
                    {
                        GUILayout.EndHorizontal();
                    }

                    if (!CanMoveObject(_targetObjects[i]))
                    {
                        EditorGUILayout.HelpBox("Is part of prefab, can't move it into another", MessageType.Warning);
                    }
                }
            }
            else if (objectCount == 1)
            {
                //Combine child to parent
                _lodGroupBuffer.Clear();

                GameObject targetObject = _targetObjects[0];
                targetObject.GetComponentsInChildren(_lodGroupBuffer);
                var targetLodGroup = targetObject.GetComponent<LODGroup>();

                if (_lodGroupBuffer.Count == 0)
                {
                    EditorGUILayout.HelpBox($"Selected object doesn't contain {nameof(LODGroup)} component", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField("Found groups:");
                    foreach (LODGroup group in _lodGroupBuffer)
                    {
                        EditorGUILayout.ObjectField(group, typeof(LODGroup), true);
                    }

                    EditorGUILayout.Separator();

                    if (GUILayout.Button("Combine"))
                    {
                        if (!targetLodGroup)
                        {
                            targetLodGroup = targetObject.AddComponent<LODGroup>();
                        }
                        else
                        {
                            _lodGroupBuffer.Remove(targetLodGroup);

                            LOD[] targetLods = targetLodGroup.GetLODs();
                            for (var i = 0; i < targetLods.Length; i++)
                            {
                                AddToMergeGroup(_lodMergeGroups, i, targetLods[i].renderers);
                            }
                        }

                        foreach (LODGroup lodGroup in _lodGroupBuffer)
                        {
                            LOD[] targetLods = lodGroup.GetLODs();
                            for (var i = 0; i < targetLods.Length; i++)
                            {
                                AddToMergeGroup(_lodMergeGroups, i, targetLods[i].renderers);
                            }

                            DestroyImmediate(lodGroup);
                        }

                        targetLodGroup.SetLODs(ConvertMergeGroups(_lodMergeGroups));
                        ClearMergeGroups(_lodMergeGroups);

                        targetLodGroup.RecalculateBounds();
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("First select object to combine into one LOD group", MessageType.Info);
            }
        }

        [MenuItem("Tools/Utility/Lod Group Builder")]
        private static void Init()
        {
            var window = GetWindow<LodGroupBuilderWindow>();

            GUIContent icon = EditorGUIUtility.IconContent("d_LODGroup Icon");
            icon.text = "Lod Group Merger";

            window.titleContent = icon;

            window.Show();
        }

        private static void ClearMergeGroups(List<List<Renderer>> groups)
        {
            foreach (List<Renderer> renderers in groups)
            {
                renderers.Clear();
            }
        }

        private static void AddToMergeGroup(IList<List<Renderer>> groups, int lodGroup, params Renderer[] renderer)
        {
            Assert.IsNotNull(renderer);

            while (groups.Count <= lodGroup)
            {
                groups.Add(new List<Renderer>());
            }

            groups[lodGroup].AddRange(renderer);
        }

        private static LOD[] ConvertMergeGroups(List<List<Renderer>> groups)
        {
            var lodGroup = 0;
            LOD[] lods = Array.Empty<LOD>();

            foreach (List<Renderer> rl in groups)
            {
                if (rl.Count > 0)
                {
                    Array.Resize(ref lods, lods.Length + 1);
                }

                lods[lodGroup++] = new LOD(CalculateLodGroupHeight(lodGroup), rl.ToArray());
            }

            return lods;
        }

        private static float CalculateLodGroupHeight(int lodGroup)
        {
            return Mathf.Pow(0.4f, lodGroup);
        }

        private static bool CanMoveObject(Object targetObject)
        {
            return !PrefabUtility.IsPartOfAnyPrefab(targetObject) || IsPrefabRoot(targetObject);
        }

        private static bool IsPrefabRoot(Object targetObject)
        {
            return PrefabUtility.GetOutermostPrefabInstanceRoot(targetObject).Equals(targetObject);
        }
    }
}