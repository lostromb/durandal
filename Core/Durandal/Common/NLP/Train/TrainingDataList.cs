using System;
using System.Collections.Generic;
using System.IO;
using Durandal.Common.Logger;
using Durandal.Common.NLP.Feature;

namespace Durandal.Common.NLP.Train
{
    using Durandal.Common.Collections;
    using Durandal.Common.File;

    public class TrainingDataList<T> where T : ITrainingFeature
    {
        private List<T> data;
        private CreateTrainingFeature activatorDelegate;

        public delegate T CreateTrainingFeature(string dataLine);

        public TrainingDataList(CreateTrainingFeature method)
        {
            data = new List<T>();
            activatorDelegate = method;
        }

        public TrainingDataList(VirtualPath fileNameToLoad, IFileSystem fileSystem, ILogger logger, CreateTrainingFeature method)
        {
            try
            {
                data = new List<T>();
                activatorDelegate = method;
                IEnumerable<string> trainingData = fileSystem.ReadLines(fileNameToLoad);
                foreach (string nextLine in trainingData)
                {
                    T newFeature = activatorDelegate(nextLine);
                    if (newFeature != null)
                    {
                        data.Add(newFeature);
                    }
                    else
                    {
                        logger.Log("Could not parse training instance " + nextLine, LogLevel.Err);
                    }
                }

                // This variant uses streams so it's much more memory-efficient, but for some reason
                // the file handles conflict (?) and cause huge spinlocks during training
                /*using (StreamReader fileIn = new StreamReader(fileSystem.ReadStream(fileNameToLoad)))
                {
                    while (!fileIn.EndOfStream)
                    {
                        string nextLine = fileIn.ReadLine();
                        T newFeature = activatorDelegate(nextLine);
                        if (newFeature != null)
                        {
                            data.Add(newFeature);
                        }
                        else
                        {
                            logger.Log("Could not parse training instance " + nextLine, LogLevel.Err);
                        }
                    }
                    fileIn.Close();
                }*/
            }
            catch (Exception e)
            {
                logger.Log("Exception in training data: " + e.Message, LogLevel.Err);
            }
        }

        public List<T> TrainingData
        {
            get
            {
                return data;
            }
        }

        public void Append(TrainingDataList<T> other)
        {
            data.FastAddRangeList(other.TrainingData);
        }

        public void Append(T feature)
        {
            data.Add(feature);
        }

        public void SaveToFile(VirtualPath path, IFileSystem fileSystem)
        {
            using (StreamWriter fileOut = new StreamWriter(fileSystem.OpenStream(path, FileOpenMode.Create, FileAccessMode.Write)))
            {
                foreach (T item in data)
                {
                    fileOut.WriteLine(item.ToString());
                }

                fileOut.Dispose();
            }
        }

        public override int GetHashCode()
        {
            int returnVal = 0;
            foreach (T x in data)
            {
                returnVal += x.GetHashCode();
            }
            return returnVal;
        }
    }
}
