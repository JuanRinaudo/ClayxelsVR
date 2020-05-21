using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Clayxels;

namespace Clayxels{
	[ExecuteInEditMode]
	public class ClayObject : MonoBehaviour{
		public float blend = 0.0f;
		public Color color;
		public Vector4 attrs = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
		public WeakReference clayxelContainerRef = null;
		public int primitiveType = 0;

		private float updateTimer = 0;
		public float steppedUpdate = 0;
		
		public int _solidId = -1; // internal use

		bool invalidated = false;
		Color gizmoColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
		
		void Awake(){
			this.cacheClayxelContainer();
		}

		void Update(){
			if(steppedUpdate != 0)
			{
				updateTimer += Time.deltaTime;
			}

			if(this.transform.hasChanged){
				if(steppedUpdate == 0 || (updateTimer > steppedUpdate))
				{
					this.transform.hasChanged = false;
					ClayContainer clayxel = this.getClayxelContainer();
					clayxel.clayObjectUpdated(this);
					updateTimer = 0;
				}
			}
		}
		
		void OnDestroy(){
			this.invalidated = true;
			
			ClayContainer clayxel = this.getClayxelContainer();
			if(clayxel != null){
				clayxel.scanSolidsHierarchy();
			}
		}

		public bool isValid(){
			return !this.invalidated;
		}

		public ClayContainer getClayxelContainer(){
			if(this.clayxelContainerRef != null){
				return (ClayContainer)this.clayxelContainerRef.Target;
			}

			this.cacheClayxelContainer();

			return (ClayContainer)this.clayxelContainerRef.Target;
		}

		public void setClayxelContainer(ClayContainer container){
			this.clayxelContainerRef = new WeakReference(container);
		}

		void cacheClayxelContainer(){
			this.clayxelContainerRef = null;
			GameObject parent = this.transform.parent.gameObject;

			ClayContainer clayxel = null;
			for(int i = 0; i < 100; ++i){
				clayxel = parent.GetComponent<ClayContainer>();
				if(clayxel != null){
					break;
				}
				else{
					parent = parent.transform.parent.gameObject;
				}
			}

			if(clayxel == null){
				Debug.Log("failed to find parent clayxel container");
			}
			else{
				this.clayxelContainerRef = new WeakReference(clayxel);
				clayxel.scanSolidsHierarchy();
			}
		}

		public void setPrimitiveType(int primType){
			this.primitiveType = primType;
		}

		public Color getColor(){
			return this.color;
		}

		#if UNITY_EDITOR
		void OnDrawGizmos(){
			if(this.blend < 0.0f || // negative shape?
				(((int)this.attrs.w >> 0)&1) == 1){// painter?

				if(UnityEditor.Selection.Contains(this.gameObject)){// if selected draw wire cage
					Gizmos.color = this.gizmoColor;
					if(this.primitiveType == 0){
						Gizmos.matrix = this.transform.localToWorldMatrix;
						Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
					}
					else if(this.primitiveType == 1){
						Gizmos.matrix = this.transform.localToWorldMatrix;
						Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
					}
					else if(this.primitiveType == 2){
						this.drawCylinder();
					}
					else if(this.primitiveType == 3){
						this.drawTorus();
					}
					else if(this.primitiveType == 4){
						this.drawCurve();
					}
				}
			}
		}

		void drawCurve(){
			Handles.color = Color.white;
			
			float radius = this.attrs.z * 0.5f;
			Vector3 heightVec = (this.transform.up * (this.transform.localScale.y - this.attrs.z)) * 0.5f;
			Vector3 sideVec = this.transform.right * ((this.transform.localScale.x*0.5f) - radius);
			Vector3 startPnt = this.transform.position - sideVec - heightVec;
			Vector3 endPnt = this.transform.position + sideVec - heightVec;
			Vector3 tanOffset = this.transform.right * - (this.transform.localScale.x * 0.2f);
			Vector3 tanOffset2 = this.transform.up * (radius * 0.5f);
			Vector3 tanSlide = this.transform.right * ((this.attrs.x - 0.5f) * (this.transform.localScale.x * 0.5f));
			Vector3 startTan = this.transform.position + heightVec + tanOffset + tanOffset2 + tanSlide;
			Vector3 endTan = this.transform.position + heightVec - tanOffset + tanOffset2 + tanSlide;
			Vector3 elongVec =  this.transform.forward * ((this.transform.localScale.z * 0.5f) - radius);
			Vector3 elongVec2 =  this.transform.forward * ((this.transform.localScale.z * 0.5f) - (radius*2.0f));

			float w0 = (1.0f - this.attrs.y) * 2.0f;
			float w1 = this.attrs.y * 2.0f;

			Handles.DrawBezier(startPnt - (elongVec*w0), endPnt - (elongVec*w1), startTan - elongVec, endTan - elongVec, Color.white, null, 2.0f);
			Handles.DrawBezier(startPnt + (elongVec*w0), endPnt + (elongVec*w1), startTan + elongVec, endTan + elongVec, Color.white, null, 2.0f);

			Gizmos.DrawWireSphere(startPnt - elongVec2, radius * w0);
			Gizmos.DrawWireSphere(endPnt - elongVec2, radius * w1);

			if(this.transform.localScale.z > 1.0f){
				Gizmos.DrawWireSphere(startPnt + elongVec2, radius);
				Gizmos.DrawWireSphere(endPnt + elongVec2, radius);

				Handles.DrawLine(
					startPnt + elongVec2 - (this.transform.right * radius), 
					startPnt - elongVec2 - (this.transform.right * radius));

				Handles.DrawLine(
					endPnt + elongVec2 + (this.transform.right * radius), 
					endPnt - elongVec2 + (this.transform.right * radius));
			}
		}

