float saturate(float val)
{
    return clamp( val, 0.0, 1.0 );
}
vec3 saturate(vec3 val)
{
    return clamp( val, 0.0, 1.0 );
}
void clip(float val)
{
    if ( val < 0.0 )
        discard;
}
vec3 UnpackNormalmapRGorAG(vec4 packednormal)
{
    // This do the trick
   packednormal.x *= packednormal.w;

    vec3 normal;
    normal.xy = packednormal.xy * 2.0 - 1.0;
    normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
    return normal;
}
vec3 UnpackNormal(vec4 packednormal)
{
#if defined(UNITY_NO_DXT5nm)
    return packednormal.xyz * 2 - 1;
#elif defined(UNITY_ASTC_NORMALMAP_ENCODING)
    return UnpackNormalDXT5nm(packednormal);
#else
    return UnpackNormalmapRGorAG(packednormal);
#endif
}
vec3 UnpackScaleNormalRGorAG(vec4 packednormal, float bumpScale)
{
    #if defined(UNITY_NO_DXT5nm)
        half3 normal = packednormal.xyz * 2 - 1;
        #if (SHADER_TARGET >= 30)
            // SM2.0: instruction count limitation
            // SM2.0: normal scaler is not supported
            normal.xy *= bumpScale;
        #endif
        return normal;
    #elif defined(UNITY_ASTC_NORMALMAP_ENCODING)
        half3 normal;
        normal.xy = (packednormal.wy * 2 - 1);
        normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
        normal.xy *= bumpScale;
        return normal;
    #else
        // This do the trick
        packednormal.x *= packednormal.w;

        vec3 normal;
        normal.xy = (packednormal.xy * 2.0 - 1.0);
        #if 1//(SHADER_TARGET >= 30)
            // SM2.0: instruction count limitation
            // SM2.0: normal scaler is not supported
            normal.xy *= bumpScale;
        #endif
        normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
        return normal;
    #endif
}

vec3 UnpackScaleNormal(vec4 packednormal, float bumpScale)
{
    return UnpackScaleNormalRGorAG(packednormal, bumpScale);
}
vec3 BlendNormals(vec3 n1, vec3 n2)
{
    return normalize(vec3(n1.xy + n2.xy, n1.z*n2.z));
}
vec4 UnityEdgeLengthBasedTessCull (vec4 v0, vec4 v1, vec4 v2, float edgeLength, float maxDisplacement)
{
    //Do nothing, tessellation doesn't work in gdshaders anyway
    return vec4(0,0,0,0);
}
struct appdata_full
{
    vec4 vertex;// : POSITION;
    vec4 tangent;// : TANGENT;
    vec3 normal;// : NORMAL;
    vec4 texcoord;// : TEXCOORD0;
    vec4 texcoord1;// : TEXCOORD1;
    vec4 texcoord2;// : TEXCOORD2;
    vec4 texcoord3;// : TEXCOORD3;
    vec4 color;// : COLOR;
    //UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct SurfaceOutputStandard
{
	vec3 Albedo;
	vec3 Normal;
	vec3 Emission;
    float Occlusion;
	vec3 Specular;
    float Gloss;
    float Smoothness;
    float Metallic;
	float Alpha;
};
#define SurfaceOutput SurfaceOutputStandard
#define SurfaceOutputStandardSpecular SurfaceOutputStandard
#define WorldNormalVector( input, vec ) input.worldNormal
#define mul( x, y ) x * y