using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Content.Recommendations.LinearAlgebra;

namespace Microsoft.Content.Recommendations.Common
{
    public class VectorMatrix
    {
        private const char ColSeparatorTab = '\t';
        private const char ColSeparatorSpace = ' ';
        public int RowCount { get { return this.rowCount; } }
        public int RowsOnDisk { get { return this.rowsOnDisk; } }
        public string FilePath { get { return this.filePath; } }
        public bool Verbose { get; set; }

        private int rowCount = 0;
        private int rowsOnDisk = 0;
        private string filePath = string.Empty;
        private string PartitionSizesFilepath = string.Empty;
        private Tuple<string, DenseVector>[] Vectors = null;

        public VectorMatrix(string filePath, int rowsOnDisk, int maxRowsToRead)
        {
            this.Verbose = true;
            this.filePath = filePath;
            if (!File.Exists(filePath))
            {
                StatusMessage.Write("Error: Input document sample not found: " + filePath);
                return;
            }

            // Prep data structures ahead of time to report more realistic run times.
            // This is more memory intensive than it should, but we need to separate de-serialization time
            // from similarity compute time, and those 2 from the rest
            this.rowsOnDisk = rowsOnDisk;
            this.rowCount = Math.Min(rowsOnDisk, maxRowsToRead);
        }

        public VectorMatrix(string filePath, float sampleRate)
        {
            this.Verbose = true;
            this.filePath = filePath;
            if (!File.Exists(filePath))
            {
                StatusMessage.Write("Error: Input document sample not found: " + filePath);
                return;
            }

            this.rowCount = this.rowsOnDisk = File.ReadLines(filePath).Count();
            if (sampleRate > 0.0F)
            {
                this.rowCount = Math.Min((int)(sampleRate * this.rowsOnDisk), this.rowCount);
            }
        }

        public VectorMatrix(string filePath, bool lazyLoad)
        {
            this.Verbose = true;
            this.filePath = filePath;
            if (!File.Exists(filePath))
            {
                StatusMessage.Write("Error: Input document sample not found: " + filePath);
                return;
            }

            if (lazyLoad) this.rowCount = this.rowsOnDisk = File.ReadLines(filePath).Count();
        }

        public DenseVector this[int index]
        {
            get
            {
                return this.Vectors[index].Item2;
            }
        }

        public string ItemId(int itemIndex)
        {
            if ((this.Vectors != null) && (this.Vectors[itemIndex] != null))
            {
                return this.Vectors[itemIndex].Item1;
            }
            return string.Empty;
        }

        public int Flush()
        {
            if (this.Vectors != null)
            {
                this.Vectors = null;
                return this.RowCount;
            }
            return 0;
        }


        /// <summary>
        /// Find all files named 0.csv, 1.csv, etc. then get their vector count
        /// </summary>
        /// <param name="rootFolderPath"></param>
        /// <returns></returns>
        private Dictionary<int, int> RebuildPartitionsInfoFromPartitions(string rootFolderPath)
        {
            var partitionSizes = new Dictionary<int, int>();
            var partitionFilePaths = Directory.EnumerateFiles(rootFolderPath, @"*.csv");

            foreach (var filePath in partitionFilePaths)
            {
                // Get filename we/o extention
                string fileName = Path.GetFileName(filePath);
                fileName = fileName.Substring(0, fileName.Length - 4);

                int partitionId;
                if (int.TryParse(fileName, out partitionId))
                {
                    partitionSizes.Add(partitionId, File.ReadLines(filePath).Count());
                }
            }
            if (partitionSizes.Count > 0)
            {
                WritePartitionsInfo(partitionSizes);

                return partitionSizes;
            }

            return null;
        }
        /// <summary>
        /// If the vector matris has alrteady been partitioned load the size of each partition so we don't have to read each one and count lines again.
        /// </summary>
        private Dictionary<int, int> LoadPartitionsInfo()
        {
            string rootFolderPath = Directory.GetParent(this.FilePath).FullName;
            this.PartitionSizesFilepath = rootFolderPath + "\\ClusterSizes.txt";

            if (!File.Exists(this.PartitionSizesFilepath))
            {
                // See if at least the partions were created before, in which case read each file to calculte its vector counts
                return RebuildPartitionsInfoFromPartitions(rootFolderPath);
            }

            var partitionSizes = new Dictionary<int, int>();
            int row = 0;
            var clusterSizes = File.ReadLines(this.PartitionSizesFilepath);
            foreach (var clusterSize in clusterSizes)
            {
                partitionSizes.Add(row++, int.Parse(clusterSize));
            }

            return partitionSizes;
        }

