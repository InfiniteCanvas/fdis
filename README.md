# FDIS

file distributor thingy

I'm mostly using this to learn how I can use channels.

## Config

Can be set by json or command line args. Command line args have the same name and can be set like ``fdis --source="d:\output"``

Overwrites:
``Command line args`` overwrite ``json configs`` overwrite ``default configs``.

```json
{
  "Source": "Z:\\source\\folder",
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
        "SaveFolder": "D:\\output",
        "BufferSize": 81920
      }
    }
  ],
  "Middlewares": [
    {
      "Type": "FileFilter",
      "Options": {
        "Regex": ".*\\.(wav|m4a|mp3|ogg|flac)",
        "Mode": "Reject"
      }
    },
    {
      "Type": "FileArchiver",
      "Options": {
        "Regex": ".*\\.(jpg|jpeg|png|gif)$",
        "ArchiveName": "pics.zip"
      }
    },
    {
      "Type": "FileArchiver",
      "Options": {
        "Regex": ".*\\.(mov|mp4|mkv|webm|avi)$",
        "ArchiveName": "vods.zip"
      }
    },
    {
      "Type": "FileArchiver",
      "Options": {
        "Regex": "^(?!.*\\.zip$).*$",
        "ArchiveName": "rest.zip"
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
- Consumers
    - ``FileWriter``
        - Writes items to ``SaveFolder``
        - ``BufferSize[81920]`` for the streams for reading/writing
