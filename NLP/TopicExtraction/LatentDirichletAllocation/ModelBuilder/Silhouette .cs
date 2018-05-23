using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Content.Recommendations.LinearAlgebra;
using System.Threading;


namespace Microsoft.Content.Recommendations.Common
{

    struct SilhouetteStats
    {
        /// <summary>
        /// Dissimilarity of vector to its cluster
        /// </summary>
        public double a;

        /// <summary>
        /// Dissimilarity of vector to its nearest non-parent cluster
        /// </summary>
        public double b;

        public double Silhouette;
    }

    class Silhouette
    {
        /// <summary>
        /// Use this many threds to compute vector distances. Default is 8.
        /// </summary>
        public int MaxThreads
        {
            get 
            { 
                return this.maxThreads; 
            } 
            set 
            {
                if (value < 1)
                    this.maxThreads = 8;
                else
                {
                    this.maxThreads = value;
                }
            }
        }

        private int maxThreads = 8;
        
        /// <summary>
        /// For each cluster we keep track of its vectors' silhouette stats
        /// </summary>
        private SilhouetteStats[][] VectorSilhouetteStats;
        private SilhouetteStats[] SihouetteSummary;

        private VectorMatrix[] Clusters;
        private int VectorsThatFit = 0;

        // ToDo: Make BytesAvailable a parameter
        private const int OneGBytesAvailable = 1073741824;  // 1 Gig

        /// <summary>
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="sampleRate">
        /// Compute silhouette on a sample of vectors per cluster os size sampleRate%.  e.g. 0.01 = 1%.
        /// Default = 0.01.
        /// If teh sample rte is so small than 
        /// </param>
        /// <param name="maxMemory">
        /// Number of gigabytes of RAM to reserve for clusters cached in memory.  
        /// Go to disk for those that do not fit. 
        /// Default = 2.0 Gigs.
        /// </param>
        public Silhouette(string filePath, float sampleRate, float maxMemory)
        {
            VectorMatrix userClusters = new VectorMatrix(filePath, false);

            // Split the big User-ClusterId-Vector stream into smaller files, one per cluster
            var clusterCounts = userClusters.PartitionOnColunmIndex(2);
            string rootFolderPath = Directory.GetParent(filePath).FullName;

            int clusterCount = clusterCounts.Count;
            Clusters = new VectorMatrix[clusterCount];
            int totalVectorsInMemory = 0;
            for (int ci = 0; ci < clusterCount; ci++)
            {
                int rowsOnDisk = clusterCounts[ci];
                int maxRowsToLoad = ((sampleRate > 0.0F) && (sampleRate <= 1.0F)) ? (int)(sampleRate * rowsOnDisk) : rowsOnDisk;

                // Make sure we at least load 500 rows  (or the actual number of rows on disk)
                if (maxRowsToLoad < 500)
                {
                    maxRowsToLoad = Math.Min(rowsOnDisk, 500);
                }
                totalVectorsInMemory += maxRowsToLoad;
                Clusters[ci] = new VectorMatrix(rootFolderPath + "\\" + ci + ".csv", rowsOnDisk, maxRowsToLoad);
                Clusters[ci].Verbose = false;
            }

            // Load first partition to estimate the size of vectors
            Clusters[0].Load();
            // Only need a rough estimate :)
            int bytesPerVector = Clusters[0][0].Length * sizeof(float) + 20 + 4 + 4;

            // How many vectors can we fit in 3 x 1GBytes ?
            if (maxMemory <= 0.0F)
            {
                maxMemory = 3.0F;
            }
            this.VectorsThatFit = (int)(maxMemory * (OneGBytesAvailable / bytesPerVector));
            // Subtract the ones already in memory (cluster 0)
            this.VectorsThatFit -= Clusters[0].RowCount;
        }

