Shader "Custom/URP/Explosion"
{
    Properties
    {
        _Explosion_Tex("Explosion Texture (Flipbook 8x8)", 2D) = "white" {}
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 uv           : TEXCOORD0; // uv.xy = UVs, uv.z = Flipbook frame index (0..1)
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uvFlipbook   : TEXCOORD0;
                float4 color        : COLOR;
            };

            TEXTURE2D(_Explosion_Tex);
            SAMPLER(sampler_Explosion_Tex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Explosion_Tex_ST;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Перевод вершины в клип-пространство
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;

                // --- Логика Flipbook UV 8x8 ---
                float2 uv = input.uv.xy;
                
                float totalTiles = 64.0; // 8 x 8
                float colsoffset = 1.0 / 8.0;
                float rowsoffset = 1.0 / 8.0;

                // Индекс кадра рассчитывается по uv.z * 63
                float currentTileIndex = round(fmod(input.uv.z * 63.0, totalTiles));
                if (currentTileIndex < 0.0) 
                    currentTileIndex += totalTiles;

                // Позиция по X и Y на атласе (8х8)
                float tileX = round(fmod(currentTileIndex, 8.0));
                float tileY = round(fmod((currentTileIndex - tileX) / 8.0, 8.0));
                
                // Инвертируем Y для считывания сверху вниз
                tileY = 7.0 - tileY;

                float2 offset = float2(tileX * colsoffset, tileY * rowsoffset);
                float2 tiling = float2(colsoffset, rowsoffset);

                // Итоговые UV координаты для сэмплинга
                output.uvFlipbook = uv * tiling + offset;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Сэмплируем текстуру
                half4 texColor = SAMPLE_TEXTURE2D(_Explosion_Tex, sampler_Explosion_Tex, input.uvFlipbook);

                // Применяем альфа-канал цвета вершины (Particle Color)
                half finalAlpha = texColor.a * input.color.a;

                // Выводим скомпонованный Unlit HDR цвет
                return half4(texColor.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}