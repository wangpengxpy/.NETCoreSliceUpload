using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebApplication2.Attributes;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private const string DEFAULT_FOLDER = "Upload";
        private readonly IWebHostEnvironment _environment;

        public UploadController(IWebHostEnvironment environment)
        {
            this._environment = environment;
        }

        /// <summary>
        /// 文件分片上传
        /// </summary>
        /// <param name="chunk"></param>
        /// <returns></returns>
        [HttpPost]
        [DisableFormValueModelBinding]
        public async Task<IActionResult> Upload([FromQuery] FileChunk chunk)
        {
            if (!this.IsMultipartContentType(this.Request.ContentType))
            {
                return this.BadRequest();
            }

            var boundary = this.GetBoundary();
            if (string.IsNullOrEmpty(boundary))
            {
                return this.BadRequest();
            }

            var reader = new MultipartReader(boundary, this.Request.Body);

            var section = await reader.ReadNextSectionAsync();

            while (section != null)
            {
                var buffer = new byte[chunk.Size];
                var fileName = this.GetUploadFileSerialName(section.ContentDisposition);
                chunk.FileName = fileName;
                var path = Path.Combine(this._environment.WebRootPath, DEFAULT_FOLDER, fileName);
                using (var stream = new FileStream(path, FileMode.Append))
                {
                    int bytesRead;
                    do
                    {
                        bytesRead = await section.Body.ReadAsync(buffer, 0, buffer.Length);
                        stream.Write(buffer, 0, bytesRead);

                    } while (bytesRead > 0);
                }

                section = await reader.ReadNextSectionAsync();
            }

            //TODO: 计算上传文件大小实时反馈进度

            //合并文件（可能涉及转码等）
            if (chunk.PartNumber == chunk.Chunks)
            {
                await this.MergeChunkFile(chunk);
            }

            return this.Ok();
        }

        /// <summary>
        /// 判断是否含有上传文件
        /// </summary>
        /// <param name="contentType"></param>
        /// <returns></returns>
        private bool IsMultipartContentType(string contentType)
        {
            return !string.IsNullOrEmpty(contentType)
                   && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 得到上传文件的边界
        /// </summary>
        /// <returns></returns>
        private string GetBoundary()
        {
            var mediaTypeHeaderContentType = MediaTypeHeaderValue.Parse(this.Request.ContentType);
            return HeaderUtilities.RemoveQuotes(mediaTypeHeaderContentType.Boundary).Value;
        }

        /// <summary>
        /// 得到带有序列号的上传文件名
        /// </summary>
        /// <param name="contentDisposition"></param>
        /// <returns></returns>
        private string GetUploadFileSerialName(string contentDisposition)
        {
            return contentDisposition
                                    .Split(';')
                                    .SingleOrDefault(part => part.Contains("filename"))
                                    .Split('=')
                                    .Last()
                                    .Trim('"');
        }

        /// <summary>
        /// 合并文件
        /// </summary>
        /// <param name="chunk"></param>
        /// <returns></returns>
        public async Task MergeChunkFile(FileChunk chunk)
        {
            var uploadDirectoryName = Path.Combine(this._environment.WebRootPath, DEFAULT_FOLDER);

            var baseFileName = chunk.FileName.Substring(0, chunk.FileName.IndexOf(FileSort.PART_NUMBER));

            var searchpattern = $"{Path.GetFileName(baseFileName)}{FileSort.PART_NUMBER}*";

            var fileNameList = Directory.GetFiles(uploadDirectoryName, searchpattern).ToArray();
            if (fileNameList.Length == 0)
            {
                return;
            }

            List<FileSort> mergeFileSortList = new List<FileSort>(fileNameList.Length);

            string fileNameNumber;
            foreach (string fileName in fileNameList)
            {
                fileNameNumber = fileName.Substring(fileName.IndexOf(FileSort.PART_NUMBER) + FileSort.PART_NUMBER.Length);

                int.TryParse(fileNameNumber, out var number);
                if (number <= 0)
                {
                    continue;
                }

                mergeFileSortList.Add(new FileSort
                {
                    FileName = fileName,
                    PartNumber = number
                });
            }

            // 按照分片排序
            FileSort[] mergeFileSorts = mergeFileSortList.OrderBy(s => s.PartNumber).ToArray();

            mergeFileSortList.Clear();
            mergeFileSortList = null;

            // 合并文件
            string fileFullPath = Path.Combine(this._environment.WebRootPath, DEFAULT_FOLDER, baseFileName);
            if (System.IO.File.Exists(fileFullPath))
            {
                System.IO.File.Delete(fileFullPath);
            }
            bool error = false;
            using var fileStream = new FileStream(fileFullPath, FileMode.Create);
            foreach (FileSort fileSort in mergeFileSorts)
            {
                error = false;
                do
                {
                    try
                    {
                        using FileStream fileChunk = new FileStream(fileSort.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                        await fileChunk.CopyToAsync(fileStream);
                        error = false;
                    }
                    catch (Exception)
                    {
                        error = true;
                        Thread.Sleep(0);
                    }
                }
                while (error);
            }

            //删除分片文件
            foreach (FileSort fileSort in mergeFileSorts)
            {
                System.IO.File.Delete(fileSort.FileName);
            }
            Array.Clear(mergeFileSorts, 0, mergeFileSorts.Length);
            mergeFileSorts = null;
        }
    }
}
