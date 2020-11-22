using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Yorozu.EditorTools
{
	public class FindMissingScriptWindow : EditorWindow
	{
		[MenuItem("Tools/Find Missing Script")]
		private static void ShowWindow()
		{
			var window = GetWindow<FindMissingScriptWindow>();
			window.titleContent = new GUIContent("Find Missing Script");
			window.Show();
		}

		[SerializeField]
		private List<MissingScript> _missingScripts = new List<MissingScript>();
		private Vector2 _scrollPosition;

		private void OnGUI()
		{
			if (GUILayout.Button("Search Missing Script"))
				FindMissingScript();

			if (_missingScripts.Count <= 0)
				return;

			using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
			{
				_scrollPosition = scroll.scrollPosition;
				using (new EditorGUILayout.VerticalScope())
				{
					foreach (var script in _missingScripts)
					{
						using (new EditorGUI.DisabledScope(true))
						{
							EditorGUILayout.ObjectField(script.GameObject, typeof(GameObject), true);
						}

						foreach (var missionObject in script.MissionObjects)
							if (GUILayout.Button(missionObject.Path, EditorStyles.linkLabel))
								Selection.objects = new Object[] {missionObject.TargetObject};
					}
				}
			}

			if (GUILayout.Button("Remove All Missing Scripts"))
			{
				foreach (var script in _missingScripts)
				{
					foreach (var missionObject in script.MissionObjects)
					{
#if UNITY_2019_2_OR_NEWER
						GameObjectUtility.RemoveMonoBehavioursWithMissingScript(missionObject.TargetObject);
#else
						GameObject.DestroyImmediate(missionObject.TargetObject);
#endif
					}

					EditorUtility.SetDirty(script.GameObject);
				}

				_missingScripts.Clear();
				AssetDatabase.SaveAssets();
			}
		}

		[Serializable]
		private class MissingScript
		{
			public GameObject GameObject;
			public List<MissingObjInfo> MissionObjects = new List<MissingObjInfo>();

			public MissingScript(GameObject gameObject)
			{
				GameObject = gameObject;
			}

			public void Add(GameObject gameObject)
			{
				MissionObjects.Add(new MissingObjInfo(GameObject, gameObject));
			}
		}

		[Serializable]
		private class MissingObjInfo
		{
			public GameObject TargetObject;
			public string Path;

			public MissingObjInfo(GameObject parent, GameObject target)
			{
				TargetObject = target;
				Path = GetPath(parent.transform, target.transform);
			}

			private string GetPath(Transform parent, Transform target)
			{
				var builder = new StringBuilder();
				builder.Append(target.name);
				while (parent != target)
				{
					target = target.parent;
					builder.Insert(0, target.name + "/");
				}

				return builder.ToString();
			}
		}

		private void FindMissingScript()
		{
			_missingScripts.Clear();
			var guids = AssetDatabase.FindAssets("t:prefab");


			for (var index = 0; index < guids.Length; index++)
			{
				if (index % 10 == 0)
					if (EditorUtility.DisplayCancelableProgressBar(
						"Check Asset",
						string.Format("{0} / {1}", index, guids.Length),
						index / (float) guids.Length)
					)
						break;

				var guid = guids[index];
				MissingScript script = null;
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				foreach (var cobj in obj.GetComponentsInChildren<Transform>())
				{
#if UNITY_2019_2_OR_NEWER
					if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(cobj.gameObject) > 0)
					{
#else
					foreach (var c in cobj.GetComponents<Component>())
					{
						if (c != null)
							continue;
#endif
						if (script == null)
							script = new MissingScript(obj);

						script.Add(cobj.gameObject);
					}
					if (script != null)
						_missingScripts.Add(script);
				}
			}

			EditorUtility.ClearProgressBar();

			if (_missingScripts.Count <= 0)
				EditorUtility.DisplayDialog("Search Result", "Not Found Missing Script", "ok");
		}
	}
}