        private void WritePartitionsInfo(Dictionary<int, int> partitionSizes)
        {
            if (partitionSizes == null) return;

            var partitionCount = partitionSizes.Count;
            using (var clusterSizesFile = new StreamWriter(this.PartitionSizesFilepath))
            {
                for (int partitionId = 0; partitionId < partitionCount; partitionId++)
                {
                    clusterSizesFile.WriteLine(partitionSizes[partitionId]);
                }
            }
        }
        public Dictionary<int, int> PartitionOnColunmIndex(int keyColIndex)
        {
            // It makes no sense to partition on the label or vector colunms
            if (keyColIndex == 0 || keyColIndex == 3) return null;

            // If we have already partitioned this vector matrix there is no need to do it again.
            var partitionSize = LoadPartitionsInfo();
            if (partitionSize != null)
            {
                return partitionSize;
            }
            else
            {
                partitionSize = new Dictionary<int, int>();
            }

            //int partitionCount = 0;
            string partitionKey = string.Empty;
            string prevKey = string.Empty;
            string outputPartitionPath = Directory.GetParent(this.FilePath).FullName;

            StreamWriter outputPartitionFile = null;
            int rowCount = 0;
            var vectors = File.ReadLines(this.FilePath);
            foreach (var vector in vectors)
            {
                // Vector file format has 4 space-separated colunms: col0 = label, col1 = market, col2 = domain, col3 = vector 
                char colSeparator = ColSeparatorTab;
                int col1Index = vector.IndexOf(colSeparator) + 1;
                if (col1Index < 0)
                {
                    colSeparator = ColSeparatorSpace;
                    col1Index = vector.IndexOf(colSeparator) + 1;

                }
                int col2Index = vector.IndexOf(colSeparator, col1Index) + 1;

                if (keyColIndex == 1)
                {
                    partitionKey = vector.Substring(col1Index, (col2Index - col1Index - 1));
                }
                else
                {
                    int vectorIndex = vector.IndexOf(colSeparator, col2Index) + 1;
                    partitionKey = vector.Substring(col2Index, (vectorIndex - col2Index - 1));
                }

                if (partitionKey != prevKey)
                {
                    //partitionCount++;
                    if (outputPartitionFile != null)
                    {
                        outputPartitionFile.Flush();
                        outputPartitionFile.Close();
                        outputPartitionFile.Dispose();
                        int partitionId;
                        if (int.TryParse(prevKey, out partitionId))
                            partitionSize.Add(partitionId, rowCount);
                        rowCount = 0;
                    }
                    outputPartitionFile = new StreamWriter(outputPartitionPath + "\\" + partitionKey + ".csv");
                    prevKey = partitionKey;
                }

                outputPartitionFile.WriteLine(vector);
                rowCount++;
            }
            if (outputPartitionFile != null)
            {
                outputPartitionFile.Flush();
                outputPartitionFile.Close();
                outputPartitionFile.Dispose();
                int partitionId;
                if (int.TryParse(prevKey, out partitionId))
                    partitionSize.Add(partitionId, rowCount);
            }

            // First item in dictionary is the number of partitions we found.
            //partitionSize.Add(0, partitionSize.Count);
            WritePartitionsInfo(partitionSize);

            return partitionSize;
        }
        public int Load()
        {
            // Do nothing if already loaded
            if (this.Vectors != null)
            {
                return 0;
            }

            if (this.RowCount > 0)
            {
                this.Vectors = new Tuple<string, DenseVector>[this.RowCount];
            }
            else
            {
                return 0;
            }

            var vectors = File.ReadLines(this.FilePath);
            string[] serialializedVectors = new string[this.RowCount];

            int i = 0;
            foreach (var vector in vectors)
            {
                // Vector file format has 4 space-separated colunms: label, market, domain, vector 
                char colSeparator = ColSeparatorTab;
                int labelDelimiterIndex = vector.IndexOf(colSeparator);
                // Tabs might not be teh delimeter.  Try spaces
                if (labelDelimiterIndex < 0)
                {
                    colSeparator = ColSeparatorSpace;
                    labelDelimiterIndex = vector.IndexOf(colSeparator);

                }
                string itemLabel = vector.Substring(0, labelDelimiterIndex);

                // Skip over the next two spaces
                int vectorStartIndex = vector.IndexOf(colSeparator, labelDelimiterIndex + 1);
                vectorStartIndex = vector.IndexOf(colSeparator, vectorStartIndex + 1) + 1;

                serialializedVectors[i] = vector.Substring(vectorStartIndex);
                this.Vectors[i] = new Tuple<string, DenseVector>(string.IsNullOrEmpty(itemLabel) ? i.ToString() : itemLabel, new DenseVector());
                i++;
                if (i >= this.RowCount) break;
            }

            if (this.Verbose) StatusMessage.Write("Deserializing vectors from " + this.FilePath);
            Stopwatch timer = new Stopwatch();
            timer.Start();

            for (i = 0; i < this.RowCount; i++)
            {
                this.Vectors[i].Item2.Deserialize(serialializedVectors[i]);
                this.Vectors[i].Item2.L2Normalize();
                serialializedVectors[i] = null; // No longer needed
            }

            timer.Stop();
            if (this.Verbose) System.Console.WriteLine("Deserialized {0} vectors. Elapsed time {1:D2}:{2:D2}:{3:F2}", this.RowCount, timer.Elapsed.Hours, timer.Elapsed.Minutes, (timer.Elapsed.TotalMilliseconds / 1000));
            if (this.Verbose) System.Console.WriteLine("Average {0:F4} milliseconds/vector.", timer.Elapsed.TotalMilliseconds / this.RowCount);

            return this.RowCount;

        }

