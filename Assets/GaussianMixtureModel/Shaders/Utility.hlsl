// Same as in "UnityCG.cginc".
// An almost-perfect approximation from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
float3 LinearToGammaSpace(in float3 linRGB)
{
    linRGB = max(linRGB, float3(0, 0, 0));
    return max(1.055 * pow(linRGB, 0.416666667) - 0.055, 0);
}

uint To1DIndex(in uint3 id, in uint dimension)
{
    return id.z * dimension * dimension + id.y * dimension + id.x;
}

uint3 To3DIndex(in uint id, in uint dimension)
{
    uint z = id / (dimension * dimension);
    id -= z * dimension * dimension;

    uint y = id / dimension;
    id -= y * dimension;

    uint x = id / 1;
    return uint3(x, y, z);
}

float3x3 MakeCovarianceMatrix(in float2x3 m2x3)
{
    float3 r0 = m2x3[0];
    float3 r1 = m2x3[1];
    float3x3 m = 0;
    m[0] = r0;
    m[1] = float3(r0.y, r1.x, r1.y);
    m[2] = float3(r0.z, r1.y, r1.z);
    return m;
}

// Must duplicate code as buffer types differ.
float3x3 ReadMatrixSymmetric3x3(StructuredBuffer<float2x3> buffer, in uint index)
{
    return MakeCovarianceMatrix(buffer[index]);
}

float3x3 ReadMatrixSymmetric3x3(RWStructuredBuffer<float2x3> buffer, in uint index)
{
    return MakeCovarianceMatrix(buffer[index]);
}

void WriteMatrixSymmetric3x3(RWStructuredBuffer<float2x3> buffer, in uint index, in float3x3 m)
{
    float2x3 m2x3;
    m2x3[0] = m[0];
    m2x3[1] = float3(m[1][1], m[1][2], m[2][2]);
    buffer[index] = m2x3;
}
