# Blizztrack

Blizztrack is a collection of projects:

- A framework written in .NET Core designed to load and treat TACT files as an explorable file systems. It is designed for minimal memory usage and high performance.
- An [ASP.NET](https://dotnet.microsoft.com/en-us/apps/aspnet) Core server.

Preliminatory: if you don't know what TACT or Ribbit are, head over to [the](https://wowdev.wiki/TACT) [wiki](https://wowdev.wiki/Ribbit).

## Blizztrack.Framework

The framework provides base implementations of various types and also expects concepts to be understood.

### `FileSystem`
This class is designed to provide exploratory APIs over a file system comprised of multiple TACT files. It exposes API that returns one or many [resource descriptors](#resourcedescriptor).

The file system does not initialize itself from a set of basic properties, such as a product code, a build configuration hash, and a CDN coonfiguration hash. Instead, you are expected
to supply objects that model each of the core files in a TACT container:

- [`Encoding`](#encoding-wiki)
- [`IIndex`](#indices) (both `indices` and `file-index` from the CDN configuration)
- [`Root`](#root-wiki)
- [`Install`](#install-wiki)

### `ResourceDescriptor`

A resource descriptor:
- is not actually readable: there is no way to get access to the binary data. For that, we need a [`ResourceHandle`](#resourcehandle).
- models a file in a TACT file system. It has the following properties:
  - `Product` This is a product code (as seen from Ribbit endpoints).
  - `Archive` This is the encoding key of the TACT archive that holds this file.
  - `EncodingKey` This is the encoding key of the file.
  - `ContentKey` This is the content key of the file.
     Note that this information may or may not be available; if it isn't, the value will be `ContentKey.Zero`.
  - `Offset` This is the absolute offset at which the BLTE-compressed data for this resource starts in `Archive`.
  - `Length` This is the length of the BLTE-compressed data for this resource in `Archive`.


### `ResourceHandle`

A resource handle is effectively a very thin wrapper around a file on disk.

⚠️ These files cannot (at the moment) be on network drives: they are **almost always** memory-mapped. There are performance implications
here: a read from a memory-mapped file on a network drive may incur excessive network data transfer, adding latency.

To transform a [resource descriptor](#resourcedescriptor) into a resource handle, calling code needs to interface with an implementation
of [`IResourceLocator`](#iresourcelocator).

### `IResourceLocator`

This interface provides utility methods to associate a [resource descriptor](#resourcedsescriptor) with a [resource handle](#resourcehandle),
as well as well-known C# types such as a `Stream`. Because it effectively will do disk I/O, all methods in this interface should be
asynchronous.

The library (as of writing) does not currently provide a default implementation of this type, in an effort to make no assumption about the way
files are stored on disk.

*to be continued...*
