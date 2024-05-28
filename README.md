# FDIS

file distributor thingy

I'm mostly using this to learn how I can use channels.

## Config

Can be set by json or command line args. Command line args have the same name and can be set like ``fdis --source="d:\output"``

Command line args overwrite json and default configs.

```json
{
  "Source": "d:\\test\\samples",
  "Provider": "FileReader",
  "Consumers": [
    "FileWriter"
  ],
  "SaveFolder": "d:\\test\\output",
  "Threads": 1,
  "Middlewares": [
    "FileArchiver"
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
    - Reads file or files from source
- Middlewares
  - ``FileArchiver``
    - zips everything into an archive using LZMA
- Consumers
  - ``FileWriter``
    - Writes items to ``SaveFolder`` ``[currentWorkingDir\output]``
