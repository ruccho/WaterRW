Shader "Water-RW/Standard"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

		_NormalA("Normal A", 2D) = "" {}
		_NormalAAmount("Normal A Intensity", Range(0,1)) = 0.05
		_NormalASpeed("Normal A Speed", Vector) = (0.2, 0.2, 0, 0)

		_NormalB("Normal B", 2D) = "" {}
		_NormalBAmount("Normal B Intensity", Range(0,1)) = 0.25
		_NormalBSpeed("Normal B Speed", Vector) = (-0.3, 0.3, 0, 0)

		_BGBlend("Background Blend", Range(0,1)) = 0.2
		_Transparency("Transparency", Range(0,1)) = 0.8
		_Multiplier("Multiplier", Color) = (0.7, 0.9, 1.0,1.0)
		_Addend("Addend", Color) = (0.08,0.24,0.33,0.0)

		_WaveSize("Wave Size in Viewport Space", Range(0, 1)) = 0.05
		_WaveDistance("Wave Distance in Viewport Space", Range(0, 1)) = 0.16
		_WavePosFreq("Wave Frequency by Position", Float) = 128
		_WaveTimeFreq("Wave Frequency by Time", Float) = 18.5

		_SurfaceColor("Surface Color", Color) = (0.43,0.48,0.62, 1.0)
		_SurfaceWidth("Surface Width in Pixel", Float) = 4
		_FadeDistance("Fade Distance in Viewport Space", Float) = 128
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

		GrabPass{}
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {

        CGPROGRAM

            #pragma vertex SpriteVertCustom
            #pragma fragment SpriteFragCustom
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

			sampler2D _GrabTexture;
			float4 _GrabTexture_TexelSize;

			sampler2D _NormalA;
			float4 _NormalA_ST;
			float _NormalAAmount;
			float4 _NormalASpeed;

			sampler2D _NormalB;
			float4 _NormalB_ST;
			float _NormalBAmount;
			float4 _NormalBSpeed;

			float _BGBlend;
			float _Transparency;
			float4 _Multiplier;
			float4 _Addend;

			float _WaveSize;
			float _WavePosFreq;
			float _WaveTimeFreq;
			float _WaveDistance;

			float4 _SurfaceColor;
			float _SurfaceWidth;
			float _FadeDistance;

			struct v2f_custom
			{
				float4 vertex   : SV_POSITION;
				fixed4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
				float4 screen : TEXCOORD1;
				float3 world : TEXCOORD2;
				
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2f_custom SpriteVertCustom(appdata_t IN)
			{
				v2f_custom OUT;
			
				UNITY_SETUP_INSTANCE_ID (IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
			
				OUT.vertex = UnityFlipSprite(IN.vertex, _Flip);
				OUT.vertex = UnityObjectToClipPos(OUT.vertex);
				OUT.texcoord = IN.texcoord;

				OUT.screen = ComputeScreenPos(OUT.vertex);
				OUT.world = mul(unity_ObjectToWorld, IN.vertex).xyz;

				OUT.color = IN.color * _Color * _RendererColor;
			
				#ifdef PIXELSNAP_ON
				OUT.vertex = UnityPixelSnap (OUT.vertex);
				#endif

				return OUT;
			}

			fixed4 SpriteFragCustom(v2f_custom IN) : SV_Target
			{
				// reflection
				float4 unitPosClip = UnityObjectToClipPos(float4(0, 0.5, 0, 1));
				
				float4 unitPosScreen = ComputeScreenPos(unitPosClip);
				unitPosScreen.xy /= unitPosScreen.w;

				float4 negUnitPosClip = UnityObjectToClipPos(float4(0, -0.5, 0, 1));				
				float4 negUnitPosScreen = ComputeScreenPos(negUnitPosClip);
				negUnitPosScreen.xy /= negUnitPosScreen.w;

				float surfacePosViewport = unitPosScreen.y;
				float2 fragPosViewport = IN.screen.xy / IN.screen.w;
				float reflectedPosViewport = 2 * surfacePosViewport - fragPosViewport.y;
				float2 reflectedPos = float2(fragPosViewport.x, reflectedPosViewport);

				// fade by distance
				float distFade = saturate(min(1, (1-reflectedPos.y) / min(_FadeDistance, 1-surfacePosViewport)));

				// distortion by sin wave
				float wave = max(0, 1-(surfacePosViewport - fragPosViewport.y)/_WaveDistance);
				float deltaWave = sin(_Time.y * _WaveTimeFreq + (surfacePosViewport - fragPosViewport.y) * _WavePosFreq) * (_WaveSize * 0.5) * pow(wave, 2);

				reflectedPos.x += deltaWave;

				// distortion by normal maps

				float2 posNormal = IN.world / 10;
				float2 deltaNormal = float2(0, 0);

				// normal map A
				float2 posNormalA = posNormal + _NormalASpeed * _Time.y;
				fixed2 texNormalA = UnpackNormal(tex2D(_NormalA, frac(posNormalA * _NormalA_ST.xy))).rg;
				deltaNormal += texNormalA * _NormalAAmount;

				// normal map B
				float2 posNormalB = posNormal + _NormalBSpeed * _Time.y;
				fixed2 texNormalB = UnpackNormal(tex2D(_NormalB, frac(posNormalB * _NormalB_ST.xy))).rg;
				deltaNormal += texNormalB * _NormalBAmount;

				reflectedPos += deltaNormal;
				
				// surface line
				float surfaceWidthScreen = _SurfaceWidth / _ScreenParams.y;
				float surface = step((1-IN.texcoord.y) * (unitPosScreen.y - negUnitPosScreen.y), surfaceWidthScreen);
				

				// remove vertical flip of ComputeScreenPos
				reflectedPos.y = ((reflectedPos.y * 2 - 1) * _ProjectionParams.x) * 0.5 + 0.5;
				fragPosViewport.y = ((fragPosViewport.y * 2 - 1) * _ProjectionParams.x) * 0.5 + 0.5;

				// flip for grabpass
				#if UNITY_UV_STARTS_AT_TOP
				reflectedPos.y = 1-reflectedPos.y;
				fragPosViewport.y = 1-fragPosViewport.y;
				#endif
								
				
				fixed4 bg = tex2D(_GrabTexture, fragPosViewport);
				fixed4 col = tex2D(_GrabTexture, reflectedPos);
				
				// hide clamped area of grabtex
				col.rgb *= step(reflectedPos.y, 1) * step(0, reflectedPos.y);

				// apply distance fade
				col *= distFade;

				// apply transparency
				col.a = (1 - _Transparency) + (col.r + col.g + col.b) / 3 * _Transparency;

				// apply background blend
				col = lerp(col, bg, _BGBlend);

				col.rgb *= _Multiplier.rgb;
				col.rgb += _Addend.rgb;

				// add surface line
				col = lerp(col, _SurfaceColor, surface);

				col *= IN.color;
				col.rgb *= col.a;
				return col;

			}


        ENDCG
        }
    }
}
