// Светящийся «голографический» материал для улик, портала и интерфейса.
// Работает и во встроенном конвейере (Built-in), и в URP, поддерживает
// стерео-рендеринг Quest (Single Pass Instanced) и стенсил для портала.
Shader "Cipher/Hologram"
{
    Properties
    {
        _Color ("Цвет", Color) = (0, 1, 1, 1)
        _RimColor ("Цвет контура", Color) = (1, 1, 1, 1)
        _RimPower ("Резкость контура", Float) = 2
        _Alpha ("Прозрачность", Range(0, 1)) = 0.6
        _Glow ("Яркость", Float) = 1
        _Scan ("Частота полос (0 = нет)", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]
            Stencil { Ref 1 Comp [_StencilComp] }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color, _RimColor;
            float _RimPower, _Alpha, _Glow, _Scan;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wN : TEXCOORD0;
                float3 wP : TEXCOORD1;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wN = UnityObjectToWorldNormal(v.normal);
                o.wP = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.wN + 1e-5);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.wP);
                float rim = pow(1 - saturate(abs(dot(n, viewDir))), _RimPower);
                float scan = _Scan > 0 ? 0.75 + 0.25 * sin(i.wP.y * _Scan + _Time.y * 4) : 1;
                fixed3 col = (_Color.rgb * _Glow + _RimColor.rgb * rim) * scan * i.color.rgb;
                return fixed4(col, saturate((_Alpha + rim) * _Color.a * i.color.a));
            }
            ENDCG
        }
    }
}
