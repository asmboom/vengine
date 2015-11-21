#version 430 core

in vec2 UV;
#include LogDepth.glsl
#include Lighting.glsl
#include UsefulIncludes.glsl
#include Shade.glsl

layout (std430, binding = 6) buffer RandomsBuffer
{
    float Randoms[]; 
}; 

float randomizer = 0;
void Seed(vec2 seeder){
    randomizer += 138.345341 * (rand2s(seeder)) ;
}

int randsPointer = 0;
uniform int RandomsCount;
float getRand(){
    float r = Randoms[randsPointer];
    randsPointer++;
    if(randsPointer >= RandomsCount) randsPointer = 0;
    return r;
}

uniform int UseRSM;

float meshRoughness;
float meshSpecular;
float meshDiffuse;

uniform float VDAOGlobalMultiplier;


out vec4 outColor;
bool IgnoreLightingFragment = false;

vec2 refractUV(){
    vec3 rdir = normalize(CameraPosition - texture(worldPosTex, UV).rgb);
    vec3 crs1 = normalize(cross(CameraPosition, texture(worldPosTex, UV).rgb));
    vec3 crs2 = normalize(cross(crs1, rdir));
    vec3 rf = refract(rdir, texture(normalsTex, UV).rgb, 0.6);
    return UV - vec2(dot(rf, crs1), dot(rf, crs2)) * 0.3;
}

mat4 PV = (ProjectionMatrix * ViewMatrix);

/********************/

struct AABox
{
    vec4 Minimum;
    vec4 Maximum;
    vec4 Color;
    int Container;
    int StupidAlignment1;
    int StupidAlignment2;
    int StupidAlignment3;
};
uniform int AABoxesCount;

layout (std430, binding = 7) buffer BoxesBuffer
{
    AABox AABoxes[]; 
}; 

/********************/

struct AAContainerBox
{
    vec4 Minimum;
    vec4 Maximum;
    int StartIndex;
    int InnerCount;
    int StupidAlignment5;
    int StupidAlignment6;
};
uniform int AABoxesContainersCount;

layout (std430, binding = 8) buffer Boxes2Buffer
{
    AAContainerBox AAContainerBoxes[]; 
}; 

/********************/

struct SimplePointLight
{
    vec4 Position;
    vec4 Color;
};
uniform int SimpleLightsCount;

layout (std430, binding = 5) buffer SimplePointLightBuffer
{
    SimplePointLight simplePointLights[]; 
}; 


// well.. yeah
// we gonna pathtrace


float NearHitPos = 0.0;
bool tryIntersectBox(vec3 origin, vec3 direction,
vec3 bMin,
vec3 bMax)
{
    vec3 OMIN = ( bMin - origin ) / direction;    
    vec3 OMAX = ( bMax - origin ) / direction;    
    vec3 MAX = max ( OMAX, OMIN );    
    vec3 MIN = min ( OMAX, OMIN );  
    float final = min ( MAX.x, min ( MAX.y, MAX.z ) );
    float start = max ( max ( MIN.x, 0.0 ), max ( MIN.y, MIN.z ) );    
    NearHitPos = start;
    return final > start;
}

float LastCombinedDistance = 9999999;
vec3 getIntersectInContainer(int start, int count, int container, vec3 origin, vec3 direction){
    float lastDistance = 9999999;
    vec3 color = vec3(0);
    AAContainerBox abc = AAContainerBoxes[container];
    for(int i=start;i<start + count;i++){
       // if(AABoxes[i].Container != container) continue;
        if(tryIntersectBox(origin, direction, AABoxes[i].Minimum.xyz, AABoxes[i].Maximum.xyz) &&
                NearHitPos < lastDistance){
            lastDistance = NearHitPos;
            color = AABoxes[i].Color.a * AABoxes[i].Color.rgb; 
        }
    }
    LastCombinedDistance = lastDistance;
    return color;
}

