using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
namespace Zios.Editors{
	using combine = CombineMeshes;
	public static class CombineMeshes{
		private static List<Mesh> meshes = new List<Mesh>();
		private static GameObject[] selection;
		private static Transform target;
		private static MeshFilter[] filters;
		private static CombineInstance[] combines;
		private static int index;
		private static int subIndex;
		private static int meshCount;
		private static int vertexCount;
		private static int meshNumber = 1;
		private static float time;
		private static bool inline;
		private static bool complete;
		[MenuItem("Zios/Dori/Combine Meshes")]
		private static void Combine(){
			if(Selection.gameObjects.Length < 1){ return; }
			List<MeshFilter> filters = new List<MeshFilter>();
			combine.meshes.Clear();
			combine.meshes.Add(new Mesh());
			combine.selection = Selection.gameObjects.Copy();
			foreach(GameObject current in combine.selection){
				filters.AddRange(current.GetComponentsInChildren<MeshFilter>());
			}
			combine.filters = filters.ToArray();
			combine.meshCount = combine.filters.Length;
			combine.combines = new CombineInstance[combine.meshCount];
			combine.index = 0;
			combine.subIndex = 0;
			combine.vertexCount = 0;
			combine.time = Time.realtimeSinceStartup;
			combine.complete = false;
			combine.inline = true;
			int passesPerStep = 1000;
			while(passesPerStep > 0){
				EditorApplication.update += combine.Step;
				passesPerStep -= 1;
			}
		}
		private static void StepLast(){
			int end = combine.index - combine.subIndex;
			List<CombineInstance> range = new List<CombineInstance>(combine.combines).GetRange(combine.subIndex,end);
			Mesh finalMesh = combine.meshes.Last();
			finalMesh.CombineMeshes(range.ToArray());
			Unwrapping.GenerateSecondaryUVSet(finalMesh);
		}
		private static void Step(){
			if(combine.complete){ return; }
			int index = combine.index;
			MeshFilter filter = combine.filters[index];
			string updateMessage = "Mesh " + index + "/" + combine.meshCount;
			bool canceled = EditorUtility.DisplayCancelableProgressBar("Combining Meshes",updateMessage,((float)index) / combine.meshCount);
			if(canceled){ combine.meshCount = 0; }
			else if(filter != null && filter.sharedMesh != null){
				if((combine.vertexCount + filter.sharedMesh.vertexCount) >= 65534){
					Debug.Log("[Combine Meshes] Added extra submesh due to vertices at " + combine.vertexCount);
					combine.StepLast();
					combine.meshes.Add(new Mesh());
					combine.subIndex = index;
					combine.vertexCount = 0;
				}
				Mesh currentMesh = filter.sharedMesh;
				if(filter.sharedMesh.subMeshCount > 1){
					currentMesh = (Mesh)UnityEngine.Object.Instantiate(filter.sharedMesh);
					currentMesh.triangles = currentMesh.triangles;
				}
				combine.combines[index].mesh = currentMesh;
				combine.combines[index].transform = filter.transform.localToWorldMatrix;
				combine.vertexCount += currentMesh.vertexCount;
				if(combine.inline){
					Component.DestroyImmediate(filter.gameObject.GetComponent<MeshRenderer>());
					Component.DestroyImmediate(filter.gameObject.GetComponent<MeshFilter>());
				}
			}
			combine.index += 1;
			if(combine.index >= combine.meshCount){
				if(!canceled){
					combine.StepLast();
					Material material = FileManager.GetAsset<Material>("Baked.mat");
					if(!combine.inline){
						foreach(GameObject current in combine.selection){
							GameObject target = (GameObject)GameObject.Instantiate(current);
							target.name = target.name.Replace("(Clone)","");
							target.transform.parent = Locate.GetScenePath("Scene-Combined").transform;
							MeshFilter[] filters = target.GetComponentsInChildren<MeshFilter>();
							foreach(MeshFilter nullFilter in filters){
								Component.DestroyImmediate(nullFilter.gameObject.GetComponent<MeshRenderer>());
								Component.DestroyImmediate(nullFilter.gameObject.GetComponent<MeshFilter>());
							}
							current.SetActive(false);
						}
					}
					bool singleRoot = combine.selection.Length == 1;
					string start = singleRoot ? combine.selection[0].name + "/" : "";
					foreach(Mesh mesh in combine.meshes){
						GameObject container = new GameObject("@Mesh" + combine.meshNumber);
						if(combine.inline && singleRoot){
							container.transform.parent = combine.selection[0].transform;
						}
						else{
							container.transform.parent = Locate.GetScenePath("Scene-Combined/" + start).transform;
						}
						MeshRenderer containerRenderer = container.AddComponent<MeshRenderer>();
						MeshFilter containerFilter = container.AddComponent<MeshFilter>();
						string path = EditorSceneManager.GetActiveScene().name.GetDirectory();
						string folder = "@" + EditorSceneManager.GetActiveScene().name.GetFileName();
						FileManager.Create(path + "/" + folder + "/");
						AssetDatabase.CreateAsset(mesh,path + "/" + folder + "/Combined" + meshNumber + ".asset");
						containerFilter.mesh = mesh;
						containerRenderer.material = new Material(material);
						combine.meshNumber += 1;
					}
				}
				TimeSpan span = TimeSpan.FromSeconds(Time.realtimeSinceStartup - combine.time);
				string totalTime = span.Minutes + " minutes and " + span.Seconds + " seconds";
				Debug.Log("[Combine Meshes] Reduced " + combine.meshCount + " meshes to " + combine.meshes.Count + ".");
				Debug.Log("[Combine Meshes] Completed in " + totalTime + ".");
				AssetDatabase.SaveAssets();
				EditorUtility.ClearProgressBar();
				combine.complete = true;
				while(EditorApplication.update == combine.Step){
					EditorApplication.update -= combine.Step;
				}
			}
		}
	}
}