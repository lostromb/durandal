namespace Durandal.Common.NLP.Annotation
{
    using Durandal.API;

    using Durandal.Common.Config;
    using Durandal.Common.Logger;
    using File;
    using Durandal.Common.Ontology;
    using System.Threading.Tasks;
    using Durandal.Common.IO;
    using Time;
    using System.Threading;

    public interface IAnnotator
    {
        /// <summary>
        /// The name of this annotator
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Initializes the annotator, giving it time to set up any external data sources it may need.
        /// </summary>
        /// <returns>True if initialization succeeded</returns>
        bool Initialize();

        /// <summary>
        /// Calculates annotation data for this reco result statelessly. It MUST NOT modify the reco result object
        /// because many annotators may be operating on the same data concurrently.
        /// The implementation of this method must be thread-safe (i.e. multiple threads may be running annotation at once)
        /// </summary>
        /// <param name="input">The classification results for a single domain</param>
        /// <param name="originalRequest">The original LU request</param>
        /// <param name="modelConfig">The current domain model's configuration</param>
        /// <param name="queryLogger">A query logger</param>
        /// <param name="cancelToken">A cancel token for the annotator operation</param>
        /// <param name="realTime">Definition of real time</param>
        /// <returns>State information that should be used by this plugin later to commit the annotation</returns>
        Task<object> AnnotateStateless(
            RecoResult input,
            LURequest originalRequest,
            IConfiguration modelConfig,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime);

        /// <summary>
        /// Applies earlier annotation data to a reco result transactionally.
        /// </summary>
        /// <param name="asyncState">Results that were generated from this annotator's previous call to AnnotateStateless</param>
        /// <param name="result">The classification results that you are annotating (you are modifying this object and you have exclusive ownership of it)</param>
        /// <param name="originalRequest">The original LU request</param>
        /// <param name="entityContext">A context to store resolved entities</param>
        /// <param name="modelConfig">The current domain model's configuration</param>
        /// <param name="queryLogger">A query logger</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task.</returns>
        Task CommitAnnotation(
            object asyncState,
            RecoResult result,
            LURequest originalRequest,
            KnowledgeContext entityContext,
            IConfiguration modelConfig,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime);

        /// <summary>
        /// Hints to this annotator that the model has been reloaded
        /// </summary>
        void Reset();
    }
}
