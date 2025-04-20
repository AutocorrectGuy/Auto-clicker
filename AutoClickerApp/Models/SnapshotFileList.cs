using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class SnapshotFileList
{
  private string _path;

  public string[] Paths { get; set; }
  public string[] Names { get; set; }

  public SnapshotFileList(string path)
  {
    _path = path;
    Load();
  }

  public void Load()
  {
    var files = new DirectoryInfo(_path)
        .GetFiles("*.csv")
        .OrderByDescending(f => f.CreationTime)
        .ToArray();

    string[] paths = files.Select(f => f.FullName).ToArray();
    string[] names = files.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToArray();

    Paths = paths;
    Names = names;
  }
}

