# FDIS

file distributor thingy

I'm mostly using this to learn how I can use channels.

## Config

Can be set by json or command line args. Command line args have the same name and can be set like ``fdis --source="d:\output"``

Overwrites:
``Command line args`` overwrite ``json configs`` overwrite ``default configs``.

```json
{
  "Source": "D:\\Repos\\C#\\fdis\\samples",
  "Threads": 1,
  "Provider": {
    "Type": "FileReader",
    "Options": {
    }
  },
  "Consumers": [
    {
      "Type": "FileWriter",
      "Options": {
        "SaveFolder": "D:\\Repos\\C#\\fdis\\output",
        "BufferSize": 81920
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
        "Regex": ".*\\.jpg",
        "Subfolder": "jpg\\"
      }
    },
    {
      "Type": "DeduplicateFiles",
      "Options": {
        "BufferSize": 64,
        "Scans": 5
      }
    },
    {
      "Type": "FilePathCollisionSolver",
      "Options": {
        "Mode": "Rename"
      }
    },
    {
      "Type": "FileArchiver",
      "Options": {
        "Regex": "^(?!.*\\.zip$).*$",
        "ArchiveName": "unarchivedFiles.zip"
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
- Consumers
    - ``FileWriter``
        - Writes items to ``SaveFolder``
        - ``BufferSize[81920]`` for the streams for reading/writing
