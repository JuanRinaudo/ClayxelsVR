
#if UNITY_EDITOR // exclude from build

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using Clayxels;

namespace Clayxels{
	[CustomEditor(typeof(ClayContainer))]
	public class ClayxelInspector : Editor{
		public override void OnInspectorGUI(){
			ClayContainer clayxel = (ClayContainer)this.target;

			EditorGUILayout.LabelField("Clayxels, V0.51 beta");
			EditorGUILayout.LabelField("clayObjects: " + clayxel.getNumClayObjects());

			#if !CLAYXELS_FULL
				EditorGUILayout.LabelField("free version limit is 64");
			#else
				EditorGUILayout.LabelField("limit is " + clayxel.getMaxSolids());
			#endif

			EditorGUILayout.Space();

			EditorGUI.BeginChangeCheck();
			int gridResolution = EditorGUILayout.IntField("resolution", clayxel.gridResolution);
			Vector3Int gridSize = EditorGUILayout.Vector3IntField("containerSize", new Vector3Int(clayxel.gridSizeX, clayxel.gridSizeY, clayxel.gridSizeZ));
			
			if(EditorGUI.EndChangeCheck()){
				Undo.RecordObject(this.target, "changed clayxel grid"); 

				clayxel.gridResolution = gridResolution;
				clayxel.gridSizeX = gridSize.x;
				clayxel.gridSizeY = gridSize.y;
				clayxel.gridSizeZ = gridSize.z;

				clayxel.init();
				clayxel.needsUpdate = true;
				UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
				ClayContainer.getSceneView().Repaint();

				return;
			}

			EditorGUI.BeginChangeCheck();

			EditorGUILayout.Space();

			Material customMaterial = (Material)EditorGUILayout.ObjectField("customMaterial", clayxel.customMaterial, typeof(Material), false);

			float materialSmoothness = 0.0f;
			float materialMetallic = 0.0f;
			Color materialEmission = new Color();
			float materialEmissionIntensity = 0.0f;
			float splatSizeMultiplier = 1.0f;
			float normalOrientedSplat = 1.0f;
			Texture2D splatTexture = null;

			if(customMaterial == null){
				materialSmoothness = EditorGUILayout.FloatField("smoothness", clayxel.materialSmoothness);
				materialMetallic = EditorGUILayout.FloatField("metallic", clayxel.materialMetallic);
				materialEmission = EditorGUILayout.ColorField("emission", clayxel.materialEmission);
				materialEmissionIntensity = EditorGUILayout.FloatField("emissionIntensity", clayxel.materialEmissionIntensity);
				splatSizeMultiplier = EditorGUILayout.FloatField("clayxelsSize", clayxel.splatSizeMultiplier);
				normalOrientedSplat = EditorGUILayout.FloatField("normal oritented", clayxel.normalOrientedSplat);
				splatTexture = (Texture2D)EditorGUILayout.ObjectField("texture", clayxel.splatTexture, typeof(Texture2D), false);
			}
			
			if(EditorGUI.EndChangeCheck()){
				Undo.RecordObject(this.target, "changed clayxel container");
				
				if(customMaterial == null){
					clayxel.materialSmoothness = materialSmoothness;
					clayxel.materialMetallic = materialMetallic;
					clayxel.materialEmission = materialEmission;
					clayxel.materialEmissionIntensity = materialEmissionIntensity;
					clayxel.splatSizeMultiplier = splatSizeMultiplier;
					clayxel.normalOrientedSplat = normalOrientedSplat;
					clayxel.splatTexture = splatTexture;
				}

				if(clayxel.normalOrientedSplat < 0.0f){
					clayxel.normalOrientedSplat = 0.0f;
				}
				else if(clayxel.normalOrientedSplat > 1.0f){
					clayxel.normalOrientedSplat = 1.0f;
				}

				clayxel.updateSplatLook();

				if(customMaterial != clayxel.customMaterial){
					clayxel.customMaterial = customMaterial;
					clayxel.init();
				}
				
				clayxel.needsUpdate = true;
				clayxel.forceUpdateAllChunks();
				UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
				ClayContainer.getSceneView().Repaint();
				
				return;
			}

			EditorGUILayout.Space();

			if(GUILayout.Button("reload all")){
				ClayContainer.reloadAll();
			}

			if(GUILayout.Button("pick solid (p)")){
				ClayContainer.startPicking();
			}

			if(GUILayout.Button("add solid")){
				ClayObject clayObj = ((ClayContainer)this.target).addSolid();

				Undo.RegisterCreatedObjectUndo(clayObj.gameObject, "added clayxel solid");
				UnityEditor.Selection.objects = new GameObject[]{clayObj.gameObject};

				clayxel.needsUpdate = true;
				UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
				ClayContainer.getSceneView().Repaint();

				return;
			}

			EditorGUILayout.Space();

			#if CLAYXELS_FULL
				clayxel.exportRetopoFBX = EditorGUILayout.Toggle("retopology", clayxel.exportRetopoFBX);
				if(clayxel.exportRetopoFBX){
					clayxel.retopoMaxVerts = EditorGUILayout.IntField("max verts", clayxel.retopoMaxVerts);
					
					clayxel.retopoFbxFile = EditorGUILayout.TextField("export fbx", clayxel.retopoFbxFile);
				}
			#endif

			if(!clayxel.hasCachedMesh()){
				if(GUILayout.Button("freeze to mesh")){

					if(clayxel.exportRetopoFBX){
						clayxel.retopoMesh();
					}
					else{
						clayxel.generateMesh();
					}
				}
			}
			else{
				if(GUILayout.Button("defrost clayxels")){
					clayxel.disableMesh();
					UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
					ClayContainer.getSceneView().Repaint();
				}
			}
		}
	}

