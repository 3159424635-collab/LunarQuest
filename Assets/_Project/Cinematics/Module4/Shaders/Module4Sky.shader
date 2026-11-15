Shader "LunarQuest/Module4Sky"
{
    Properties { _Top("Top",Color)=(.015,.025,.07,1) _Bottom("Bottom",Color)=(.35,.17,.12,1) _Stars("Stars",Range(0,1))=1 }
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; }; struct V { float4 p:SV_POSITION; float3 dir:TEXCOORD0; };
            float4 _Top,_Bottom; float _Stars;
            V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.dir=i.p.xyz;return o;}
            float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
            half4 frag(V i):SV_Target {
                float3 d=normalize(i.dir);float f=saturate(d.y*.65+.45);
                float3 c=lerp(_Bottom.rgb,_Top.rgb,pow(f,.4));
                float3 grid=floor(d*1100);float star=step(.99925,hash(grid));
                star*=pow(saturate(1-length(frac(d*1100)-.5)*1.9),1.4);
                c+=star*_Stars*lerp(float3(.5,.65,1),float3(1,.8,.55),hash(grid+13));
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
