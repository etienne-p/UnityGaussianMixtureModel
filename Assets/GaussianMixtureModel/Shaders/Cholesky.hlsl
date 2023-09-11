// Evaluate the Cholesky decomposition of a symmetric and positive-definite matrix.
float3x3 CholeskyCreate(in float3x3 A)
{
    uint i;
    float3x3 L = A;
    [unroll(3)]
    for(i = 0; i != 3; ++i)
    {
        [unroll(3)]
        for (uint j = i; j != 3; ++j)
        {
            float sum = L[i][j];

            [unroll(3)]
            for(int k = i - 1; k >= 0; --k)
            {
                sum -= L[i][k] * L[j][k];
            }
            
            if (i == j)
            {
                // sum should be positive.
                L[i][i] = sqrt(sum);
            }
            else
            {
                L[j][i] = sum / L[i][i];
            }
        }
    }

    [unroll(3)]
    for(i = 0; i != 3; ++i)
    {
        [unroll(2)]
        for (uint j = 0; j < i; ++j)
        {
            L[j][i] = 0;
        }
    }

    return L;
}

// Solve L.y = b.
void CholeskySolve(in float3x3 L, in float3 b, inout float3 y)
{
    [unroll(3)]
    for(uint i = 0; i != 3; ++i)
    {
        float sum = b[i];
        [unroll(2)]
        for(uint j = 0; j < i; ++j)
        {
            sum -= L[i][j] * y[j];
        }
        y[i] = sum / L[i][i];
    }
}

// Returns the logarithm of the determinant of the matrix whose Cholseky decomposition we pass.
float CholeskyLogDet(in float3x3 L)
{
    float3 trace = float3(L[0][0], L[1][1], L[2][2]);
    return 2 * dot((1).xxx, log(trace));
}