vec3 getIntersect(vec3 originalColor, vec3 origin, vec3 direction){

    vec3 color = originalColor;
    float lastDistance = 9999999;
    
    int indices[16] = int[16](0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    int hit = 0;
    for(int i=0;i<AABoxesContainersCount;i++){
        if(tryIntersectBox(origin, direction, AAContainerBoxes[i].Minimum.xyz, AAContainerBoxes[i].Maximum.xyz)){
            indices[hit] = i;
            hit++;
            //color = getIntersectInContainer(i, origin, direction);
        }
        
        // color += getIntersectInContainer(i, origin, direction);
    }
    lastDistance = 9999999;
    for(int i=0;i<hit;i++){
        
        vec3 a = getIntersectInContainer(AAContainerBoxes[indices[i]].StartIndex, AAContainerBoxes[indices[i]].InnerCount, indices[i], origin, direction);
        if(LastCombinedDistance < lastDistance){
            lastDistance = LastCombinedDistance;
            color = a;
        }
        NearHitPos = 0.0;
    }
    
    
    LastCombinedDistance = 999999;
    return color;
}
bool getIntersectInContainerEarlyEndShadows(int start, int count, int container, vec3 origin, vec3 direction, float maxdistance){
    float lastDistance = 9999999;
    AAContainerBox abc = AAContainerBoxes[container];
    for(int i=start;i<start + count;i++){
        if(tryIntersectBox(origin, direction, AABoxes[i].Minimum.xyz, AABoxes[i].Maximum.xyz)){
            if(NearHitPos < maxdistance) return true;
        }
    }
    LastCombinedDistance = lastDistance;
    return false;
}

float getIntersectShadows(vec3 origin, vec3 direction, float maxdistance){

    float lastDistance = 9999999;
    
    int indices[16] = int[16](0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    int hit = 0;
    for(int i=0;i<AABoxesContainersCount;i++){
        if(tryIntersectBox(origin, direction, AAContainerBoxes[i].Minimum.xyz, AAContainerBoxes[i].Maximum.xyz) && NearHitPos < maxdistance){
            indices[hit] = i;
            hit++;
        }
    }
    lastDistance = 9999999;
    for(int i=0;i<hit;i++){
        
        if(getIntersectInContainerEarlyEndShadows(AAContainerBoxes[indices[i]].StartIndex, AAContainerBoxes[indices[i]].InnerCount, indices[i], origin, direction, maxdistance)){
            return 0;
        }
    }
    LastCombinedDistance = 999999;
    return 1;
}

float traceVisibility(vec3 p1, vec3 p2){
    return getIntersectShadows(p1, normalize(p2 - p1), distance(p1, p2));
}

vec3 random3dSample(){
    return normalize(vec3(
    getRand() * 2 - 1, 
    getRand() * 2 - 1, 
    getRand() * 2 - 1
    ));
}

vec2 getTexel(sampler2D t){
 return 1.0 / vec2(textureSize(t, 0));
}

vec3 reconstructTangentWorldSpace(vec2 uv){
    vec3 bc = texture(worldPosTex, uv).rgb;
    vec2 dsp = getTexel(worldPosTex);
    vec3 bdx = bc - texture(worldPosTex, uv+vec2(dsp.x, 0)).rgb;
    vec3 bdy = bc - texture(worldPosTex, uv+vec2(0, dsp.y)).rgb;
    
    return vec3(0, 0, 1);
}

// using this brdf makes cosine diffuse automatically correct
vec3 BRDF(vec3 reflectdir, vec3 norm, float roughness){
    vec3 displace = random3dSample();
    displace = displace * sign(dot(norm, displace));
    return mix(displace, reflectdir, roughness);
}
vec3 vec3pow(vec3 inputx, float po){
    return vec3(
    pow(inputx.x, po),
    pow(inputx.y, po),
    pow(inputx.z, po)
    );
}
vec3 adjustGamma(vec3 c, float gamma){
    return vec3pow(c, 1.0/gamma)/gamma;
}

vec2 HitPos = vec2(-2);
float textureMaxFromLine(float v1, float v2, vec2 p1, vec2 p2, sampler2D sampler){
    float ret = 0;
    for(int i=0; i<5; i++){
        float ix = getRand();
        vec2 muv = mix(p1, p2, ix);
        float expr = min(muv.x, muv.y) * -(max(muv.x, muv.y)-1.0);
        float tmp = min(0, sign(expr)) * 
        ret + 
        min(0, -sign(expr)) * 
        max(
        mix(v1, v2, ix) - texture(sampler, muv).r,
        ret);
        if(tmp > ret) HitPos = muv;
        ret = tmp;
    }
    //if(abs(ret) > 0.1) return 0;
    return abs(ret) - 0.0006;
}
vec2 saturatev2(vec2 v){
    return clamp(v, 0.0, 1.0);
}
float testVisibility3d(vec3 w1, vec3 w2) {
    HitPos = vec2(-2);
    vec4 clipspace = (PV) * vec4((w1), 1.0);
    vec2 sspace1 = saturatev2((clipspace.xyz / clipspace.w).xy * 0.5 + 0.5);
    vec4 clipspace2 = (PV) * vec4((w2), 1.0);
    vec2 sspace2 = saturatev2((clipspace2.xyz / clipspace2.w).xy * 0.5 + 0.5);
    float d3d1 = toLogDepth(length(ToCameraSpace(w1)));
    float d3d2 = toLogDepth(length(ToCameraSpace(w2)));
    float mx = (textureMaxFromLine(d3d1, d3d2, sspace1, sspace2, depthTex));

    return mx;
}

vec3 lookupCubeMap(vec3 displace){
    vec3 c = texture(cubeMapTex, displace).rgb;
    //return c;
    return vec3pow(c*1.1, 3.7) * 1;
}


uniform int UseVDAO;
uniform int UseHBAO;
vec3 Radiosity()
{
    if(texture(diffuseColorTex, UV).r >= 999){ 
        return texture(cubeMapTex, normalize(texture(worldPosTex, UV).rgb)).rgb;
    }
// gather data
    uint idvals = texture(meshIdTex, UV).b;
    /*
    uint packpart1 = packUnorm4x8(vec4(AORange, AOStrength, AOAngleCutoff, SubsurfaceScatteringMultiplier));
    uint packpart2 = packUnorm4x8(vec4(VDAOMultiplier, VDAOSamplingMultiplier, VDAORefreactionMultiplier, 0));
    */
    vec4 vals = unpackUnorm4x8(idvals);
    float vdaomult = vals.x * 4 + 0.1;
    float vdaosampling = vals.y * 2;
    float vdaorefract = vals.z;

    uint tangentEncoded = texture(meshIdTex, UV).a;
    vec3 tangent = unpackSnorm4x8(tangentEncoded).xyz;
	
    vec3 posCenter = texture(worldPosTex, UV).rgb;
    vec3 normalCenter = normalize(texture(normalsTex, UV).rgb);
    vec3 ambient = vec3(0);
    
    vec3 dir = normalize(reflect(posCenter, normalCenter));
    vec3 vdir = normalize(posCenter);
    vec3 dir2 = normalize(refract(posCenter, normalCenter, 0.8));
    
    uint counter = 0;   
    float meshRoughness = 1.0 - texture(meshDataTex, UV).a;
    
    mat3 TBN = inverse(transpose(mat3(
        normalCenter,
        cross(tangent, normalCenter),
        tangent
    )));
    
   // int samples = int(mix(8, 48, 1.0 - meshRoughness));
    float samples = mix(3, 12, 1.0 - meshRoughness);
    float stepsize = PI*2 / samples;
    float ringsize = length(posCenter)*0.8;
    //for(float g = 0; g < samples; g+=1)
    for(float g = 0.0; g <= PI*2; g+=stepsize)
    {
        float minang = 0;

        //vec3 displace = normalize(BRDF(dir, norm, meshRoughness)) * ringsize;
		float grd = getRand() * stepsize;
        vec2 zx = vec2(sin(g+grd), cos(g+grd));
        vec3 displace = normalize(mix((TBN * normalize(vec3(zx, sqrt(1.0 - length(zx))))), dir, meshRoughness));
        //vec3 displace = normalize(BRDFBiased(dir, norm, meshRoughness, (vec2(getRand2(), getRand2())))) * ringsize;
        
       // vec3 displace = normalize(BRDF(dir, normalCenter, meshRoughness));
        //float fresnel = fresnelSchlick(dot(displace, normalCenter));
        
        vec3 color = shadePhotonSpecular(UV, lookupCubeMap(displace));
       // vec3 color = vec3(0);
       // color = getIntersect(color, FromCameraSpace(posCenter), displace);
        float dotdiffuse = max(0, dot(displace, normalCenter));
        vec3 radiance = makeFresnel(1.0 - max(0, dot(displace, vdir)), color);
        ambient += radiance;// * vi * vi2;
        counter++;
    }
    vec3 vdaoMain = counter == 0 ? vec3(0) : (ambient / (counter)) * vdaomult;
    float metalness =  texture(meshDataTex, UV).z;
    
    if(metalness < 1.0){
        ambient = vec3(0);
        counter = 0;  
        for(int i=0; i<samples; i++)
        {
            vec3 displace = normalize(BRDF(dir, normalCenter, 0.0));
            
            float vi = testVisibility3d(FromCameraSpace(posCenter), FromCameraSpace(posCenter) + displace);
            vi = HitPos.x > 0 && reverseLog(vi) < 1.0  ? reverseLog(vi) : 1.0;
            float vi2 = testVisibility3d(FromCameraSpace(posCenter), FromCameraSpace(posCenter) + displace*0.1);
            vi2 = HitPos.x > 0 && reverseLog(vi2) < 0.1  ? reverseLog(vi2)*10.0 : 1.0;
                    
            vec3 color = shadePhoton(UV, lookupCubeMap(displace));
           // color = getIntersect(color, FromCameraSpace(posCenter), displace);
            float dotdiffuse = max(0, dot(displace, normalCenter));
            vec3 radiance = color;
            ambient += radiance;// * vi * vi2;
            counter++;
        }        
        vec3 vdaoFullDiffuse = counter == 0 ? vec3(0) : (ambient / (counter)) * vdaomult;
        vdaoMain = mix((vdaoMain + vdaoFullDiffuse)*0.5, vdaoMain, metalness);
    }
    
    
    
    ambient = vec3(0);
    counter = 0;
    
    if(vdaorefract > 0){
        dir = normalize(refract(posCenter, normalCenter, 0.3));
        for(int i=0; i<samples; i++)
        {
            vec3 displace = normalize(BRDF(dir2, -normalCenter, meshRoughness));
                            
            vec3 color = shadePhoton(UV, lookupCubeMap(displace));
            //color = getIntersect(color, FromCameraSpace(posCenter), displace);
            float dotdiffuse = max(0, dot(displace, -normalCenter));
            vec3 radiance = color;
            ambient += radiance;
            counter++;
        }
    }
    
    vec3 vdaoRefract = counter == 0 ? vec3(0) : (ambient / (counter)) * vdaorefract;
    
    return (vdaoMain + vdaoRefract) * VDAOGlobalMultiplier;
}


float lookupShadow(vec2 fuv){
    float outc = 0;
    int counter = 0;
    float depthCenter = texture(depthTex, fuv).r;
    for(float g = 0; g < mPI2 * 2; g+=GOLDEN_RATIO)
    {
        for(float g2 = 0; g2 < 6.0; g2+=1.0)
        {
            vec2 gauss = vec2(sin(g + g2)*ratio, cos(g + g2)) * (g2 * 0.01);
            float color = texture(indirectTex, fuv + gauss).r;
            float depthThere = texture(indirectTex, fuv + gauss).g;
            if(abs(depthThere - depthCenter) < 0.01){
                outc += color;
                counter++;
            }
        }
    }
    return counter == 0 ? texture(indirectTex, fuv).r : outc / counter;
}

void main()
{   

    //  float alpha = texture(texColor, UV).a;
    vec2 nUV = UV;
    // if(alpha < 0.99){
    //nUV = refractUV();
    // }
    vec3 colorOriginal = texture(diffuseColorTex, nUV).rgb;    
    vec4 normal = texture(normalsTex, nUV);
    meshDiffuse = normal.a;
    meshSpecular = texture(worldPosTex, nUV).a;
    meshRoughness = texture(meshDataTex, nUV).a;
    float meshMetalness =  texture(meshDataTex, UV).z;
    vec3 color1 = vec3(0);
    if(normal.x == 0.0 && normal.y == 0.0 && normal.z == 0.0){
        color1 = colorOriginal;
        IgnoreLightingFragment = true;
    } else {
        // color1 = colorOriginal * 0.01;
    }
    
    //vec3 color1 = colorOriginal * 0.2;
    //if(texture(texColor, UV).a < 0.99){
    //    color1 += texture(texColor, UV).rgb * texture(texColor, UV).a;
    //}
    gl_FragDepth = texture(depthTex, nUV).r;
    vec4 fragmentPosWorld3d = texture(worldPosTex, nUV);
    Seed(fragmentPosWorld3d.xy + fragmentPosWorld3d.yz);
    randsPointer = int(randomizer * 123.86786 ) % RandomsCount;
    vec3 cameraRelativeToVPos = normalize(-fragmentPosWorld3d.xyz);
    fragmentPosWorld3d.xyz = FromCameraSpace(fragmentPosWorld3d.xyz);


    //vec3 cameraRelativeToVPos = normalize( CameraPosition - fragmentPosWorld3d.xyz);
    float len = length(cameraRelativeToVPos);
    // int foundSun = 0;
    if(!IgnoreLightingFragment) for(int i=0;i<LightsCount;i++){

        mat4 lightPV = (LightsPs[i] * LightsVs[i]);
        vec4 lightClipSpace = lightPV * vec4(fragmentPosWorld3d.xyz, 1.0);
        if(lightClipSpace.z <= 0.0) continue;
        vec2 lightScreenSpace = ((lightClipSpace.xyz / lightClipSpace.w).xy + 1.0) / 2.0;   

        float percent = 0;
        if(lightScreenSpace.x >= 0.0 && lightScreenSpace.x <= 1.0 && lightScreenSpace.y >= 0.0 && lightScreenSpace.y <= 1.0) {
            percent = getShadowPercent(lightScreenSpace, fragmentPosWorld3d.xyz, i);

        } else percent = lookupShadow(UV);
        vec3 radiance = shadeUV(UV, LightsPos[i], LightsColors[i]);
        color1 += (radiance) * percent;
    }
    if(!IgnoreLightingFragment) for(int i=0;i<SimpleLightsCount;i++){
        color1 += shadeUV(UV, simplePointLights[i].Position.xyz, simplePointLights[i].Color);
    }

    if(UseVDAO == 1 && UseHBAO == 0) color1 += Radiosity();
    if(UseVDAO == 1 && UseHBAO == 1) color1 += Radiosity() * texture(HBAOTex, UV).a;
    //   if(UseVDAO == 0 && UseHBAO == 1) color1 += texture(HBAOTex, UV).rrr;
    
    // experiment
    
    
    outColor = vec4(clamp(color1, 0.0, 9999.0), 1.0);
    
    
}