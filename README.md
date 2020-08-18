# .NETCoreSliceUpload
.NET Core Web APi大文件分片上传

前端对大文件采取分片上传

后端读取FormData，由于异步上传当合并文件时此时文件还处于缓冲区内未持久化到磁盘，所以会引起IO异常采用Polly一直重试机制简单而粗暴优雅解决（当然可以设置时长重试）

满足一般项目大文件上传，断点续传并未实现，有时间我会实现这个功能。

若需更强大功能支持大文件支撑，请使用基于tus协议的tusdotnet，支持.NET和.NET Core