        /// <summary>
        /// Computes de Euclidean distance between 2 unit vectors
        /// </summary>
        /// <param name="v1">Unit LDA topic vector.</param>
        /// <param name="v2">Unit LDA topic vector.</param>
        /// <returns></returns>
        public static double Distance(DenseVector v1, DenseVector v2)
        {
            return Math.Sqrt((2 - 2 * VectorBase.DotProduct(v1, v2)));
        }

        public enum VectorFunction { Distance, Similarity };

        /// <summary>
        /// Given a group of N vectors, compute NxN comparisions and return results in a matrix.
        /// The comparison operator can be Distance or Similarity
        /// </summary>
        /// <param name="func">The comparison function to apply between pairs of vectors: Euclidean Distance or Cosine Similarity</param>
        /// <param name="n">Limit comparisons to the top n vectors in the vector group.  If less than 1, compute comps for all vectors in vectorGroup.</param>
        /// <param name="maxCountItemsToCompare">For each of n vectors in the top, compare to the top maxCountItemsToCompare. 
        /// If less than 1, compare top n vs all vectors in vectorGroup.
        /// If both n and maxCountItemsToCompare are less than 1 a full NxN compas are computed where N = |vectorGroup|
        /// </param>
        /// <returns>A diagonal matrix of doubles.  Each cell encodes either the euclidean Distance or Cosine similarity between each vector pair.
        /// The bottom triangule is set to 0. 
        /// </returns>
        public double[][] GenerateSimDistMatrix(VectorFunction func, int n, int maxCountItemsToCompare)
        {
            if (this.RowCount <= 0)
            {
                return null;
            }

            // Are we being asked to compare top n vectors vs. only maxCountItemsToCompare vector?
            int vectorCount = ((maxCountItemsToCompare > 0) && (maxCountItemsToCompare < this.RowCount)) ? maxCountItemsToCompare : this.RowCount;

            // Create a diagonal matrix:  for document i we need to compute and store only (docCount - (i+1)) similarity or distance  scores
            // If asked to compare only the top n vectors againts the rest we only need to allocate a matrix of height n.
            // Otherwise  (n <= 0) we compare all (e.g. vectorCount) vs. all => need to allocate a matrix of height vectorCount

            int matrixHeight = (n > 0) ? n : vectorCount - 1;
            var funcMatrix = new double[matrixHeight][];
            for (int i = 0; i < matrixHeight; i++)
            {
                funcMatrix[i] = new double[vectorCount - (i + 1)];
            }

            if (this.Verbose) StatusMessage.Write(string.Format("Comparing top {0} vs. {1} other items...", matrixHeight, vectorCount - 1));

            var timer = new Stopwatch();
            timer.Start();

            for (int y = 0; y < matrixHeight; y++)
            {
                for (var x = 0; x < funcMatrix[y].Length; x++)
                {
                    if (func == VectorFunction.Distance)
                    {
                        funcMatrix[y][x] = Distance(this[y], this[x + (y + 1)]);
                    }
                    else
                    {
                        funcMatrix[y][x] = VectorBase.CosineSimilarity(this[y], this[x + (y + 1)]);
                    }
                }
            }

            timer.Stop();

            return funcMatrix;
        }