        /// <summary>
        /// Compute avg Silhouettes for the loaded Clusters
        /// </summary>
        /// <param name="maxClusters">If > 0 we run only on the top maxClusters, for limited tests.  
        /// In this case b(i) is based only on proximinity to the top maxClusters, so it will be understimated.
        /// Therefore when using this param you can only trust the a(i) values. 
        /// </param>
        public void ComputeSilhouettes(int maxClusters)
        {
            int clusterCount = Clusters.Length;
            if (maxClusters > 0)
                clusterCount = Math.Min(clusterCount, maxClusters);

            // Make room to store the average silhouettes for each cluster
            SihouetteSummary = new SilhouetteStats[clusterCount];
            // And for each cluster's vectors' silhouette info
            VectorSilhouetteStats = new SilhouetteStats[clusterCount][];

            System.Console.WriteLine("# Cluster\tSize\tSample Size\tAvg Silhouette\ta\tb");
            for (int ci = 0; ci < clusterCount; ci++)
            {
                var curCluster = Clusters[ci];
                this.VectorsThatFit -= curCluster.Load();

                // Pre-compute a triangular matrix of Intra-cluster vector distances: a total of (n-1)n/2 distances
                var distMatrix = curCluster.GenerateSimDistMatrix(VectorMatrix.VectorFunction.Distance, 0, 0);

                // Update the Silhouette a(i) avg distance measure for each vector in the cluster
                SilhouetteStats[] Sci = CoumputeAvgIntraClusterDistances(curCluster, distMatrix);
                int vectorCount = Sci.Length;

                // Then update b(i), the min avg distance from each vector in this cluster to vectors in other clusters
                for (int neighborClusterId = 0; neighborClusterId < clusterCount; neighborClusterId++)
                {
                    if (neighborClusterId != ci)
                    {
                        this.VectorsThatFit -= Clusters[neighborClusterId].Load();
                        Task<double>[] distanceTasks = new Task<double>[vectorCount];
                        using (var semaphore = new SemaphoreSlim(this.MaxThreads))
                        {
                            for (int vi = 0; vi < vectorCount; vi++)
                            {
                                DenseVector vector = curCluster[vi];

                                distanceTasks[vi] = Task<double>.Run(async () =>
                                {
                                    await semaphore.WaitAsync();
                                    try
                                    {
                                        return AvgDistanceToCluster(vector, neighborClusterId);
                                    }
                                    finally
                                    {
                                        semaphore.Release();
                                    }
                                });
                            }

                            Task.WaitAll(distanceTasks);
                        }

                        for (int vi = 0; vi < vectorCount; vi++)
                        {
                            Sci[vi].b = Math.Min(Sci[vi].b, distanceTasks[vi].Result);
                        }

                        // Keep neighbor cluster loaded if we are about to use it on the next iteration of ci
                        if ((this.VectorsThatFit < 0) && (neighborClusterId != (ci + 1)))
                        {
                            this.VectorsThatFit += Clusters[neighborClusterId].Flush();
                        }
                    }
                }

                // Compute Cluster's avg. silhouette metric
                double silhouette, avgA, avgB;
                silhouette = avgA = avgB = 0.0d;
                for (int vi = 0; vi < vectorCount; vi++)
                {
                    var a = Sci[vi].a;
                    var b = Sci[vi].b;
                    avgA += a;
                    avgB += b;

                    if ((a == 0.0D) && (b == 0.0))
                    {
                        Sci[vi].Silhouette = 0.0D;
                    }
                    else
                    {
                        Sci[vi].Silhouette = (b - a) / Math.Max(a, b);
                        silhouette += Sci[vi].Silhouette;
                    }
                }
                VectorSilhouetteStats[ci] = Sci;

                SihouetteSummary[ci].Silhouette = silhouette / vectorCount;
                SihouetteSummary[ci].a = avgA / vectorCount;
                SihouetteSummary[ci].b = avgB / vectorCount;

                System.Console.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", ci, Clusters[ci].RowsOnDisk, vectorCount, SihouetteSummary[ci].Silhouette, SihouetteSummary[ci].a, SihouetteSummary[ci].b);
            }
        }

        private double AvgDistanceToCluster(DenseVector vector, int clusterId)
        {
            double dist = 0.0D;

            var cluster = Clusters[clusterId];
            int vectorCount = cluster.RowCount;
            for (int vi = 0; vi < vectorCount; vi++)
            {
                dist += VectorMatrix.Distance(vector, cluster[vi]);
            }

            return dist / vectorCount;
        }

        /// <summary>
        /// Sum distances from vector i to all its neighboors in its cluster.  
        /// distMatrix in a triangular matrix:  Bottom left conner is zeroed out
        /// </summary>
        /// <param name="vi">Index of vector in the matrix we want to compute an avg distance for.</param>
        /// <param name="distMatrix"></param>
        /// <returns></returns>
        private double AvgDistance(int vi, double[][] distMatrix)
        {
            if ((distMatrix == null) || (distMatrix.Length == 0))
                return 0.0D;

            double avgDistance = 0.0D;

            // Since the left bottom corner of the matrix is full of zeros (this is a triangular mirror matrix), 
            // we need to walk and sum over the mirror top-right corner for the first vi records              
            for (int row = 0; row < vi; row++)
            {
                avgDistance += distMatrix[row][(vi - 1) - row];
            }

            if (vi >= distMatrix.Length)
            {
                return avgDistance / vi;
            }

            // Now continue summin over the rest of the row
            int n = distMatrix[vi].Length;
            for (int col = 0; col < n; col++)
            {
                avgDistance += distMatrix[vi][col];
            }
            return avgDistance / (vi + n);
        }

        /// <summary>
        /// Computes Intra-cluster distance for each vector in a cluster 
        /// </summary>
        /// <returns></returns>
        private SilhouetteStats[] CoumputeAvgIntraClusterDistances(VectorMatrix cluster, double[][] distMatrix)
        {
            var silhouettes = new SilhouetteStats[cluster.RowCount];

            for (int vi = 0; vi < cluster.RowCount; vi++)
            {
                silhouettes[vi].a = AvgDistance(vi, distMatrix);
                silhouettes[vi].b = Double.MaxValue;
            }
            return silhouettes;
        }

        /// <summary>
        /// For each vector in every cluster output its Silhouette, a(i) and b(i) metrics
        /// </summary>
        /// <param name="printVectorSilhouettes"></param>
        public void PrintSilhouettes()
        {
            int clusterId = 0;
            foreach (var cluster in VectorSilhouetteStats)
            {
                // Print averages for current cluster
                System.Console.WriteLine("# Cluster {0} Avg Silhouette = {1}", clusterId, SihouetteSummary[clusterId].Silhouette);

                int vectorId = 0;
                foreach (var vectorSilhouette in cluster)
                {
                    System.Console.WriteLine("{0}\t{1}\t{2}", clusterId, vectorId, vectorSilhouette.Silhouette);
                    vectorId++;
                }
                clusterId++;
            }
        }

    }
}
