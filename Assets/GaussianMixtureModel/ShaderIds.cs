using UnityEngine;

namespace GaussianMixtureModel
{
    // Grouping all Ids here allows us to eyeball them in one pass to look for spelling errors.
    static class ShaderIds
    {
        public static readonly int _NumClusters = Shader.PropertyToID("_NumClusters");
        public static readonly int _ColorBins = Shader.PropertyToID("_ColorBins");
        public static readonly int _SourceSize = Shader.PropertyToID("_SourceSize");
        public static readonly int _SourceTexture = Shader.PropertyToID("_SourceTexture");
        public static readonly int _AppendSelectedColorBins = Shader.PropertyToID("_AppendSelectedColorBins");
        public static readonly int _IndirectArgsBufferIn = Shader.PropertyToID("_IndirectArgsBufferIn");
        public static readonly int _IndirectArgsBufferOut = Shader.PropertyToID("_IndirectArgsBufferOut");
        public static readonly int _SelectedColorBins = Shader.PropertyToID("_SelectedColorBins");
        public static readonly int _IndirectArgsOffset = Shader.PropertyToID("_IndirectArgsOffset");
        public static readonly int _ClusterIndex = Shader.PropertyToID("_ClusterIndex");
        public static readonly int _Means = Shader.PropertyToID("_Means");
        public static readonly int _Covariances = Shader.PropertyToID("_Covariances");
        public static readonly int _Cholesky = Shader.PropertyToID("_Cholesky");
        public static readonly int _ViewProjection = Shader.PropertyToID("_ViewProjection");
        public static readonly int _Opacity = Shader.PropertyToID("_Opacity");
        public static readonly int _MaxBinSize = Shader.PropertyToID("_MaxBinSize");
        public static readonly int _TotalSamples = Shader.PropertyToID("_TotalSamples");

        public static readonly int _WeightsIn = Shader.PropertyToID("_WeightsIn");
        public static readonly int _WeightsOut = Shader.PropertyToID("_WeightsOut");
        public static readonly int _RespsIn = Shader.PropertyToID("_RespsIn");
        public static readonly int _RespsOut = Shader.PropertyToID("_RespsOut");
        public static readonly int _FracsIn = Shader.PropertyToID("_FracsIn");
        public static readonly int _FracsOut = Shader.PropertyToID("_FracsOut");
        public static readonly int _MeansIn = Shader.PropertyToID("_MeansIn");
        public static readonly int _MeansOut = Shader.PropertyToID("_MeansOut");
        public static readonly int _CovariancesIn = Shader.PropertyToID("_CovariancesIn");
        public static readonly int _CovariancesOut = Shader.PropertyToID("_CovariancesOut");
        public static readonly int _LnDetsIn = Shader.PropertyToID("_LnDetsIn");
        public static readonly int _LnDetsOut = Shader.PropertyToID("_LnDetsOut");
        public static readonly int _CholeskysIn = Shader.PropertyToID("_CholeskysIn");
        public static readonly int _CholeskysOut = Shader.PropertyToID("_CholeskysOut");
    }
}
