# FDIS

file distributor thingy

I'm mostly using this to learn how I can use channels.

## Config

Can be set by json or command line
args. [Command line args](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#command-line-configuration-provider) have
the same name, but you should just use the json config.

Overwrites:
``Command line args`` overwrite ``json configs`` overwrite ``default configs``.

```json
{
  "Threads": 1,
  "Providers": [
    {
      "Type": "FileReader",
      "Options": {
        "Source": "D:\\Repos\\C#\\fdis\\samples",
        "Mode": "Unchecked"
      }
    },
    {
      "Type": "FileReader",
      "Options": {
        "Source": "D:\\Repos\\C#\\fdis\\samples2",
        "Mode": "Deduplicate"
      }
    }
  ],
  "Middlewares": [
    {
      "Type": "FileFilter",
      "Options": {
        "Regex": ".*",
        "Mode": "Allow"
      }
    },
    {
      "Type": "FileSorter",
      "Options": {
        "Regex": ".*\\.jpg$",
        "Subfolder": "jpg\\"
      }
    },
    {
      "Type": "DeduplicateFiles",
      "Options": {
        "BufferSize": 64,
        "Scans": 3
      }
    },
    {
      "Type": "FilePathCollisionSolver",
      "Options": {
        "Mode": "Rename"
      }
    },
    {
      "Type": "ConvertImagesToWebp",
      "Options": {
        "Regex": ".*\\.(png|bmp)$",
        "Mode": "Lossless",
        "Quality": 100
      }
    },
    {
      "Type": "FileArchiver",
      "Options": {
        "Regex": "^(?!.*\\.zip$).*$",
        "ArchiveName": "unarchivedFiles.zip"
      }
    }
  ],
  "Consumers": [
    {
      "Type": "FileWriter",
      "Options": {
        "SaveFolder": "D:\\Repos\\C#\\fdis\\output",
        "BufferSize": 81920,
        "Mode": "Overwrite"
      }
    }
  ]
}
```

Defaults are in ``[default]``

- Source
    - Source uri for the provider.
- Threads ``[1]``
    - Max numbers of threads or connections to be used
- Provider
    - ``FileReader``
        - Reads file or files from ``Source``
        - ``Mode["Deduplicate"]``
            - ``Deduplicate`` will reject duplicates from source if they already exist in the channel
            - ``Unchecked`` no checks, adds everything
- Middlewares (run from top to bottom, sequentially)
    - ``FileArchiver``
        - zips everything into an archive using Deflate
        - ``Regex`` matches things it should put into the archive
        - ``ArchiveName`` will be the name of the resulting archive; relative subfolder will be empty
    - ``FileFilter``
        - allows or rejects files based on a regex and the filter mode
        - ``Regex`` expression to match against the file path relative to source
        - ``Mode["Allow"]``
            - 'Allow' will let matched files through and reject everything else
            - 'Reject' will block matched files and let everything else through
    - ``FileSorter``
        - sorts matched files into specified subfolder
        - ``Regex`` for matching files
        - ``Subfolder`` change a matched item's subfolder to this
    - ``FilePathCollisionSolver``
        - looks for items that have the same subfolder & file name
        - ``Mode["Remove"]`` ``Rename`` renames items that collide, ``Remove`` removes items that collide so that only 1 remains
    - ``DeduplicateFiles``
        - looks for items that have the same file content by comparing probes of ``BufferSize``, ``Scans`` times
    - ``ConvertImagesToWebp``
        - tries to convert matched items to webp
        - ``Regex[@".*\.(png|bmp)"]`` matches filenames and processes only those
        - ``Mode[Lossy]`` sets whether to use lossless or lossy compression
        - ``Quality[75]`` sets the quality of encoding. For lossy, 0 is smallest, worst quality, 100 is biggest, best quality. For lossless, 0 is
          fastest with least compression, 100 is slowest with highest compression
- Consumers
    - ``FileWriter``
        - Writes items to ``SaveFolder``
        - ``BufferSize[81920]`` for the streams for reading/writing
