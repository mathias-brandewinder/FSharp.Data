F# Data: Library for Data Access
================================

The F# Data library (`FSharp.Data.dll`) implements everything you need to 
access data in your F# applications and scripts. It implements F# type 
providers for working with structured file formats (CSV, JSON and XML) 
and for accessing the WorldBank and Freebase data. It also includes helpers for parsing 
JSON and CSV files and for sending HTTP requests.

### Library philosophy

This library focuses on providing a simple read-only access to the structured documents 
and other data sources. It does not aim to be a comprehensive collection of F# type providers 
(which can be used for numerous other purposes). It's also designed to play well with other libraries
like [Deedle](http://bluemountaincapital.github.io/Deedle), [F# R Type Provider](http://bluemountaincapital.github.io/FSharpRProvider), [F# Charting](http://fsharp.github.io/FSharp.Charting) and [FunScript](http://funscript.info).

### Library license

The library is available under Apache 2.0. For more information see the 
[License file][license] in the GitHub repository. In summary, this means that you can 
use the library for commercial purposes, fork it, and modify it as you wish.

<br/><hr/>

# How to get FSharp.Data

* The F# Data Library is available as <a href="https://nuget.org/packages/FSharp.Data">FSharp.Data on NuGet</a>.

* In addition to the official releases, you can also get NuGet packages from the [Continuous Integration package source](https://ci.appveyor.com/nuget/fsharp-data-q9vtdm6ej782).

* Alternatively, you can download the [source as a ZIP file][source] or download the [compiled binaries][compiled] as a ZIP. <br /> Please note that on windows when downloading a zip file with `dll` files the files will be blocked, and you have to manually unblock them in the file properties.

<br/><hr/>

# Using F# Data

### F# type providers

The type providers for structured file formats infer the structure of a sample 
document (or a document containing multiple samples). The structure is then used
to provide easy to use type-safe access to documents that follow the same structure.
For more information see:

 * [XML Type Provider](library/XmlProvider.html) - discusses the `XmlProvider<..>` type
 * [JSON Type Provider](library/JsonProvider.html) - discusses the `JsonProvider<..>` type
 * [CSV Type Provider](library/CsvProvider.html) - discusses the `CsvProvider<..>` type

The library also implements a type provider for accessing data from 
[the WorldBank](http://data.worldbank.org/) and [Freebase graph database](http://www.freebase.com/).

 * [WorldBank Provider](library/WorldBank.html) - discusses the `WorldBankData` type 
   and the `WorldBankDataProvider<..>` type
 * [Freebase Provider](library/Freebase.html) - discusses the `FreebaseData` type 
   and the `FreebaseDataProvider<..>` type

### Data access tools
 
In addition to the F# type providers, the library also defines several types that 
simplify data access. In particular, it includes tools for HTTP web requests and a 
JSON and CSV parsers with simple dynamic API. For more information about these types, see the 
following topics:

 * [JSON Parser and Reader](library/JsonValue.html) - introduces the JSON parser 
   (without using the type provider)
 * [CSV Parser and Reader](library/CsvFile.html) - introduces the CSV parser 
   (without using the type provider)
 * [HTTP Utilities](library/Http.html) - discusses the `Http` type that can be used
   to send HTTP web requests.

### Tutorials

The above articles cover all key features of the F# Data library. However, if you're interested
in more samples or more details, then the following tutorials contain additional examples that use multiple different features together:

 * [Converting between JSON and XML](tutorials/JsonToXml.html) - implements two serialization 
   functions that convert between the standard .NET `XElement` and the `JsonValue` from F# Data.
   The tutorial demonstrates pattern matching on `JsonValue`.

 * [Anonymizing JSON](tutorials/JsonAnonymizer.html) - implements a function to anonymize a `JsonValue` from F# Data.
   The tutorial demonstrates pattern matching on `JsonValue`.

### Reference Documentation

There's also [reference documentation](reference) available. Please note that everything under the `FSharp.Data.Runtime` namespace is not considered as part of the public API and can change without notice.

<br/><hr/>

# Contributing

F# Data is made possible by the volunteer work [of more than a dozen contributors](https://github.com/fsharp/FSharp.Data/graphs/contributors) and we're open to contributions from anyone. If you want to help out but don't know where to start, you can take one of the [Up-For-Grabs](https://github.com/fsharp/FSharp.Data/issues?labels=up-for-grabs&state=open) issues, or help to improve the documentation.

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding new public API's, please also 
contribute [samples][samples] that can be turned into a documentation.

 * If you want to discuss an issue or feature that you want to add the to the library,
   then you can submit [an issue or feature request][issues] via Github or you can 
   send an email to the [F# open source][fsharp-oss] mailing list.

 * For more information about the library architecture, organization, how to debug, etc., see the [contributing to F# data](contributing.html) page.

  [source]: https://github.com/fsharp/FSharp.Data/zipball/master
  [compiled]: https://github.com/fsharp/FSharp.Data/zipball/release
  [samples]: https://github.com/fsharp/FSharp.Data/tree/master/docs/content
  [gh]: https://github.com/fsharp/FSharp.Data
  [issues]: https://github.com/fsharp/FSharp.Data/issues
  [license]: https://github.com/fsharp/FSharp.Data/blob/master/LICENSE.md
  [fsharp-oss]: http://groups.google.com/group/fsharp-opensource
