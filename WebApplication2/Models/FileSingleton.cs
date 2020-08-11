using System.Collections.Generic;

namespace WebApplication2.Models
{
    public class FileSingleton
    {
        private static object _lock = new object();
        private static FileSingleton instance;
        private readonly List<string> MergeFileList;

        private FileSingleton()
        {
            MergeFileList = new List<string>();
        }

        public static FileSingleton Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new FileSingleton();
                    }
                }
                return instance;
            }
        }

        public void AddFile(string baseFileName)
        {
            MergeFileList.Add(baseFileName);
        }

        public bool InUse(string baseFileName)
        {
            return MergeFileList.Contains(baseFileName);
        }

        public bool RemoveFile(string baseFileName)
        {
            return MergeFileList.Remove(baseFileName);
        }
    }
}
