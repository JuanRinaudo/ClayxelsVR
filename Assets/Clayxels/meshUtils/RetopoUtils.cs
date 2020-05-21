#if UNITY_EDITOR
#if CLAYXELS_RETOPO

using System;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Clayxels{
	public static class RetopoUtils{
		static class NativeLib
		{
		    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
		    static public extern IntPtr LoadLibrary(string lpFileName);

		    [DllImport("kernel32", SetLastError = true)]
		    [return: MarshalAs(UnmanagedType.Bool)]
		    static public extern bool FreeLibrary(IntPtr hModule);

		    [DllImport("kernel32")]
		    static public extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RetopoMeshDelegate(IntPtr verts, int numVerts, IntPtr indices, int numTriangles, IntPtr colors, int maxVerts, int maxFaces);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int GetRetopoMeshVertsCountDelegate();

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int GetRetopoMeshTrisCountDelegate();

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void GetRetopoMeshDelegate(IntPtr verts, IntPtr indices, IntPtr normals, IntPtr colors);

		public static unsafe void retopoMesh(Mesh mesh, int maxVerts = -1, int maxFaces = -1){
			IntPtr? libPtr = null;
			
			#if CLAYXELS_RETOPO_LIB
				libPtr = NativeLib.LoadLibrary(CLAYXELS_RETOPO_LIB);
			#else
				libPtr = NativeLib.LoadLibrary("Assets\\Clayxels\\meshUtils\\RetopoLib.dll");
			#endif

			if(libPtr == null){
				Debug.Log("failed to find Assets\\Clayxels\\meshUtils\\RetopoLib.dll, please define CLAYXELS_RETOPO_LIB with the actual path to this lib in your project");
				return;
			}

			try{
				IntPtr retopoMeshFuncPtr = NativeLib.GetProcAddress(libPtr.Value, "retopoMesh");
				RetopoMeshDelegate retopoMesh = (RetopoMeshDelegate)Marshal.GetDelegateForFunctionPointer(retopoMeshFuncPtr, typeof(RetopoMeshDelegate));
				
				IntPtr getRetopoMeshVertsCountFuncPtr = NativeLib.GetProcAddress(libPtr.Value, "getRetopoMeshVertsCount");
				GetRetopoMeshVertsCountDelegate getRetopoMeshVertsCount = (GetRetopoMeshVertsCountDelegate)Marshal.GetDelegateForFunctionPointer(getRetopoMeshVertsCountFuncPtr, typeof(GetRetopoMeshVertsCountDelegate));
				
				IntPtr getRetopoMeshTrisCountFuncPtr = NativeLib.GetProcAddress(libPtr.Value, "getRetopoMeshTrisCount");
				GetRetopoMeshTrisCountDelegate getRetopoMeshTrisCount = (GetRetopoMeshTrisCountDelegate)Marshal.GetDelegateForFunctionPointer(getRetopoMeshTrisCountFuncPtr, typeof(GetRetopoMeshTrisCountDelegate));
				
				IntPtr getRetopoMeshFuncPtr = NativeLib.GetProcAddress(libPtr.Value, "getRetopoMesh");
				GetRetopoMeshDelegate getRetopoMesh = (GetRetopoMeshDelegate)Marshal.GetDelegateForFunctionPointer(getRetopoMeshFuncPtr, typeof(GetRetopoMeshDelegate));
				
				Vector3[] vertsArray = mesh.vertices;
				int[] indices = mesh.triangles;
				Color[] colors = mesh.colors;
				fixed(Vector3* vertsPtr = vertsArray){
					fixed(int* indicesPtr = indices){
						fixed(Color* colorsPtr = colors){
							retopoMesh((IntPtr)vertsPtr, mesh.vertices.Length, (IntPtr)indicesPtr, indices.Length, (IntPtr)colorsPtr, maxVerts, maxFaces);
						}
					}
				}
				
				int newVertsCount = getRetopoMeshVertsCount();
				int newTrisCount = getRetopoMeshTrisCount();
				
				Vector3[] newVerts = new Vector3[newVertsCount];
				int[] newIndices = new int[newTrisCount];
				Vector3[] normals = new Vector3[newVertsCount];
				Color[] newColors = new Color[newVertsCount];

				fixed(Vector3* vertsPtr = newVerts){
					fixed(int* indicesPtr = newIndices){
						fixed(Vector3* normalsPtr = normals){
							fixed(Color* newColorsPtr = newColors){
								getRetopoMesh((IntPtr)vertsPtr, (IntPtr)indicesPtr, (IntPtr)normalsPtr, (IntPtr)newColorsPtr);
							}
						}
					}
				}
				
				mesh.Clear();
				mesh.vertices = newVerts;
				mesh.triangles = newIndices;
				mesh.normals = normals;
				mesh.colors = newColors;
			}
			catch{
				Debug.Log("error during retopo");
			}
			
			NativeLib.FreeLibrary(libPtr.Value);
			libPtr = null;
		}
	}
}
#endif
#endif