        public void PrintMatrix(double[][] matrix, StreamWriter stream)
        {
            this.PrintMatrix(matrix, false, stream, null);
        }

        public void PrintMatrix(double[][] matrix, bool printPadding, StreamWriter stream, VectorMatrix vectorMatrix2)
        {
            if ((matrix == null) || (matrix[0] == null))
            {
                return;
            }

            // Make room for each line of output: One tab char + 6 digits of precission after "0."
            int maxRowCharLength = (matrix[0].Length + 1) * 9;
            StringBuilder rowValues = new StringBuilder(maxRowCharLength);

            // If we are not called with a second set of vectors then we are simply comparing this[] in  N x  N fashion.
            bool isNxM = (vectorMatrix2 == null);
            for (int curRow = 0; curRow < matrix.Length; curRow++)
            {
                rowValues.Clear();
                if ((matrix[curRow] == null) || (matrix[curRow].Length < 1))
                {
                    throw new InvalidDataException(string.Format("Similarity/Distance matrix is missing data at row {0}", curRow));
                }

                var rowId = this.Vectors[curRow].Item1;
                if (isNxM && printPadding)
                {
                    // Output row Id only once
                    rowValues.Append(curRow);
                    // ...followed by 0 padding of bottom left corner of the triangular matrix
                    for (int i = 0; i <= curRow; i++)
                    {
                        rowValues.AppendFormat("\t{0:F1}", 0.0D);
                    }
                }

                for (int curCol = 0; curCol < matrix[curRow].Length; curCol++)
                {
                    string colId = string.Empty;
                    if (isNxM)
                    {
                        colId = this.Vectors[curCol + curRow + 1].Item1;
                    }
                    else
                    {
                        rowId = this.Vectors[curCol].Item1;
                        colId = vectorMatrix2.Vectors[curCol].Item1;
                    }

                    if (!printPadding)
                    {
                        // Output row and col Ids with every line  (i.e. not using diagonal matrix format)
                        rowValues.AppendFormat("{0}\t{1}", rowId, colId);
                    }

                    rowValues.AppendFormat("\t{0:F6}", matrix[curRow][curCol]);
                }

                stream.WriteLine(rowValues.ToString());
            }
        }
    }
   

}
