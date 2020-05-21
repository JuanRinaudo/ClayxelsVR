
// enable this to improve performance on modern video cards
// #define CLAYXELS_INDIRECTDRAW

// #define CLAYXELS_FULL // this has only effect on the inspector

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.SceneManagement;

	#if CLAYXELS_RETOPO
		using UnityEditor.Formats.Fbx.Exporter;
		using System.IO;
		using System.Text;
	#endif
#endif

namespace Clayxels{
	[ExecuteInEditMode]
	public class ClayContainer : MonoBehaviour{
		class ClayxelChunk{
			public ComputeBuffer pointCloudDataBuffer;
			public ComputeBuffer indirectDrawArgsBuffer;
			public Vector3 center = new Vector3();
			public Material clayxelMaterial;
			public Material clayxelPickingMaterial;

			#if DRAW_DEBUG
				public ComputeBuffer debugGridOutPointsBuffer;
			#endif
		}

		public int gridResolution = 8;
		public int gridSizeX = 1;
		public int gridSizeY = 1;
		public int gridSizeZ = 1;
		public float normalOrientedSplat = 1.0f;
		public Material customMaterial = null;
		public float materialSmoothness = 0.5f;
		public float materialMetallic = 0.5f;
		public Color materialEmission = new Color(0.0f, 0.0f, 0.0f, 1.0f);
		public float materialEmissionIntensity = 0.0f;
		public Texture2D splatTexture = null;
		public float splatSizeMultiplier = 1.0f;
		public bool exportRetopoFBX = false;
		public int retopoMaxVerts = -1;
		public string retopoFbxFile = "";

		static public bool globalDataNeedsInit = true;

		static List<string> solidsCatalogueLabels = new List<string>();
		static List<List<string[]>> solidsCatalogueParameters = new List<List<string[]>>();
		static ComputeShader claycoreCompute;
		static ComputeBuffer gridDataBuffer;
		static ComputeBuffer triangleConnectionTable;
		static ComputeBuffer prefilteredSolidIdsBuffer;
		static ComputeBuffer numSolidsPerChunkBuffer;

		static List<ComputeBuffer> globalCompBuffers = new List<ComputeBuffer>();
		static int lastUpdatedContainerId = -1;
		static int maxThreads = 8;
		static int maxSolids = 512;
		
		public bool needsUpdate = true;
		public bool forceUpdate = false;
		
		static ComputeBuffer solidsPosBuffer;
		static ComputeBuffer solidsRotBuffer;
		static ComputeBuffer solidsScaleBuffer;
		static ComputeBuffer solidsBlendBuffer;
		static ComputeBuffer solidsTypeBuffer;
		static ComputeBuffer solidsColorBuffer;
		static ComputeBuffer solidsAttrsBuffer;
		static ComputeBuffer solidsUpdatedBuffer;
		static ComputeBuffer solidsPerChunkBuffer;
		static ComputeBuffer updatingChunksBuffer;
		
		static List<Vector3> solidsPos;
		static List<Quaternion> solidsRot;
		static List<Vector3> solidsScale;
		static List<float> solidsBlend;
		static List<int> solidsType;
		static List<Vector3> solidsColor;
		static List<Vector4> solidsAttrs;
		static int[] updatingChunks = new int[64];

		Dictionary<int, int> solidsUpdatedDict = new Dictionary<int, int>();
		int[] solidsUpdated = new int[512];
		List<ClayxelChunk> chunks = new List<ClayxelChunk>();
		List<ComputeBuffer> compBuffers = new List<ComputeBuffer>();
		int chunkMaxOutPoints = (256*256*256) / 8;
		bool needsInit = true;
		bool invalidated = false;
		int[] countBufferArray = new int[1]{0};
		ComputeBuffer countBuffer;
		Vector3 boundsScale = new Vector3(0.0f, 0.0f, 0.0f);
		Vector3 boundsCenter = new Vector3(0.0f, 0.0f, 0.0f);
		Bounds renderBounds = new Bounds();
		Vector3[] vertices = new Vector3[1];
		int[] meshTopology = new int[1];
		bool solidsHierarchyNeedsScan = false;
		List<WeakReference> clayObjects = new List<WeakReference>();
		int numChunks = 0;
		float deltaTime = 0.0f;
		bool meshCached = false;
		Mesh mesh = null;
		int numThreadsComputeStartRes;
		int numThreadsComputeFullRes;
		float splatRadius = 0.0f;
		int clayxelId = -1;
		
		static string renderPipe = "";
		static RenderTexture pickingRenderTexture = null;
		static RenderTargetIdentifier pickingRenderTextureId;
		static CommandBuffer pickingCommandBuffer;
		static Texture2D pickingTextureResult;
		static Rect pickingRect;
		static int pickingMousePosX = -1;
		static int pickingMousePosY = -1;
		static int pickedSolidId = -1;
		static int pickedClayxelId = -1;
		static GameObject pickedObj = null;
		static bool pickingMode = false;
		static bool pickingShiftPressed = false;

		enum Kernels{
			computeGrid,
			generatePointCloud,
			debugDisplayGridPoints,
			genMesh,
			clearGrid,
			filterSolidsPerChunk
		}

		public int getMaxSolids(){
			return ClayContainer.maxSolids;
		}

		public void scanSolids(){
			this.clayObjects.Clear();

			this.scanRecursive(this.transform);
		}

		public int getNumClayObjects(){
			return  this.clayObjects.Count;
		}