		void drawTorus(){
			Handles.color = Color.white;

			float radius = this.attrs.x;

			Vector3 elongationVec = this.transform.forward * ((this.transform.localScale.z * 0.5f) - radius);
			Vector3 sideVec = this.transform.right * ((this.transform.localScale.x * 0.5f) - radius);
			Vector3 radiusSideOffsetVec = this.transform.right * radius;
			Vector3 heightVec = this.transform.up * ((this.transform.localScale.y * 0.5f) - radius);
			Vector3 radiusUpOffsetVec = this.transform.up * radius;
			Vector3 sideCrossSecVec = this.transform.right * (this.transform.localScale.x * 0.5f);

			float crossSecRadius = this.transform.localScale.x * 0.5f;
			Vector3 radiusCrossSecVec = this.transform.up * crossSecRadius;
			Vector3 heightCrossSecVec = this.transform.up * ((this.transform.localScale.y *0.5f) - crossSecRadius);

			float crossSecRadiusIn = (this.transform.localScale.x * 0.5f) - (radius*2.0f);
			Vector3 sideCrossSecVecIn = this.transform.right * ((this.transform.localScale.x * 0.5f) - (radius * 2.0f));

			if(this.transform.localScale.y >= this.transform.localScale.x){
				// cross out section
				Handles.DrawWireArc(this.transform.position + heightCrossSecVec, 
					this.transform.forward, this.transform.right, 180.0f, crossSecRadius);

				Handles.DrawWireArc(this.transform.position - heightCrossSecVec, 
					this.transform.forward, this.transform.right, -180.0f, crossSecRadius);

				Handles.DrawLine(
					this.transform.position + heightCrossSecVec + sideCrossSecVec, 
					this.transform.position - heightCrossSecVec + sideCrossSecVec);

				Handles.DrawLine(
					this.transform.position + heightCrossSecVec - sideCrossSecVec, 
					this.transform.position - heightCrossSecVec - sideCrossSecVec);

				// cross in section
				Handles.DrawWireArc(this.transform.position + heightCrossSecVec, 
					this.transform.forward, this.transform.right, 180.0f, crossSecRadiusIn);

				Handles.DrawWireArc(this.transform.position - heightCrossSecVec, 
					this.transform.forward, this.transform.right, -180.0f, crossSecRadiusIn);

				Handles.DrawLine(
					this.transform.position + heightCrossSecVec + sideCrossSecVecIn, 
					this.transform.position - heightCrossSecVec + sideCrossSecVecIn);

				Handles.DrawLine(
					this.transform.position + heightCrossSecVec - sideCrossSecVecIn, 
					this.transform.position - heightCrossSecVec - sideCrossSecVecIn);
			}

			if(this.transform.localScale.z >= radius * 2.0f){
				// top section
				Handles.DrawWireArc(this.transform.position - elongationVec + heightVec, 
					this.transform.right, this.transform.up, -180.0f, radius);

				Handles.DrawWireArc(this.transform.position + elongationVec + heightVec, 
					this.transform.right, this.transform.up, 180.0f, radius);

				Handles.DrawLine(
					this.transform.position + elongationVec + heightVec + radiusUpOffsetVec , 
					this.transform.position - elongationVec + heightVec + radiusUpOffsetVec);

				Handles.DrawLine(
					this.transform.position + elongationVec + heightVec - radiusUpOffsetVec , 
					this.transform.position - elongationVec + heightVec - radiusUpOffsetVec);

				// bottom section
				Handles.DrawWireArc(this.transform.position - elongationVec - heightVec, 
					this.transform.right, this.transform.up, -180.0f, radius);

				Handles.DrawWireArc(this.transform.position + elongationVec - heightVec, 
					this.transform.right, this.transform.up, 180.0f, radius);

				Handles.DrawLine(
					this.transform.position + elongationVec - heightVec + radiusUpOffsetVec , 
					this.transform.position - elongationVec - heightVec + radiusUpOffsetVec);

				Handles.DrawLine(
					this.transform.position + elongationVec - heightVec - radiusUpOffsetVec , 
					this.transform.position - elongationVec - heightVec - radiusUpOffsetVec);

				// left section
				Handles.DrawWireArc(this.transform.position - elongationVec - sideVec, 
					this.transform.up, this.transform.right, 180.0f, radius);

				Handles.DrawWireArc(this.transform.position + elongationVec - sideVec, 
					this.transform.up, this.transform.right, -180.0f, radius);

				Handles.DrawLine(
					this.transform.position + elongationVec - sideVec + radiusSideOffsetVec , 
					this.transform.position - elongationVec - sideVec + radiusSideOffsetVec);

				Handles.DrawLine(
					this.transform.position + elongationVec - sideVec - radiusSideOffsetVec, 
					this.transform.position - elongationVec - sideVec - radiusSideOffsetVec);

				// right section
				Handles.DrawWireArc(this.transform.position - elongationVec + sideVec, 
					this.transform.up, this.transform.right, 180.0f, radius);

				Handles.DrawWireArc(this.transform.position + elongationVec + sideVec, 
					this.transform.up, this.transform.right, -180.0f, radius);

				Handles.DrawLine(
					this.transform.position + elongationVec + sideVec + radiusSideOffsetVec , 
					this.transform.position - elongationVec + sideVec + radiusSideOffsetVec);

				Handles.DrawLine(
					this.transform.position + elongationVec + sideVec - radiusSideOffsetVec, 
					this.transform.position - elongationVec + sideVec - radiusSideOffsetVec);
			}
		}

