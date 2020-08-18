using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebApplication2.Attributes;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private const string DEFAULT_FOLDER = "a658591407c04576cff0649dbff0d285";
        private readonly IWebHostEnvironment _environment;
        private readonly HttpContext context;
        public UploadController(IHttpContextAccessor accessor,
            IWebHostEnvironment environment)
        {
            context = accessor?.HttpContext ?? throw new ArgumentNullException(nameof(accessor));
            _environment = environment;
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
            if (!IsMultipartContentType(context.Request.ContentType))
            {
                return BadRequest();
            }

            var boundary = GetBoundary();
            if (string.IsNullOrEmpty(boundary))
            {
                return BadRequest();
            }

            var reader = new MultipartReader(boundary, context.Request.Body);

            var section = await reader.ReadNextSectionAsync();

            while (section != null)
            {
                var buffer = new byte[chunk.Size];
                var fileName = GetFileName(section.ContentDisposition);
                chunk.FileName = fileName;
                var path = Path.Combine(_environment.WebRootPath, DEFAULT_FOLDER, fileName);
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

            //计算上传文件大小实时反馈进度（TODO)

            //合并文件（可能涉及转码等）
            if (chunk.PartNumber == chunk.Chunks)
            {
                await MergeChunkFile(chunk);
            }

            return Ok();
        }

        private bool IsMultipartContentType(string contentType)
        {
            return
                !string.IsNullOrEmpty(contentType) &&
                contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetBoundary()
        {
            var mediaTypeHeaderContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);

            return HeaderUtilities.RemoveQuotes(mediaTypeHeaderContentType.Boundary).Value;
        }

        private string GetFileName(string contentDisposition)
        {
            return contentDisposition
                .Split(';')
                .SingleOrDefault(part => part.Contains("filename"))
                .Split('=')
                .Last()
                .Trim('"');
        }

        public async Task MergeChunkFile(FileChunk chunk)
        {
            var uploadDirectoryName = Path.Combine(_environment.WebRootPath, DEFAULT_FOLDER, chunk.FileName);

            var partToken = FileSort.PART_NUMBER;

            var baseFileName = chunk.FileName.Substring(0, chunk.FileName.IndexOf(partToken));

            var searchpattern = $"{Path.GetFileName(baseFileName)}{partToken}*";

            var filesList = Directory.GetFiles(Path.GetDirectoryName(uploadDirectoryName), searchpattern);
            if (!filesList.Any()) { return; }

            var mergeFiles = new List<FileSort>();

            string fileNameNumber;
            foreach (string fileName in filesList)
            {
                fileNameNumber = fileName.Substring(fileName.IndexOf(FileSort.PART_NUMBER) + FileSort.PART_NUMBER.Length);

                int.TryParse(fileNameNumber, out var number);
                if (number <= 0)
                {
                    continue;
                }

                mergeFiles.Add(new FileSort
                {
                    FileName = fileName,
                    PartNumber = number
                });
            }

            // 按照分片排序
            var mergeFileSorts = mergeFiles.OrderBy(s => s.PartNumber).ToArray();

            mergeFiles.Clear();
            mergeFiles = null;

            // 合并文件
            var fileFullPath = Path.Combine(_environment.WebRootPath, DEFAULT_FOLDER, baseFileName);
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
            Parallel.ForEach(mergeFiles, f =>
            {
                System.IO.File.Delete(f.FileName);
            });

            Array.Clear(mergeFileSorts, 0, mergeFileSorts.Length);
            mergeFileSorts = null;
        }
    }
}
