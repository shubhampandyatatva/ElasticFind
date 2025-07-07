using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElasticFind.Repository.Data;

[Table("files")]
public partial class File
{
    public int Id { get; set; }

    public string FileName { get; set; } = null!;

    public DateTime? UploadDate { get; set; }

    public string? FileType { get; set; }
    public bool IsDeleted { get; set; }
}