		void drawCylinder(){
			Handles.color = Color.white;
			
			float radius = this.transform.localScale.x;
			if(this.transform.localScale.z < radius){
				radius = this.transform.localScale.z;
			}

			radius *= 0.5f;

			Vector3 arcDir = this.transform.right;
			Vector3 extVec = - (this.transform.forward * ((this.transform.localScale.z * 0.5f) - radius));
			if(this.transform.localScale.z < this.transform.localScale.x){
				arcDir = this.transform.forward;
				extVec = (this.transform.right * ((this.transform.localScale.x*0.5f) - radius));
			}

			Vector3 heightVec = this.transform.up * (this.transform.localScale.y * 0.5f);

			// draw top
			Handles.DrawWireArc(this.transform.position + extVec + heightVec, this.transform.up, arcDir, 180.0f, radius);
			Handles.DrawWireArc(this.transform.position - extVec + heightVec, this.transform.up, arcDir, -180.0f, radius);

			Handles.DrawLine(
				this.transform.position + extVec + heightVec + (arcDir*radius), 
				this.transform.position - extVec + heightVec + (arcDir*radius));

			Handles.DrawLine(
				this.transform.position + extVec + heightVec - (arcDir*radius), 
				this.transform.position - extVec + heightVec - (arcDir*radius));

			// draw bottom
			Handles.DrawWireArc(this.transform.position + extVec - heightVec, this.transform.up, arcDir, 180.0f, radius+this.attrs.z);
			Handles.DrawWireArc(this.transform.position - extVec - heightVec, this.transform.up, arcDir, -180.0f, radius+this.attrs.z);
			
			Handles.DrawLine(
				this.transform.position + extVec - heightVec - (arcDir*(radius+this.attrs.z)), 
				this.transform.position - extVec - heightVec - (arcDir*(radius+this.attrs.z)));

			Handles.DrawLine(
				this.transform.position + extVec - heightVec + (arcDir*(radius+this.attrs.z)), 
				this.transform.position - extVec - heightVec + (arcDir*(radius+this.attrs.z)));

			// draw side lines
			Handles.DrawLine(
				this.transform.position + heightVec + (arcDir*radius), 
				this.transform.position - heightVec + (arcDir*(radius+this.attrs.z)));

			Handles.DrawLine(
				this.transform.position + heightVec - (arcDir*radius), 
				this.transform.position - heightVec - (arcDir*(radius+this.attrs.z)));
		}
		#endif // end if UNITY_EDITOR 
	}
}
