namespace WebApplication2.Models
{
    public class FileSort
    {
        public const string PART_NUMBER = ".partNumber-";
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }
        /// <summary>
        /// 文件分片号
        /// </summary>
        public int PartNumber { get; set; }
    }
}
