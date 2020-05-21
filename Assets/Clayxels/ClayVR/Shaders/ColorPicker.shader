Shader "ClayxelsVR/ColorPicker" {
    Properties{
        _ZCoord("Z Coord", float) = 1
        _HSV("HSV", float) = 0
        _MainTex("Base (RGB) Trans (A)", 2D) = "white" {}
    }

    SubShader{
        Tags {"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
        LOD 100

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ZCoord;
            float _HSV;

            float3 rgb_to_hsv_no_clip(float3 RGB)
            {
                float3 HSV;

                float minChannel, maxChannel;
                if (RGB.x > RGB.y) {
                    maxChannel = RGB.x;
                    minChannel = RGB.y;
                }
                else {
                    maxChannel = RGB.y;
                    minChannel = RGB.x;
                }

                if (RGB.z > maxChannel) maxChannel = RGB.z;
                if (RGB.z < minChannel) minChannel = RGB.z;

                HSV.xy = 0;
                HSV.z = maxChannel;
                float delta = maxChannel - minChannel;             //Delta RGB value
                if (delta != 0) {                    // If gray, leave H  S at zero
                    HSV.y = delta / HSV.z;
                    float3 delRGB;
                    delRGB = (HSV.zzz - RGB + 3 * delta) / (6.0 * delta);
                    if (RGB.x == HSV.z) HSV.x = delRGB.z - delRGB.y;
                    else if (RGB.y == HSV.z) HSV.x = (1.0 / 3.0) + delRGB.x - delRGB.z;
                    else if (RGB.z == HSV.z) HSV.x = (2.0 / 3.0) + delRGB.y - delRGB.x;
                }
                return (HSV);
            }

            float3 hsv_to_rgb(float3 HSV)
            {
                float3 RGB = HSV.z;

                float var_h = HSV.x * 6;
                float var_i = floor(var_h);   // Or ... var_i = floor( var_h )
                float var_1 = HSV.z * (1.0 - HSV.y);
                float var_2 = HSV.z * (1.0 - HSV.y * (var_h - var_i));
                float var_3 = HSV.z * (1.0 - HSV.y * (1 - (var_h - var_i)));
                if (var_i == 0) { RGB = float3(HSV.z, var_3, var_1); }
                else if (var_i == 1) { RGB = float3(var_2, HSV.z, var_1); }
                else if (var_i == 2) { RGB = float3(var_1, HSV.z, var_3); }
                else if (var_i == 3) { RGB = float3(var_1, var_2, HSV.z); }
                else if (var_i == 4) { RGB = float3(var_3, var_1, HSV.z); }
                else { RGB = float3(HSV.z, var_1, var_2); }

                return (RGB);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 col = float4(0, 0, 0, 0);
                if (_HSV > 0) {
                    col = float4(hsv_to_rgb(float3(i.texcoord, _ZCoord)), 1.0);
                }
                else {
                    col = float4(i.texcoord, _ZCoord, 1.0);
                }
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

}
