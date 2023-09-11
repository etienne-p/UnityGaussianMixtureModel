#define PI 3.14159265359

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

float3x3 ReadMatrix3x3(RWStructuredBuffer<float3> buffer, in uint index)
{
    float3x3 m;
    m[0] = buffer[index * 3];
    m[1] = buffer[index * 3 + 1];
    m[2] = buffer[index * 3 + 2];
    return m;
}

void WriteMatrix3x3(RWStructuredBuffer<float3> buffer, in uint index, in float3x3 m)
{
    buffer[index * 3] = m[0];
    buffer[index * 3 + 1] = m[1];
    buffer[index * 3 + 2] = m[2];
}

float3x3 MakeCovarianceMatrix(in float3 r0, in float3 r1)
{
    float3x3 m;
    m[0] = r0;
    m[1] = float3(r0.y, r1.x, r1.y);
    m[2] = float3(r0.z, r1.y, r1.z);
    return m;
}

float3x3 ReadMatrixSymmetric3x3(StructuredBuffer<float3> buffer, in uint index)
{
    float3 r0 = buffer[index * 2];
    float3 r1 = buffer[index * 2 + 1];
    return MakeCovarianceMatrix(r0, r1);
}

float3x3 ReadMatrixSymmetric3x3(RWStructuredBuffer<float3> buffer, in uint index)
{
    float3 r0 = buffer[index * 2];
    float3 r1 = buffer[index * 2 + 1];
    return MakeCovarianceMatrix(r0, r1);
}

void WriteMatrixSymmetric3x3(RWStructuredBuffer<float3> buffer, in uint index, in float3x3 m)
{
    buffer[index * 2] = m[0];
    buffer[index * 2 + 1] = float3(m[1][1], m[1][2], m[2][2]);
}

float GaussianDensity(in float3 p, in float3 u, in float3x3 invCov, in float srqtDetReciprocal)
{
    float3 du = p - u;
    static const float gNorm = 1.0 / pow(2 * PI, 1.5);
    return gNorm * srqtDetReciprocal * exp(-0.5 * dot(mul(du, invCov), du));
}

float DeterminantSymmetric(in float3x3 m)
{
    return 2 * m[0][1] * m[0][2] * m[1][2]
             - m[0][0] * m[1][2] * m[1][2]
             + m[0][0] * m[1][1] * m[2][2]
             - m[0][1] * m[0][1] * m[2][2]
             - m[0][2] * m[0][2] * m[1][1];
}

float3x3 InvertSymmetric(in float3x3 m, float detReciprocal)
{
    float3x3 inv;
    inv[0][0] = (-m[1][2] * m[1][2] + m[1][1] * m[2][2]) * detReciprocal;
    inv[0][1] = ( m[0][2] * m[1][2] - m[0][1] * m[2][2]) * detReciprocal;
    inv[0][2] = (-m[0][2] * m[1][1] + m[0][1] * m[1][2]) * detReciprocal;
    inv[1][0] = inv[0][1];
    inv[1][1] = (-m[0][2] * m[0][2] + m[0][0] * m[2][2]) * detReciprocal;
    inv[1][2] = ( m[0][1] * m[0][2] - m[0][0] * m[1][2]) * detReciprocal;
    inv[2][0] = inv[0][2];
    inv[2][1] = inv[1][2];
    inv[2][2] = (-m[0][1] * m[0][1] + m[0][0] * m[1][1]) * detReciprocal;

    return inv;
}