	[CustomEditor(typeof(ClayObject)), CanEditMultipleObjects]
	public class ClayObjectInspector : Editor{
		
		public override void OnInspectorGUI(){
			ClayObject clayObj = (ClayObject)this.targets[0];

			EditorGUI.BeginChangeCheck();

			float blend = EditorGUILayout.FloatField("blend", clayObj.blend);
			if(blend > 1.0f){
				blend = 1.0f;
			}
			else if(blend < -1.0f){
				blend = -1.0f;
			}

			Color color = EditorGUILayout.ColorField("color", clayObj.color);
			
			ClayContainer clayxel = clayObj.getClayxelContainer();
			string[] solidsLabels = clayxel.getSolidsCatalogueLabels();
	 		int primitiveType = EditorGUILayout.Popup("solidType", clayObj.primitiveType, solidsLabels);

	 		Dictionary<string, float> paramValues = new Dictionary<string, float>();
	 		paramValues["x"] = clayObj.attrs.x;
	 		paramValues["y"] = clayObj.attrs.y;
	 		paramValues["z"] = clayObj.attrs.z;
	 		paramValues["w"] = clayObj.attrs.w;

	 		List<string[]> parameters = clayxel.getSolidsCatalogueParameters(primitiveType);
	 		List<string> wMaskLabels = new List<string>();
	 		for(int paramIt = 0; paramIt < parameters.Count; ++paramIt){
	 			string[] parameterValues = parameters[paramIt];
	 			string attr = parameterValues[0];
	 			string label = parameterValues[1];
	 			string defaultValue = parameterValues[2];

	 			if(primitiveType != clayObj.primitiveType){
	 				// reset to default params when changing primitive type
	 				paramValues[attr] = float.Parse(defaultValue);
	 			}
	 			
	 			if(attr.StartsWith("w")){
	 				wMaskLabels.Add(label);
	 			}
	 			else{
	 				paramValues[attr] = EditorGUILayout.FloatField(label, paramValues[attr]);
	 			}
	 		}

	 		if(wMaskLabels.Count > 0){
	 			paramValues["w"] = (float)EditorGUILayout.MaskField("options", (int)clayObj.attrs.w, wMaskLabels.ToArray());
	 		}

	 		if(EditorGUI.EndChangeCheck()){
	 			Undo.RecordObjects(this.targets, "changed clayobject");

	 			for(int i = 1; i < this.targets.Length; ++i){
	 				bool somethingChanged = false;
	 				ClayObject currentClayObj = (ClayObject)this.targets[i];

	 				bool shouldAutoRename = false;

	 				if(clayObj.blend != blend){
	 					currentClayObj.blend = blend;
	 					somethingChanged = true;
	 					shouldAutoRename = true;
	 				}

	 				if(clayObj.color != color){
	 					currentClayObj.color = color;
	 					somethingChanged = true;
	 				}
					
	 				if(clayObj.primitiveType != primitiveType){

	 					currentClayObj.primitiveType = primitiveType;
	 					somethingChanged = true;
	 					shouldAutoRename = true;
	 				}

	 				if(clayObj.attrs.x != paramValues["x"]){
	 					currentClayObj.attrs.x = paramValues["x"];
	 					somethingChanged = true;
	 				}

	 				if(clayObj.attrs.y != paramValues["y"]){
	 					currentClayObj.attrs.y = paramValues["y"];
	 					somethingChanged = true;
	 				}

	 				if(clayObj.attrs.z != paramValues["z"]){
	 					currentClayObj.attrs.z = paramValues["z"];
	 					somethingChanged = true;
	 				}

	 				if(clayObj.attrs.w != paramValues["w"]){
	 					currentClayObj.attrs.w = paramValues["w"];
	 					somethingChanged = true;
	 					shouldAutoRename = true;
	 				}

	 				if(somethingChanged){
	 					currentClayObj.getClayxelContainer().clayObjectUpdated(currentClayObj);

	 					if(shouldAutoRename){
		 					if(currentClayObj.gameObject.name.StartsWith("clay_")){
		 						this.autoRename(currentClayObj, solidsLabels);
		 					}
		 				}
	 				}
				}

	 			clayObj.blend = blend;
	 			clayObj.color = color;
	 			clayObj.primitiveType = primitiveType;
	 			clayObj.attrs.x = paramValues["x"];
	 			clayObj.attrs.y = paramValues["y"];
	 			clayObj.attrs.z = paramValues["z"];
	 			clayObj.attrs.w = paramValues["w"];

	 			if(clayObj.gameObject.name.StartsWith("clay_")){
					this.autoRename(clayObj, solidsLabels);
				}
	 			
	 			clayxel.clayObjectUpdated(clayObj);
	 			UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
	 			ClayContainer.getSceneView().Repaint();
			}
		}

		void autoRename(ClayObject clayObj, string[] solidsLabels){
			string blendSign = "+";
			if(clayObj.blend < 0.0f){
				blendSign = "-";
			}

			string isColoring = "";
			if(clayObj.attrs.w == 1.0f){
				blendSign = "";
				isColoring = "[paint]";
			}

			clayObj.gameObject.name = "clay_" + solidsLabels[clayObj.primitiveType] + blendSign + isColoring;
		}
	}
}

#endif // end if UNITY_EDITOR