		static public void initGlobalData(){
			if(!ClayContainer.globalDataNeedsInit){
				return;
			}

			string renderPipeAsset = "";
			if(GraphicsSettings.renderPipelineAsset != null){
				renderPipeAsset = GraphicsSettings.renderPipelineAsset.GetType().Name;
			}
			
			if(renderPipeAsset == "HDRenderPipelineAsset"){
				ClayContainer.renderPipe = "hdrp";
			}
			else if(renderPipeAsset == "UniversalRenderPipelineAsset"){
				ClayContainer.renderPipe = "urp";
			}
			else{
				ClayContainer.renderPipe = "builtin";
			}

			#if UNITY_EDITOR
				if(!Application.isPlaying){
					ClayContainer.setupScenePicking();
					ClayContainer.pickingMode = false;
					ClayContainer.pickedObj = null;
				}

				ClayContainer.reloadSolidsCatalogue();
			#endif

			ClayContainer.globalDataNeedsInit = false;

			ClayContainer.lastUpdatedContainerId = -1;

			ClayContainer.releaseGlobalBuffers();

			UnityEngine.Object clayCore = Resources.Load("clayCoreLock");
			if(clayCore == null){
				clayCore = Resources.Load("clayCore");
			}

			ClayContainer.claycoreCompute = (ComputeShader)Instantiate(clayCore);

			ClayContainer.gridDataBuffer = new ComputeBuffer(256 * 256 * 256, sizeof(float) * 3);
			ClayContainer.globalCompBuffers.Add(ClayContainer.gridDataBuffer);

			ClayContainer.prefilteredSolidIdsBuffer = new ComputeBuffer(64 * 64 * 64, sizeof(int) * 128);
			ClayContainer.globalCompBuffers.Add(ClayContainer.prefilteredSolidIdsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "prefilteredSolidIds", ClayContainer.prefilteredSolidIdsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.clearGrid, "prefilteredSolidIds", ClayContainer.prefilteredSolidIdsBuffer);
			
			ClayContainer.triangleConnectionTable = new ComputeBuffer(256 * 16, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.triangleConnectionTable);

			ClayContainer.triangleConnectionTable.SetData(MarchingCubesTables.TriangleConnectionTable);

			ClayContainer.claycoreCompute.SetFloat("surfaceBoundaryThreshold", 1.0f);

			int numKernels = Enum.GetNames(typeof(Kernels)).Length;
			for(int i = 0; i < numKernels; ++i){
				ClayContainer.claycoreCompute.SetBuffer(i, "gridData", ClayContainer.gridDataBuffer);
			}
			
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloud, "triangleConnectionTable", ClayContainer.triangleConnectionTable);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.genMesh, "triangleConnectionTable", ClayContainer.triangleConnectionTable);

