using Durandal.Common.Audio;
using Durandal.Common.Audio.FFT;
using Stromberg.Utils.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers
{
    public class SpectralImage
    {
        private const int WINDOW_SIZE = 512;
        private const int MAX_LENGTH = 30;
        private IList<float[]> _buffers = new List<float[]>();

        public int Length
        {
            get
            {
                return _buffers.Count;
            }
        }

        public int Height
        {
            get
            {
                if (this.Length == 0)
                    return 0;
                return _buffers[0].Length;
            }
        }

        public float[] GetBuffer(int index)
        {
            return _buffers[index];
        }

        public float[][] ToMatrix()
        {
            float[][] returnVal = new float[_buffers.Count][];
            for (int c = 0; c < returnVal.Length; c++)
            {
                returnVal[c] = _buffers[c];
            }

            return returnVal;
        }

        public void Add(float[] vector)
        {
            if (_buffers.Count >= MAX_LENGTH)
            {
                _buffers.RemoveAt(0);
            }
            _buffers.Add(vector);
        }

        ///writes a .tsv file for debugging
        /*public void WriteToFile(string fileName)
        {
            using (StreamWriter fileOut = new StreamWriter(fileName))
            {
                foreach (float[] buffer in _buffers)
                {
                    fileOut.WriteLine(string.Join("\t", buffer));
                }
                fileOut.Close();
            }
        }*/

        public void WriteAsImage(string fileName)
        {
            int hStretch = 8;
            int vStretch = 4;
            Bitmap image = new Bitmap(Length * hStretch, Height * vStretch);
            for (int x = 0; x < Length; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    float val = Math.Min(255, Math.Max(0, _buffers[x][y] * 255));
                    Color color = Color.FromArgb((int)(255 - val), (int)(255 - val), (int)(255 - val));
                    for (int z = 0; z < hStretch; z++)
                    {
                        for (int w = 0; w < vStretch; w++)
                        {
                            image.SetPixel((hStretch * x) + z, ((Height - y - 1) * vStretch) + w, color);
                        }
                    }
                }
            }
            image.Save(fileName, ImageFormat.Png);
        }

        public static SpectralImage BuildFromFile(FileInfo fileName)
        {
            if (!File.Exists(fileName.FullName))
            {
                return null;
            }

            AudioChunk audio = AudioChunkFactory.CreateFromFile(fileName.FullName);
            return BuildFromAudio(audio);
        }

        public static SpectralImage BuildFromFile(IResourceManager resourceManager, ResourceName fileName)
        {
            if (!resourceManager.Exists(fileName))
            {
                return null;
            }

            AudioChunk audio = AudioChunkFactory.CreateFromWavStream(resourceManager.ReadStream(fileName));
            return BuildFromAudio(audio);
        }

        public static SpectralImage BuildFromAudio(AudioChunk audio)
        {
            SpectralImage returnVal = new SpectralImage();
            short[] buffer = new short[WINDOW_SIZE];
            float[] suppression = GenerateSuppressionVector(WINDOW_SIZE);
            int cursor = 0;
            while (cursor < audio.DataLength - WINDOW_SIZE)
            {
                Array.Copy(audio.Data, cursor, buffer, 0, WINDOW_SIZE);
                float[] vector = GetVector(buffer, suppression);
                returnVal.Add(vector);
                cursor += WINDOW_SIZE / 2;
            }
            returnVal.Normalize();

            return returnVal;
        }

        public static float[] GetVector(short[] inputAudio, float[] suppressionVector)
        {
            if (inputAudio.Length != WINDOW_SIZE)
            {
                throw new ArgumentException("SpectralTrigger requires input audio slices to be exactly " + WINDOW_SIZE + " samples long");
            }

            ComplexF[] inputVector = new ComplexF[inputAudio.Length];

            for (int c = 0; c < WINDOW_SIZE; c++)
            {
                float real = (float)inputAudio[c] / (float)short.MaxValue;
                // Apply a window function
                real = real * (float)AudioMath.NuttallWindow((double)c / WINDOW_SIZE);
                inputVector[c] = new ComplexF(real, 0);
            }

            float percentToKeep = 0.5f * 0.25f;
            float normalizer = 15f;
            float averageDecay = 0.99f;

            Fourier.FFT(inputVector, FourierDirection.Forward);

            float[] magnitudes = new float[(int)(WINDOW_SIZE * percentToKeep)];

            for (int c = 0; c < magnitudes.Length; c++)
            {
                magnitudes[c] = inputVector[c].GetModulus();
                // Apply the "noise filter" using a moving average array
                suppressionVector[c] = (suppressionVector[c] * averageDecay) + (magnitudes[c] * (1 - averageDecay));
                magnitudes[c] = Math.Max(0, magnitudes[c] - suppressionVector[c]) / normalizer;
            }

            return magnitudes;
        }

        private void Normalize()
        {
            float maxValue = 0;
            foreach (float[] vector in _buffers)
            {
                for (int c = 0; c < vector.Length; c++)
                {
                    if (vector[c] > maxValue)
                        maxValue = vector[c];
                }
            }
            if (maxValue >= 0)
            {
                foreach (float[] vector in _buffers)
                {
                    for (int c = 0; c < vector.Length; c++)
                    {
                        vector[c] /= maxValue;
                    }
                }
            }
        }

        /// <summary>
        /// Initializes the moving average array using a power decay function that approximates
        /// a slightly noisy curve in fourier space
        /// </summary>
        /// <param name="windowSize"></param>
        /// <returns></returns>
        public static float[] GenerateSuppressionVector(int windowSize)
        {
            float[] returnVal = new float[windowSize];
            for (int c = 0; c < windowSize; c++)
            {
                returnVal[c] = (float)Math.Pow((12 / Math.Pow((c + 1), 3)), 2);
            }
            return returnVal;
        }

        public float GetDifference(SpectralImage other)
        {
            if (this.Height != other.Height)
            {
                throw new ArgumentException("Images to compare have mismatched height");
            }

            int length = Math.Min(this.Length, other.Length);
            if (length == 0)
            {
                return 0;
            }

            int height = this.Height;
            
            float difference = 0;
            for (int x = 0; x < length; x++)
            {
                float[] otherBuf = other.GetBuffer(other.Length - x - 1);
                float[] thisBuf = _buffers[this.Length - x - 1];
                for (int y = 0; y < height; y++)
                {
                    difference += Math.Max(0, otherBuf[y] - thisBuf[y]);
                }
            }

            difference /= (float)length;

            return difference;
        }

        public static SpectralImage AlignImages(SpectralImage one, SpectralImage two, out float residualDif, out float vectorMotion)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            
            if (one.Height != two.Height)
            {
                throw new ArgumentException("Images to align have mismatched height");
            }

            // Make image 1 always be the longer image, to save us from lots of bounds checks later on
            if (two.Length > one.Length)
            {
                Console.WriteLine("Image two is longer than image one, swapping them...");
                SpectralImage temp = one;
                one = two;
                two = temp;
            }

            int w = two.Length;
            int h = one.Height;
            
            float[][] imageA = one.ToMatrix();
            float[][] imageB = two.ToMatrix();

            // How many slices image A is larger than image B
            int imageAGreaterLength = one.Length - two.Length;

            float[][] alignedImage = new float[w][];
            for (int c = 0; c < w; c++)
            {
                alignedImage[c] = new float[h];
            }

            // Subdivide into blocks
            int blockHeight = 5;
            int blockWidth = 3;
            int blockResX = Math.Max(1, w / blockWidth);
            int blockResY = Math.Max(1, h / blockHeight);
            int[][] vectorsX = new int[blockResX][];
            int[][] vectorsY = new int[blockResX][];
            for (int c = 0; c < blockResX; c++)
            {
                vectorsX[c] = new int[blockResY];
                vectorsY[c] = new int[blockResY];
            }

            // Step 1: Align by global offset along time-axis
            //Console.WriteLine("Performing time-axis alignment...");
            float bestResidual = float.MaxValue;
            int bestOffset = 0;
            const int MAX_TIME_AXIS_OFFSET = 5;

            for (int offset = 0 - MAX_TIME_AXIS_OFFSET; offset <= MAX_TIME_AXIS_OFFSET; offset++)
            {
                float residual = 0;
                float cellCount = 0.1f;

                for (int x = imageAGreaterLength; x < w + imageAGreaterLength; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        float a = 0;
                        float b = 0;
                        if (x + offset >= 0 && x + offset < imageA.Length)
                        {
                            a = imageA[x + offset][y];
                            cellCount += 1;
                        }
                        b = imageB[x][y];

                        residual += Math.Abs(a - b);
                    }
                }

                residual /= cellCount;

                if (residual < bestResidual)
                {
                    bestResidual = residual;
                    bestOffset = 0 - offset;
                }
            }

            int linearShift = bestOffset;

            //Console.WriteLine("Found optimum time-axis alignment: {0}", linearShift);
            
            const int SEARCH_PATTERN_SIZE = 1;

            // If the block's initial residual is below this, don't bother aligning it
            const float EMPTY_BLOCK_THRESHOLD = 0.1f;
            
            // Now iterate through all blocks in the field and find optimal alignments for each one
            for (int blockX = 0; blockX < blockResX; blockX++)
            {
                for (int blockY = 0; blockY < blockResY; blockY++)
                {
                    bestResidual = 0;
                    float cellsProcessed = 0;
                    int bestXOffset = 0;
                    int bestYOffset = 0;
                    int originX = (blockX * blockWidth);
                    int originY = (blockY * blockHeight);

                    // Evaluate the initial difference.
                    for (int x = 0; x < blockWidth; x++)
                    {
                        for (int y = 0; y < blockHeight; y++)
                        {
                            int sourceX = x + originX;
                            int sourceY = y + originY;
                            int targetX = sourceX + linearShift + imageAGreaterLength;
                            if (targetX >= 0 && targetX < w && sourceY >= 0 && sourceY < h)
                            {
                                bestResidual += Math.Abs(imageB[sourceX][sourceY] - imageA[targetX][sourceY]);
                            }
                        }
                    }

                    if (bestResidual < EMPTY_BLOCK_THRESHOLD)
                    {
                        // Block is not significant enough; skip alignment
                        vectorsX[blockX][blockY] = 0 - linearShift;
                        vectorsY[blockX][blockY] = 0;
                        //Console.Write(" 0, 0\t");
                        continue;
                    }

                    // No fancy search protocol here - just search a square region exhaustively to find the best
                    // place to fit this block
                    for (int offsetX = 0 - SEARCH_PATTERN_SIZE; offsetX <= SEARCH_PATTERN_SIZE; offsetX++)
                    {
                        for (int offsetY = 0 - SEARCH_PATTERN_SIZE; offsetY <= SEARCH_PATTERN_SIZE; offsetY++)
                        {
                            if (offsetX == 0 && offsetY == 0)
                                continue;

                            float residual = 0;
                            cellsProcessed = 0;
                            
                            // Evaluate this alignment
                            for (int x = 0; x < blockWidth; x++)
                            {
                                for (int y = 0; y < blockHeight; y++)
                                {
                                    int sourceX = x + originX;
                                    int sourceY = y + originY;
                                    int targetX = offsetX + sourceX - linearShift + imageAGreaterLength;
                                    int targetY = offsetY + sourceY;
                                    if (targetX >= 0 && targetX < w && targetY >= 0 && targetY < h && sourceX < w && sourceY < h)
                                    {
                                        residual += Math.Abs(imageB[sourceX][sourceY] - imageA[targetX][targetY]);
                                        cellsProcessed += 1;
                                    }
                                }
                            }

                            if (cellsProcessed > 0)
                            {
                                residual /= cellsProcessed;
                                if (residual < bestResidual)
                                {
                                    //Console.WriteLine("({0} {1}) has residual of {2}", offsetX, offsetY, residual);
                                    bestResidual = residual;
                                    bestXOffset = offsetX;
                                    bestYOffset = offsetY;
                                }
                            }
                        }
                    }

                    vectorsX[blockX][blockY] = bestXOffset - linearShift;
                    vectorsY[blockX][blockY] = bestYOffset;
                    //Console.Write("{0:D2},{1:D2}\t", vectorsX[blockX][blockY], vectorsY[blockX][blockY]);
                }
                //Console.WriteLine();
            }
            
            // Sum up total vector motion for all blocks
            vectorMotion = 0.0f;
            for (int x = 0; x < blockResX; x++)
            {
                for (int y = 0; y < blockResY; y++)
                {
                    vectorMotion += vectorsX[x][y] + vectorsY[x][y];
                }
            }

            // Build the aligned image
            for (int blockX = 0; blockX < blockResX; blockX++)
            {
                for (int blockY = 0; blockY < blockResY; blockY++)
                {
                    for (int x = 0; x < blockWidth; x++)
                    {
                        for (int y = 0; y < blockHeight; y++)
                        {
                            int sourceX = x + (blockX * blockWidth);
                            int sourceY = y + (blockY * blockHeight);
                            if (sourceX < w && sourceY < h)
                            {
                                int targetX = sourceX - vectorsX[blockX][blockY];
                                int targetY = sourceY - vectorsY[blockX][blockY];
                                if (targetX >= 0 && targetX < w && targetY >= 0 && targetY < h && sourceX < w && sourceY < h)
                                {
                                    alignedImage[targetX][targetY] += imageA[sourceX][sourceY];
                                }
                            }
                        }
                    }
                }
            }

            // Calculate the final residual (error) to evaluate this alignment
            residualDif = 0;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    residualDif += Math.Abs(alignedImage[x][y] - imageB[x][y]);
                }
            }

            SpectralImage returnVal = new SpectralImage();
            for (int c = 0; c < w; c++)
            {
                returnVal._buffers.Add(alignedImage[c]);
            }

            timer.Stop();
            Console.WriteLine("Alignment time (ms): {0}", timer.ElapsedMilliseconds);
            Console.WriteLine("Final values: residual {0} motion {1}", residualDif, vectorMotion);

            return returnVal;
        }

        public static float WaveletCompareWithAlignment(SpectralImage one, SpectralImage two, int vectorsToUse = 4)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            if (one.Height != two.Height)
            {
                throw new ArgumentException("Images to align have mismatched height");
            }

            // Make image 1 always be the longer image, to save us from lots of bounds checks later on
            if (two.Length > one.Length)
            {
                //Console.WriteLine("Image two is longer than image one, swapping them...");
                SpectralImage temp = one;
                one = two;
                two = temp;
            }

            int w = two.Length;
            int h = one.Height;

            float[][] imageA = one.ToMatrix();
            float[][] imageB = two.ToMatrix();

            // How many slices image A is larger than image B
            int imageAGreaterLength = one.Length - two.Length;

            // Align by global offset along time-axis
            //Console.WriteLine("Performing time-axis alignment...");
            float bestResidual = float.MaxValue;
            int linearShift = 0;
            const int MAX_TIME_AXIS_OFFSET = 5;

            for (int offset = 0 - MAX_TIME_AXIS_OFFSET; offset <= MAX_TIME_AXIS_OFFSET; offset++)
            {
                float residual = 0;
                float cellCount = 0.1f;

                for (int x = imageAGreaterLength; x < w + imageAGreaterLength; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        float a = 0;
                        float b = 0;
                        if (x + offset >= 0 && x + offset < imageA.Length)
                        {
                            a = imageA[x + offset][y];
                            cellCount += 1;
                        }
                        b = imageB[x - imageAGreaterLength][y];

                        // don't use Abs() here because it is a heavy function
                        if (a > b)
                            residual += a - b;
                        else
                            residual += b - a;
                    }
                }

                residual /= cellCount;

                if (residual < bestResidual)
                {
                    bestResidual = residual;
                    linearShift = 0 - offset;
                }
            }

            float[][] trimmedShiftedImageA = new float[w][];
            for (int c = 0; c < w; c++)
            {
                trimmedShiftedImageA[c] = new float[h];
            }

            for (int x = 0; x < w; x++)
            {
                int offsetX = x - linearShift + imageAGreaterLength;
                if (offsetX >= 0 && offsetX < w)
                {
                    for (int y = 0; y < h; y++)
                    {
                        trimmedShiftedImageA[x][y] = imageA[offsetX][y];
                    }
                }
            }

            // Use that offset to construct two images to pass into Haar alignment
            WaveletDecomposition waveletA = HaarWavelet.Transform(trimmedShiftedImageA);
            WaveletDecomposition waveletB = HaarWavelet.Transform(imageB);

            float returnVal = 0.0f;
            
            if (waveletA != null && waveletB != null)
            {
                returnVal = waveletA.CalculateDifference(waveletB, vectorsToUse);
            }

            timer.Stop();
            //Console.WriteLine("Alignment time (ms): {0}", timer.ElapsedMilliseconds);
            //Console.WriteLine("Final difference: " + returnVal);

            return returnVal;
        }
    }
}
