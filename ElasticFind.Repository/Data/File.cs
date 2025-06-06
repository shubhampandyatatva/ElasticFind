using System;
using System.Collections.Generic;

namespace ElasticFind.Repository.Data;

public partial class File
{
    public int Id { get; set; }

    public string FileName { get; set; } = null!;

    public DateTime? UploadDate { get; set; }

    public string? FileType { get; set; }
}