			ClayContainer.solidsPosBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 3);
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsPosBuffer);
			ClayContainer.solidsRotBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 4);
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsRotBuffer);
			ClayContainer.solidsScaleBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 3);
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsScaleBuffer);
			ClayContainer.solidsBlendBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float));
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsBlendBuffer);
			ClayContainer.solidsTypeBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsTypeBuffer);
			ClayContainer.solidsColorBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 3);
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsColorBuffer);
			ClayContainer.solidsAttrsBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 4);
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsAttrsBuffer);
			
			ClayContainer.solidsPos = new List<Vector3>(new Vector3[ClayContainer.maxSolids]);
			ClayContainer.solidsRot = new List<Quaternion>(new Quaternion[ClayContainer.maxSolids]);
			ClayContainer.solidsScale = new List<Vector3>(new Vector3[ClayContainer.maxSolids]);
			ClayContainer.solidsBlend = new List<float>(new float[ClayContainer.maxSolids]);
			ClayContainer.solidsType = new List<int>(new int[ClayContainer.maxSolids]);
			ClayContainer.solidsColor = new List<Vector3>(new Vector3[ClayContainer.maxSolids]);
			ClayContainer.solidsAttrs = new List<Vector4>(new Vector4[ClayContainer.maxSolids]);

			ClayContainer.numSolidsPerChunkBuffer = new ComputeBuffer(64, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.numSolidsPerChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "numSolidsPerChunk", ClayContainer.numSolidsPerChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "numSolidsPerChunk", ClayContainer.numSolidsPerChunkBuffer);

			ClayContainer.solidsUpdatedBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsUpdatedBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "solidsUpdated", ClayContainer.solidsUpdatedBuffer);

			int maxChunks = 64;
			ClayContainer.solidsPerChunkBuffer = new ComputeBuffer(maxChunks, sizeof(int) * 512);
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsPerChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "solidsPerChunk", ClayContainer.solidsPerChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "solidsPerChunk", ClayContainer.solidsPerChunkBuffer);

			ClayContainer.updatingChunksBuffer = new ComputeBuffer(maxChunks, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.updatingChunksBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "updatingChunks", ClayContainer.updatingChunksBuffer);

			for(int i = 0; i < ClayContainer.updatingChunks.Length; ++i){
				ClayContainer.updatingChunks[i] = -1;
			}

			#if DRAW_DEBUG
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.debugDisplayGridPoints, "gridData", ClayContainer.gridDataBuffer);
			#endif
		}

		public void init(){
			#if UNITY_EDITOR
				if(!Application.isPlaying){
					this.reinstallEditorEvents();
				}
			#endif

			if(ClayContainer.globalDataNeedsInit){
				ClayContainer.initGlobalData();
			}

			this.needsInit = false;

			if(this.gameObject.GetComponent<MeshFilter>() != null){
				this.meshCached = true;
				this.releaseBuffers();
				return;
			}

			this.limitChunkValues();

			this.clayObjects.Clear();

			this.releaseBuffers();

			this.numThreadsComputeStartRes = 64 / ClayContainer.maxThreads;
			this.numThreadsComputeFullRes = 256 / ClayContainer.maxThreads;

			this.splatRadius = (((float)this.gridResolution / 256) * 0.5f) * 1.7f;

			this.initChunks();
			this.updateSplatLook();

			this.countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
			this.compBuffers.Add(this.countBuffer);

			this.solidsHierarchyNeedsScan = true;
			this.needsUpdate = true;
			ClayContainer.lastUpdatedContainerId = -1;

			this.updateChunksTransform();
		}

		public ClayObject addSolid(){
			GameObject clayObj = new GameObject("clay_cube+");
			clayObj.transform.parent = this.transform;
			clayObj.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

			ClayObject clayObjComp = clayObj.AddComponent<ClayObject>();
			clayObjComp.clayxelContainerRef = new WeakReference(this);
			clayObjComp.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);

			this.solidsHierarchyNeedsScan = true;

			return clayObjComp;
		}

		public ClayObject getClayObj(int id){
			return (ClayObject)this.clayObjects[id].Target;
		}

		public void scanSolidsHierarchy(){
			this.solidsHierarchyNeedsScan = true;
		}

		public void generateMesh(){
			this.meshCached = true;

			if(this.gameObject.GetComponent<MeshFilter>() == null){
				this.gameObject.AddComponent<MeshFilter>();
			}
			
			MeshRenderer render = this.gameObject.GetComponent<MeshRenderer>();
			if(render == null){
				render = this.gameObject.AddComponent<MeshRenderer>();

				if(ClayContainer.renderPipe == "hdrp"){
					render.material = new Material(Shader.Find("Shader Graphs/ClayxelHDRPMeshShader"));
				}
				else if(ClayContainer.renderPipe == "urp"){
					render.material = new Material(Shader.Find("Shader Graphs/ClayxelURPMeshShader"));
				}
				else{
					render.material = new Material(Shader.Find("Clayxels/ClayxelBuiltInMeshShader"));
				}
			}

			render.sharedMaterial.SetFloat("_Smoothness", this.materialSmoothness);
			render.sharedMaterial.SetFloat("_Metallic", this.materialMetallic);
			render.sharedMaterial.SetColor("_Emission", this.materialEmission);
			render.sharedMaterial.SetFloat("_EmissionIntensity", this.materialEmissionIntensity + 1.0f);

			ComputeBuffer meshIndicesBuffer = new ComputeBuffer(this.chunkMaxOutPoints * 6, sizeof(float) * 3, ComputeBufferType.Counter);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.genMesh, "meshOutIndices", meshIndicesBuffer);

			ComputeBuffer meshVertsBuffer = new ComputeBuffer(this.chunkMaxOutPoints, sizeof(float) * 3, ComputeBufferType.Counter);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.genMesh, "meshOutPoints", meshVertsBuffer);

			ComputeBuffer meshColorsBuffer = new ComputeBuffer(this.chunkMaxOutPoints, sizeof(float) * 4);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.genMesh, "meshOutColors", meshColorsBuffer);

			List<Vector3> totalVertices = new List<Vector3>();
			List<int> totalIndices = new List<int>();
			List<Color> totalColors = new List<Color>();

			int totalNumVerts = 0;

			this.mesh = new Mesh();
			this.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

			this.switchComputeData();

			this.updateSolids();

			ClayContainer.claycoreCompute.SetInt("numSolids", this.clayObjects.Count);
			ClayContainer.claycoreCompute.SetFloat("chunkSize", (float)this.gridResolution);

			ClayContainer.claycoreCompute.SetFloat("surfaceBoundaryThreshold", 2.0f);

			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				ClayxelChunk chunk = this.chunks[chunkIt];

				meshIndicesBuffer.SetCounterValue(0);
				meshVertsBuffer.SetCounterValue(0);

				ClayContainer.claycoreCompute.SetInt("chunkId", chunkIt);

				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.clearGrid, "indirectDrawArgs", chunk.indirectDrawArgsBuffer);
				ClayContainer.claycoreCompute.Dispatch((int)Kernels.clearGrid, this.numThreadsComputeStartRes, this.numThreadsComputeStartRes, this.numThreadsComputeStartRes);
				
				ClayContainer.claycoreCompute.SetVector("chunkCenter", chunk.center);
				ClayContainer.claycoreCompute.Dispatch((int)Kernels.computeGrid, this.numThreadsComputeStartRes, this.numThreadsComputeStartRes, this.numThreadsComputeStartRes);

				ClayContainer.claycoreCompute.SetInt("outMeshIndexOffset", totalNumVerts);
				ClayContainer.claycoreCompute.Dispatch((int)Kernels.genMesh, 64, 64, 64);

				int numVerts = this.getBufferCount(meshVertsBuffer);
				int numQuads = this.getBufferCount(meshIndicesBuffer) * 3;
				
				totalNumVerts += numVerts;
				
				Vector3[] vertices = new Vector3[numVerts];
				meshVertsBuffer.GetData(vertices);

				int[] indices = new int[numQuads];
				meshIndicesBuffer.GetData(indices);

				Color[] colors = new Color[numVerts];
				meshColorsBuffer.GetData(colors);

				totalVertices.AddRange(vertices);
				totalIndices.AddRange(indices);
				totalColors.AddRange(colors);
			}

			ClayContainer.claycoreCompute.SetFloat("surfaceBoundaryThreshold", 1.0f);

			mesh.vertices = totalVertices.ToArray();
			mesh.triangles = totalIndices.ToArray();
			mesh.colors = totalColors.ToArray();
			
			this.mesh.Optimize();

			this.mesh.RecalculateNormals();
			
			this.gameObject.GetComponent<MeshFilter>().mesh = this.mesh;

			meshIndicesBuffer.Release();
			meshVertsBuffer.Release();
			meshColorsBuffer.Release();

			this.releaseBuffers();
			this.needsInit = false;
		}

		public bool hasCachedMesh(){
			return this.meshCached;
		}

		public void disableMesh(){
			this.meshCached = false;
			this.needsInit = true;

			if(this.gameObject.GetComponent<MeshFilter>() != null){
				DestroyImmediate(this.gameObject.GetComponent<MeshFilter>());
			}
		}

		static void parseSolidsAttrs(string content, ref int lastParsed){
			string[] lines = content.Split(new[]{ "\r\n", "\r", "\n" }, StringSplitOptions.None);
			for(int i = 0; i < lines.Length; ++i){
				string line = lines[i];
				if(line.Contains("label: ")){
					if(line.Split('/').Length == 3){// if too many comment slashes, it's a commented out solid,
						lastParsed += 1;

						string[] parameters = line.Split(new[]{"label:"}, StringSplitOptions.None)[1].Split(',');
						string label = parameters[0].Trim();
						
						ClayContainer.solidsCatalogueLabels.Add(label);

						List<string[]> paramList = new List<string[]>();

						for(int paramIt = 1; paramIt < parameters.Length; ++paramIt){
							string param = parameters[paramIt];
							string[] attrs = param.Split(':');
							string paramId = attrs[0];
							string[] paramLabelValue = attrs[1].Split(' ');
							string paramLabel = paramLabelValue[1];
							string paramValue = paramLabelValue[2];

							paramList.Add(new string[]{paramId.Trim(), paramLabel.Trim(), paramValue.Trim()});
						}

						ClayContainer.solidsCatalogueParameters.Add(paramList);
					}
				}
			}
		}

		static public void reloadSolidsCatalogue(){
			ClayContainer.solidsCatalogueLabels.Clear();
			ClayContainer.solidsCatalogueParameters.Clear();

			int lastParsed = -1;
			try{
				string claySDF = ((TextAsset)Resources.Load("claySDF", typeof(TextAsset))).text;
				ClayContainer.parseSolidsAttrs(claySDF, ref lastParsed);
			}
			catch{
				Debug.Log("error trying to parse parameters in claySDF.compute, solid #" + lastParsed);
			}

			int numBuiltinSolids = lastParsed + 2;
			// try{
				string userConfig = ((TextAsset)Resources.Load("userClay", typeof(TextAsset))).text;

				ClayContainer.parseSolidsAttrs(userConfig, ref lastParsed);

				string numThreadsDef = "MAXTHREADS";
				ClayContainer.maxThreads = (int)char.GetNumericValue(userConfig[userConfig.IndexOf(numThreadsDef) + numThreadsDef.Length + 1]);
			// }
			// catch{
			// 	int customSolidAttr = lastParsed - numBuiltinSolids;
			// 	Debug.Log("error trying to parse parameters in userClay.compute, solid #" + customSolidAttr + " might have something wrong?");
			// }
		}

		public string[] getSolidsCatalogueLabels(){
			return ClayContainer.solidsCatalogueLabels.ToArray();
		}

		public List<string[]> getSolidsCatalogueParameters(int solidId){
			return ClayContainer.solidsCatalogueParameters[solidId];
		}

		public void updateSplatLook(){
			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				ClayxelChunk chunk = this.chunks[chunkIt];
				if(customMaterial == null)
				{
					chunk.clayxelMaterial.SetTexture("_MainTex", this.splatTexture);
				}
				else
				{
					splatTexture = (Texture2D)chunk.clayxelMaterial.GetTexture("_MainTex");
				}

				if(this.splatTexture != null){
					chunk.clayxelMaterial.EnableKeyword("SPLATTEXTURE_ON");
					chunk.clayxelMaterial.DisableKeyword("SPLATTEXTURE_OFF");
				}
				else{
					chunk.clayxelMaterial.DisableKeyword("SPLATTEXTURE_ON");
					chunk.clayxelMaterial.EnableKeyword("SPLATTEXTURE_OFF");
				}
			}
		}

		void Awake(){
			ClayContainer.globalDataNeedsInit = true;
			this.needsInit = true;
		}

		void OnDestroy(){
			this.invalidated = true;

			this.releaseBuffers();

			if(UnityEngine.Object.FindObjectsOfType<ClayContainer>().Length == 0){
				ClayContainer.releaseGlobalBuffers();
			}

			#if UNITY_EDITOR
				if(!Application.isPlaying){
					this.removeEditorEvents();
				}
			#endif
		}

		void releaseBuffers(){
			for(int i = 0; i < this.compBuffers.Count; ++i){
				this.compBuffers[i].Release();
			}

			this.compBuffers.Clear();
		}

		static void releaseGlobalBuffers(){
			for(int i = 0; i < ClayContainer.globalCompBuffers.Count; ++i){
				ClayContainer.globalCompBuffers[i].Release();
			}

			ClayContainer.globalCompBuffers.Clear();
		}

		void limitChunkValues(){
			if(this.gridSizeX > 4){
				this.gridSizeX = 4;
			}
			if(this.gridSizeY > 4){
				this.gridSizeY = 4;
			}
			if(this.gridSizeZ > 4){
				this.gridSizeZ = 4;
			}
			if(this.gridSizeX < 1){
				this.gridSizeX = 1;
			}
			if(this.gridSizeY < 1){
				this.gridSizeY = 1;
			}
			if(this.gridSizeZ < 1){
				this.gridSizeZ = 1;
			}

			if(this.gridResolution < 1){
				this.gridResolution = 1;
			}
		}

		void initChunks(){
			this.numChunks = 0;
			this.chunks.Clear();

			this.boundsScale.x = (float)this.gridResolution * this.gridSizeX;
			this.boundsScale.y = (float)this.gridResolution * this.gridSizeY;
			this.boundsScale.z = (float)this.gridResolution * this.gridSizeZ;

			float gridCenterOffset = (this.gridResolution * 0.5f);
			this.boundsCenter.x = ((this.gridResolution * (this.gridSizeX - 1)) * 0.5f) - (gridCenterOffset*(this.gridSizeX-1));
			this.boundsCenter.y = ((this.gridResolution * (this.gridSizeY - 1)) * 0.5f) - (gridCenterOffset*(this.gridSizeY-1));
			this.boundsCenter.z = ((this.gridResolution * (this.gridSizeZ - 1)) * 0.5f) - (gridCenterOffset*(this.gridSizeZ-1));

			for(int z = 0; z < this.gridSizeZ; ++z){
				for(int y = 0; y < this.gridSizeY; ++y){
					for(int x = 0; x < this.gridSizeX; ++x){
						this.initNewChunk(x, y, z);
						this.numChunks += 1;
					}
				}
			}
		}

		void initNewChunk(int x, int y, int z){
			ClayxelChunk chunk = new ClayxelChunk();
			this.chunks.Add(chunk);

			float seamOffset = this.gridResolution / 256.0f; // removes the seam between chunks
			float chunkOffset = this.gridResolution - seamOffset;
			float gridCenterOffset = (this.gridResolution * 0.5f);
			chunk.center = new Vector3(
				(-((this.gridResolution * this.gridSizeX) * 0.5f) + gridCenterOffset) + (chunkOffset * x),
				(-((this.gridResolution * this.gridSizeY) * 0.5f) + gridCenterOffset) + (chunkOffset * y),
				(-((this.gridResolution * this.gridSizeZ) * 0.5f) + gridCenterOffset) + (chunkOffset * z));

			chunk.pointCloudDataBuffer = new ComputeBuffer(this.chunkMaxOutPoints, sizeof(int) * 4);
			this.compBuffers.Add(chunk.pointCloudDataBuffer);

			chunk.indirectDrawArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
			this.compBuffers.Add(chunk.indirectDrawArgsBuffer);

			chunk.indirectDrawArgsBuffer.SetData(new int[]{0, 1, 0, 0});

			if(this.customMaterial != null){
				chunk.clayxelMaterial = new Material(this.customMaterial);
				
				if(ClayContainer.renderPipe == "hdrp"){
					chunk.clayxelMaterial.hideFlags = HideFlags.HideAndDontSave;// required in hdrp
				}
			}
			else{
				if(ClayContainer.renderPipe == "hdrp"){
					chunk.clayxelMaterial = new Material(Shader.Find("Clayxels/ClayxelHDRPShader"));
					chunk.clayxelMaterial.hideFlags = HideFlags.HideAndDontSave;// required in hdrp
				}
				else if(ClayContainer.renderPipe == "urp"){
					chunk.clayxelMaterial = new Material(Shader.Find("Clayxels/ClayxelURPShader"));
				}
				else{
					chunk.clayxelMaterial = new Material(Shader.Find("Clayxels/ClayxelBuiltInShader"));
				}
			}

			chunk.clayxelMaterial.SetBuffer("chunkPoints", chunk.pointCloudDataBuffer);

			chunk.clayxelPickingMaterial = new Material(Shader.Find("Clayxels/ClayxelPickingShader"));
			chunk.clayxelPickingMaterial.SetBuffer("chunkPoints", chunk.pointCloudDataBuffer);

			// #if DRAW_DEBUG
			// 	chunk.clayxelMaterial = new Material(Shader.Find("ClayContainer/ClayxelDebugShader"));

			// 	chunk.debugGridOutPointsBuffer = new ComputeBuffer(this.chunkMaxOutPoints, sizeof(float) * 3, ComputeBufferType.Counter);
			// 	this.compBuffers.Add(chunk.debugGridOutPointsBuffer);

			// 	chunk.clayxelMaterial.SetBuffer("debugChunkPoints", chunk.debugGridOutPointsBuffer);
			// #endif

			chunk.clayxelMaterial.SetFloat("splatRadius", this.splatRadius);
			chunk.clayxelMaterial.SetFloat("chunkSize", (float)this.gridResolution);
			chunk.clayxelMaterial.SetVector("chunkCenter",  chunk.center);
			chunk.clayxelMaterial.SetInt("solidHighlightId", -1);

			chunk.clayxelPickingMaterial.SetFloat("chunkSize", (float)this.gridResolution);
			chunk.clayxelPickingMaterial.SetVector("chunkCenter",  chunk.center);
			chunk.clayxelPickingMaterial.SetFloat("splatRadius",  this.splatRadius);
		}

		void scanRecursive(Transform trn){
			if(this.clayObjects.Count == ClayContainer.maxSolids){
				return;
			}

			ClayObject clayObj = trn.gameObject.GetComponent<ClayObject>();
			if(clayObj != null){
				if(clayObj.isValid() && trn.gameObject.activeSelf){
					clayObj._solidId = this.clayObjects.Count;
					this.clayObjects.Add(new WeakReference(clayObj));
					clayObj.transform.hasChanged = true;
					this.solidsUpdatedDict[clayObj._solidId] = 1;
					clayObj.setClayxelContainer(this);
				}
			}

			for(int i = 0; i < trn.childCount; ++i){
				this.scanRecursive(trn.GetChild(i));
			}
		}

		int getBufferCount(ComputeBuffer buffer){
			ComputeBuffer.CopyCount(buffer, this.countBuffer, 0);
			this.countBuffer.GetData(this.countBufferArray);
			int count = this.countBufferArray[0];

			return count;
		}

		#if DRAW_DEBUG
		void debugGridPoints(ClayxelChunk chunk){
			chunk.debugGridOutPointsBuffer.SetCounterValue(0);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.debugDisplayGridPoints, "debugGridOutPoints", chunk.debugGridOutPointsBuffer);
			ClayContainer.claycoreCompute.Dispatch((int)Kernels.debugDisplayGridPoints, this.numThreadsComputeFullRes, this.numThreadsComputeFullRes, this.numThreadsComputeFullRes);
		}
		#endif

		void updateChunk(int chunkId){
			if(ClayContainer.updatingChunks[chunkId] < 0){
				return;
			}
			
			ClayxelChunk chunk = this.chunks[chunkId];

			ClayContainer.claycoreCompute.SetInt("chunkId", chunkId);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.clearGrid, "indirectDrawArgs", chunk.indirectDrawArgsBuffer);
			ClayContainer.claycoreCompute.Dispatch((int)Kernels.clearGrid, this.numThreadsComputeStartRes, this.numThreadsComputeStartRes, this.numThreadsComputeStartRes);
			
			ClayContainer.claycoreCompute.SetVector("chunkCenter", chunk.center);
			ClayContainer.claycoreCompute.Dispatch((int)Kernels.computeGrid, this.numThreadsComputeStartRes, this.numThreadsComputeStartRes, this.numThreadsComputeStartRes);
			
			#if DRAW_DEBUG
			this.debugGridPoints(chunk);
			return;
			#endif

			// generate point cloud
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloud, "indirectDrawArgs", chunk.indirectDrawArgsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloud, "pointCloudData", chunk.pointCloudDataBuffer);
			ClayContainer.claycoreCompute.Dispatch((int)Kernels.generatePointCloud, this.numThreadsComputeFullRes, this.numThreadsComputeFullRes, this.numThreadsComputeFullRes);

			// set material params
			if(customMaterial == null) {
				chunk.clayxelMaterial.SetFloat("_Smoothness", this.materialSmoothness);
				chunk.clayxelMaterial.SetFloat("_Metallic", this.materialMetallic);
				chunk.clayxelMaterial.SetColor("_Emission", this.materialEmission);
				chunk.clayxelMaterial.SetFloat("_EmissionIntensity", this.materialEmissionIntensity + 1.0f);
			}
			chunk.clayxelMaterial.SetFloat("normalOrientedSplat", this.normalOrientedSplat);
			chunk.clayxelMaterial.SetFloat("splatSizeMult", this.splatSizeMultiplier);
		}

		public void forceUpdateAllChunks(){
			for(int i = 0; i < this.clayObjects.Count; ++i){
				ClayObject clayObj = (ClayObject)this.clayObjects[i].Target;
				this.solidsUpdatedDict[clayObj._solidId] = 1;
			}
		}

		public void clayObjectUpdated(ClayObject clayObj){
			if(!this.transform.hasChanged){
				this.solidsUpdatedDict[clayObj._solidId] = 1;
				
				this.needsUpdate = true;
			}
		}

		void updateSolids(){
			Matrix4x4 thisMatInv = this.transform.worldToLocalMatrix;

			float minNegativeBlend = (this.splatRadius * 2.0f);

			for(int i = 0; i < this.clayObjects.Count; ++i){
				ClayObject clayObj = (ClayObject)this.clayObjects[i].Target;
				Matrix4x4 clayObjMat = thisMatInv * clayObj.transform.localToWorldMatrix;

				float blend = clayObj.blend;
				if(blend < 0.0f){
					blend = blend - minNegativeBlend;
				}
				
				ClayContainer.solidsPos[i] = (Vector3)clayObjMat.GetColumn(3);
				ClayContainer.solidsRot[i] = clayObjMat.rotation;
				ClayContainer.solidsScale[i] = clayObj.transform.localScale*0.5f;
				ClayContainer.solidsBlend[i] = blend;
				ClayContainer.solidsType[i] = clayObj.primitiveType;
				ClayContainer.solidsColor[i] = new Vector3(clayObj.color.r, clayObj.color.g, clayObj.color.b);
				ClayContainer.solidsAttrs[i] = clayObj.attrs;
			}

			ClayContainer.claycoreCompute.SetInt("numSolids", this.clayObjects.Count);
			ClayContainer.claycoreCompute.SetFloat("chunkSize", (float)this.gridResolution);

			this.solidsUpdated = this.solidsUpdatedDict.Keys.ToArray();

			ClayContainer.claycoreCompute.SetInt("numSolidsUpdated", this.solidsUpdated.Length);
			ClayContainer.solidsUpdatedBuffer.SetData(this.solidsUpdated);
			
			if(this.clayObjects.Count > 0){
				ClayContainer.solidsPosBuffer.SetData(ClayContainer.solidsPos);
				ClayContainer.solidsRotBuffer.SetData(ClayContainer.solidsRot);
				ClayContainer.solidsScaleBuffer.SetData(ClayContainer.solidsScale);
				ClayContainer.solidsBlendBuffer.SetData(ClayContainer.solidsBlend);
				ClayContainer.solidsTypeBuffer.SetData(ClayContainer.solidsType);
				ClayContainer.solidsColorBuffer.SetData(ClayContainer.solidsColor);
				ClayContainer.solidsAttrsBuffer.SetData(ClayContainer.solidsAttrs);
			}
			
			ClayContainer.claycoreCompute.Dispatch((int)Kernels.filterSolidsPerChunk, this.gridSizeX, this.gridSizeY, this.gridSizeZ);
			ClayContainer.updatingChunksBuffer.GetData(ClayContainer.updatingChunks);
			
			this.solidsUpdatedDict.Clear();
		}

		void logFPS(){
			this.deltaTime += (Time.unscaledDeltaTime - this.deltaTime) * 0.1f;
			float fps = 1.0f / this.deltaTime;
			Debug.Log(fps);
		}

		void switchComputeData(){
			ClayContainer.lastUpdatedContainerId = this.GetInstanceID();

			int numKernels = Enum.GetNames(typeof(Kernels)).Length;
			for(int i = 0; i < numKernels; ++i){
				ClayContainer.claycoreCompute.SetBuffer(i, "solidsPos", ClayContainer.solidsPosBuffer);
				ClayContainer.claycoreCompute.SetBuffer(i, "solidsRot", ClayContainer.solidsRotBuffer);
				ClayContainer.claycoreCompute.SetBuffer(i, "solidsScale", ClayContainer.solidsScaleBuffer);
				ClayContainer.claycoreCompute.SetBuffer(i, "solidsBlend", ClayContainer.solidsBlendBuffer);
				ClayContainer.claycoreCompute.SetBuffer(i, "solidsType", ClayContainer.solidsTypeBuffer);
				ClayContainer.claycoreCompute.SetBuffer(i, "solidsColor", ClayContainer.solidsColorBuffer);
				ClayContainer.claycoreCompute.SetBuffer(i, "solidsAttrs", ClayContainer.solidsAttrsBuffer);
			}

			ClayContainer.claycoreCompute.SetFloat("globalRoundCornerValue", this.splatRadius * 2.0f);

			ClayContainer.claycoreCompute.SetInt("numChunksX", this.gridSizeX);
			ClayContainer.claycoreCompute.SetInt("numChunksY", this.gridSizeY);
			ClayContainer.claycoreCompute.SetInt("numChunksZ", this.gridSizeZ);
		}

		void updateChunksTransform(){
			Vector3 scale = this.transform.localScale;
			float splatScale = (scale.x + scale.y + scale.z) / 3.0f;

			// clayxels are computed at the center of the world, so we need to transform them back when this transform is updated
			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				ClayxelChunk chunk = this.chunks[chunkIt];

				chunk.clayxelMaterial.SetMatrix("objectMatrix", this.transform.localToWorldMatrix);
				chunk.clayxelMaterial.SetFloat("splatRadius", this.splatRadius * splatScale);

				chunk.clayxelPickingMaterial.SetMatrix("objectMatrix", this.transform.localToWorldMatrix);
				chunk.clayxelPickingMaterial.SetFloat("splatRadius",  this.splatRadius * splatScale);
			}
		}

		void Update(){
			if(this.meshCached){
				return;
			}

			if(this.invalidated){
				return;
			}

			if(this.needsInit){
				this.init();
			}
			else{
				// inhibit updates if this transform is the trigger
				if(this.transform.hasChanged){
					this.needsUpdate = false;
					this.transform.hasChanged = false;

					// if this transform moved and also one of the solids moved, then we still need to update
					if(this.forceUpdate){
						this.needsUpdate = true;
					}

					this.updateChunksTransform();
				}
			}

			if(!this.needsUpdate){
				this.drawClayxels();
				return;
			}
			
			this.needsUpdate = false;
			
			if(this.solidsHierarchyNeedsScan){
				this.scanSolids();
				this.solidsHierarchyNeedsScan = false;
			}
			
			if(ClayContainer.lastUpdatedContainerId != this.GetInstanceID()){
				this.switchComputeData();
			}

			this.updateSolids();
			
			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				this.updateChunk(chunkIt);
			}

			this.drawClayxels();

			#if CLAYXELS_INDIRECTDRAW
			#else
			// this dummy getData fixes a driver error on some lower end GPUS
			this.countBuffer.GetData(this.countBufferArray);
			#endif
		}

		void drawClayxels(){
			if(this.needsInit){
				return;
			}

			this.renderBounds.center = this.transform.position;
			this.renderBounds.size = this.boundsScale;

			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				ClayxelChunk chunk = this.chunks[chunkIt];

				// #if DRAW_DEBUG 
				// 	int pnts = this.getBufferCount(chunk.debugGridOutPointsBuffer);
				// 	Graphics.DrawProcedural(chunk.clayxelMaterial, 
				// 		this.renderBounds,
				// 		MeshTopology.Points, pnts, 1);
				// 	return;

				if(ClayContainer.pickedClayxelId == this.clayxelId){
					chunk.clayxelMaterial.SetInt("solidHighlightId", ClayContainer.pickedSolidId);
				}
				else{
					chunk.clayxelMaterial.SetInt("solidHighlightId", -1);
				}
				
				Graphics.DrawProceduralIndirect(chunk.clayxelMaterial, 
					this.renderBounds,
					MeshTopology.Triangles, chunk.indirectDrawArgsBuffer, 0,
					null, null,
					ShadowCastingMode.TwoSided, true, this.gameObject.layer);
			}
		}

		#if UNITY_EDITOR
		[MenuItem("GameObject/3D Object/Clayxel Container" )]
		public static ClayContainer createNewContainer(){
			 GameObject newObj = new GameObject("ClayxelContainer");
			 ClayContainer newClayContainer = newObj.AddComponent<ClayContainer>();

			 UnityEditor.Selection.objects = new GameObject[]{newObj};

			 return newClayContainer;
		}

		bool editingThisContainer = false;

		void OnValidate(){
			// called when editor value on this object is changed
			this.numChunks = 0;
		}

		void removeEditorEvents(){
			AssemblyReloadEvents.beforeAssemblyReload -= this.onBeforeAssemblyReload;

			EditorApplication.hierarchyChanged -= this.onSolidsHierarchyOrderChanged;

			UnityEditor.Selection.selectionChanged -= this.onSelectionChanged;

			UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= this.onSceneSaved;

			Undo.undoRedoPerformed -= this.onUndoPerformed;

			EditorApplication.update -= ClayContainer.onUnityEditorUpdate;
		}

		void reinstallEditorEvents(){
			this.removeEditorEvents();

			AssemblyReloadEvents.beforeAssemblyReload += this.onBeforeAssemblyReload;

			EditorApplication.hierarchyChanged += this.onSolidsHierarchyOrderChanged;

			UnityEditor.Selection.selectionChanged += this.onSelectionChanged;

			UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += this.onSceneSaved;

			Undo.undoRedoPerformed += this.onUndoPerformed;

			EditorApplication.update -= ClayContainer.onUnityEditorUpdate;
			EditorApplication.update += ClayContainer.onUnityEditorUpdate;
		}

		static bool _appFocused = true;
		static void onUnityEditorUpdate(){
			if(!ClayContainer._appFocused && UnityEditorInternal.InternalEditorUtility.isApplicationActive){
				ClayContainer._appFocused = UnityEditorInternal.InternalEditorUtility.isApplicationActive;
				ClayContainer.reloadAll();
			}
			else if (ClayContainer._appFocused && !UnityEditorInternal.InternalEditorUtility.isApplicationActive){
				ClayContainer._appFocused = UnityEditorInternal.InternalEditorUtility.isApplicationActive;
			}
		}

		void onBeforeAssemblyReload(){
			// called when this script recompiles

			if(Application.isPlaying){
				return;
			}

			this.releaseBuffers();
			ClayContainer.releaseGlobalBuffers();

			ClayContainer.globalDataNeedsInit = true;
			this.needsInit = true;
		}

		void onSceneSaved(UnityEngine.SceneManagement.Scene scene){
			// saving a scene will break some of the stored data, we need to reinit
			this.needsInit = true;
		}

		void onUndoPerformed(){
			if(Undo.GetCurrentGroupName() == "changed clayobject" ||
				Undo.GetCurrentGroupName() == "changed clayxel container"){
				this.needsUpdate = true;
			}
			else if(Undo.GetCurrentGroupName() == "changed clayxel grid"){
				this.init();
			}
			else if(Undo.GetCurrentGroupName() == "added clayxel solid"){
				this.needsUpdate = true;
			}
			else if(Undo.GetCurrentGroupName() == "Selection Change"){
				if(UnityEditor.Selection.Contains(this.gameObject)){
					this.init();
				}
				else{
					if(UnityEditor.Selection.gameObjects.Length > 0){
						ClayObject clayObj = UnityEditor.Selection.gameObjects[0].GetComponent<ClayObject>();
						if(clayObj != null){
							if(clayObj.getClayxelContainer() == this){
								this.needsUpdate = true;
							}
						}
					}
				}
			}
		}

		void onSolidsHierarchyOrderChanged(){
			if(this.meshCached){
				return;
			}

			if(this.invalidated){
				// scene is being cleared
				return;
			}
			
			this.solidsHierarchyNeedsScan = true;
			this.needsUpdate = true;
			this.onSelectionChanged();
			
			UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
			ClayContainer.getSceneView().Repaint();
			#if DEBUG_CLAYXEL_REPAINT_WARN
			Debug.Log("onSolidsHierarchyOrderChanged!");
			#endif
		}

		void onSelectionChanged(){
			if(this.invalidated){
				return;
			}

			if(this.meshCached){
				return;
			}

			this.editingThisContainer = false;
			if(UnityEditor.Selection.Contains(this.gameObject)){
				// check if this container got selected
				this.editingThisContainer = true;
			}

			if(!this.editingThisContainer){
				// check if one of thye clayObjs in container has been selected
				for(int i = 0; i < this.clayObjects.Count; ++i){
					ClayObject clayObj = (ClayObject)this.clayObjects[i].Target;
					if(clayObj != null){
						if(UnityEditor.Selection.Contains(clayObj.gameObject)){
							this.editingThisContainer = true;
							return;
						}
					}
				}
			}

			if(ClayContainer.lastUpdatedContainerId != this.GetInstanceID()){
				this.switchComputeData();
			}
		}

		static void setupScenePicking(){
			SceneView sceneView = (SceneView)SceneView.sceneViews[0];
			SceneView.duringSceneGui -= ClayContainer.onSceneGUI;
			SceneView.duringSceneGui += ClayContainer.onSceneGUI;

			ClayContainer.pickingCommandBuffer = new CommandBuffer();
			
			ClayContainer.pickingTextureResult = new Texture2D(1, 1, TextureFormat.ARGB32, false);

			ClayContainer.pickingRect = new Rect(0, 0, 1, 1);

			if(ClayContainer.pickingRenderTexture != null){
				ClayContainer.pickingRenderTexture.Release();
				ClayContainer.pickingRenderTexture = null;
			}

			ClayContainer.pickingRenderTexture = new RenderTexture(1024, 768, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			ClayContainer.pickingRenderTexture.Create();
			ClayContainer.pickingRenderTextureId = new RenderTargetIdentifier(ClayContainer.pickingRenderTexture);
		}

		public static void startPicking(){
			ClayContainer.pickingMode = true;
			ClayContainer.pickedObj = null;

			ClayContainer.getSceneView().Repaint();
		}

		static void clearPicking(){
			ClayContainer.pickingMode = false;
			ClayContainer.pickedObj = null;
			ClayContainer.pickedClayxelId = -1;
			ClayContainer.pickedSolidId = -1;

			UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
		}

		static void onSceneGUI(SceneView sceneView){
			if(Application.isPlaying){
				return;
			}

			if(!UnityEditorInternal.InternalEditorUtility.isApplicationActive){
				// this callback keeps running even in the background!
				return;
			}

			Event ev = Event.current;

			if(ev.isKey){
				if(ev.keyCode == KeyCode.P){
					ClayContainer.startPicking();
				}

				return;
			}
			
			if(!ClayContainer.pickingMode){
				return;
			}
			
			// if(ev.type == EventType.MouseLeaveWindow){
			// 	ClayContainer.clearPicking();
			// 	return;
			// }
			
			if(ClayContainer.pickedObj != null){
				if(ClayContainer.pickingShiftPressed){
					List<UnityEngine.Object> sel = new List<UnityEngine.Object>();
	   			for(int i = 0; i < UnityEditor.Selection.objects.Length; ++i){
	   				sel.Add(UnityEditor.Selection.objects[i]);
	   			}
	   			sel.Add(ClayContainer.pickedObj);
	   			UnityEditor.Selection.objects = sel.ToArray();
	   		}
	   		else{
					UnityEditor.Selection.objects = new GameObject[]{ClayContainer.pickedObj};
				}
			}
			
			if(ev.type == EventType.MouseMove){
				ClayContainer.pickingMousePosX = (int)ev.mousePosition.x;
				ClayContainer.pickingMousePosY = (int)ev.mousePosition.y;
				
				if(ClayContainer.pickedObj != null){
					ClayContainer.clearPicking();
				}
			}
			else if(ev.type == EventType.MouseDown && !ev.alt){
				if(ClayContainer.pickingMousePosX < 0 || ClayContainer.pickingMousePosX >= sceneView.camera.pixelWidth || 
					ClayContainer.pickingMousePosY < 0 || ClayContainer.pickingMousePosY >= sceneView.camera.pixelHeight){
					ClayContainer.clearPicking();
					return;
				}

				ev.Use();

				if(ClayContainer.pickedClayxelId > -1 && ClayContainer.pickedSolidId > -1){
					ClayContainer[] clayxels = UnityEngine.Object.FindObjectsOfType<ClayContainer>();
					GameObject newSel = clayxels[ClayContainer.pickedClayxelId].getClayObj(ClayContainer.pickedSolidId).gameObject;
					UnityEditor.Selection.objects = new GameObject[]{newSel};

					ClayContainer.pickedObj = newSel;
					ClayContainer.pickingShiftPressed = ev.shift;
				}
				else{
					ClayContainer.clearPicking();
				}
			}
			else if((int)ev.type == 7){ // on repaint
				if(ClayContainer.pickingMousePosX < 0 || ClayContainer.pickingMousePosX >= sceneView.camera.pixelWidth || 
					ClayContainer.pickingMousePosY < 0 || ClayContainer.pickingMousePosY >= sceneView.camera.pixelHeight){
					return;
				}

				ClayContainer.pickedSolidId = -1;
		  		ClayContainer.pickedClayxelId = -1;

				ClayContainer.pickingCommandBuffer.Clear();
				ClayContainer.pickingCommandBuffer.SetRenderTarget(ClayContainer.pickingRenderTextureId);
				ClayContainer.pickingCommandBuffer.ClearRenderTarget(true, true, Color.black, 1.0f);

				ClayContainer[] clayxels = UnityEngine.Object.FindObjectsOfType<ClayContainer>();

				for(int i = 0; i < clayxels.Length; ++i){
					clayxels[i].drawClayxelPicking(i, ClayContainer.pickingCommandBuffer);
				}

				Graphics.ExecuteCommandBuffer(ClayContainer.pickingCommandBuffer);
				
				ClayContainer.pickingRect.Set(
					(int)(1024.0f * ((float)ClayContainer.pickingMousePosX / (float)sceneView.camera.pixelWidth)), 
					(int)(768.0f * ((float)ClayContainer.pickingMousePosY / (float)sceneView.camera.pixelHeight)), 
					1, 1);

				RenderTexture oldRT = RenderTexture.active;
				RenderTexture.active = ClayContainer.pickingRenderTexture;
				ClayContainer.pickingTextureResult.ReadPixels(ClayContainer.pickingRect, 0, 0);
				ClayContainer.pickingTextureResult.Apply();
				RenderTexture.active = oldRT;
				
				Color pickCol = ClayContainer.pickingTextureResult.GetPixel(0, 0);
				
				int pickId = (int)((pickCol.r + pickCol.g * 255.0f + pickCol.b * 255.0f) * 255.0f);
		  		ClayContainer.pickedSolidId = pickId - 1;
		  		ClayContainer.pickedClayxelId = (int)(pickCol.a * 255.0f) - 1;
			}

			ClayContainer.getSceneView().Repaint();
		}

		void drawClayxelPicking(int clayxelId, CommandBuffer pickingCommandBuffer){
			if(this.needsInit){
				return;
			}

			this.clayxelId = clayxelId;

			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				ClayxelChunk chunk = this.chunks[chunkIt];

				chunk.clayxelPickingMaterial.SetMatrix("objectMatrix", this.transform.localToWorldMatrix);
				chunk.clayxelPickingMaterial.SetInt("clayxelId", clayxelId);

				pickingCommandBuffer.DrawProceduralIndirect(Matrix4x4.identity, chunk.clayxelPickingMaterial, -1, 
					MeshTopology.Triangles, chunk.indirectDrawArgsBuffer);
			}
		}

		void OnDrawGizmos(){
			if(Application.isPlaying){
				return;
			}

			if(!this.editingThisContainer){
				return;
			}

			Gizmos.color = new Color(0.5f, 0.5f, 1.0f, 0.1f);
			Gizmos.matrix = this.transform.localToWorldMatrix;
			Gizmos.DrawWireCube(this.boundsCenter, this.boundsScale);

			// debug chunks
			// Vector3 boundsScale2 = new Vector3(this.gridResolution, this.gridResolution, this.gridResolution);
			// for(int i = 0; i < this.numChunks; ++i){
			// 	Gizmos.DrawWireCube(this.chunks[i].center, boundsScale2);
			// }
		}

		static public void reloadAll(){
			ClayContainer.globalDataNeedsInit = true;

			ClayContainer[] clayxelObjs = UnityEngine.Object.FindObjectsOfType<ClayContainer>();
			for(int i = 0; i < clayxelObjs.Length; ++i){
				clayxelObjs[i].init();
			}
			
			UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
			((SceneView)SceneView.sceneViews[0]).Repaint();
		}

		public static SceneView getSceneView(){
			return (SceneView)SceneView.sceneViews[0];
		}

		#if CLAYXELS_RETOPO
			public void retopoMesh(){
				this.generateMesh();
				
				ModelExporter.ExportObject(Path.Combine(Application.dataPath, "tmp.fbx"), this);

				UnityEngine.Object[] data = AssetDatabase.LoadAllAssetsAtPath("Assets/tmp.fbx");
				for(int i = 0; i < data.Length; ++i){
					if(data[i].GetType() == typeof(Mesh)){
						this.mesh = (Mesh)data[i];
						this.gameObject.GetComponent<MeshFilter>().mesh = this.mesh;

						break;
					}
				}
				RetopoUtils.retopoMesh(this.mesh, this.retopoMaxVerts, -1);

				if(this.retopoFbxFile != ""){
					ModelExporter.ExportObject(Path.Combine(Application.dataPath, this.retopoFbxFile + ".fbx"), this);
				}
			}
			#else
			public void retopoMesh(){
				Debug.Log("please install the FBX export package, then define CLAYXELS_RETOPO in your project.");
			}
		#endif

		#endif// end if UNITY_EDITOR
	}
}
